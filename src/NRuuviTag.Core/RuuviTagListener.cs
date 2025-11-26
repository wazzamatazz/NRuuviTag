using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Nito.AsyncEx;

namespace NRuuviTag;

/// <summary>
/// Base <see cref="IRuuviTagListener"/> implementation.
/// </summary>
public abstract partial class RuuviTagListener : IRuuviTagListener {

    /// <summary>
    /// The logger for the listener.
    /// </summary>
    private readonly ILogger<RuuviTagListener> _logger;
    
    /// <summary>
    /// Listener options.
    /// </summary>
    private readonly RuuviTagListenerOptions _options;
    
    /// <summary>
    /// The type of the listener.
    /// </summary>
    /// <remarks>
    ///   Used in metric tags.
    /// </remarks>
    private readonly string _listenerType;

    /// <summary>
    /// The time provider to use for timestamps.
    /// </summary>
    private readonly TimeProvider _timeProvider;
    
    /// <summary>
    /// Indicates whether the listener is actively listening.
    /// </summary>
    private readonly AsyncManualResetEvent _listeningSignal = new AsyncManualResetEvent();
    
    /// <summary>
    /// Channel that emits received samples.
    /// </summary>
    private readonly Channel<RuuviTagSample> _sampleChannel = Channel.CreateUnbounded<RuuviTagSample>();

    /// <summary>
    /// The counter for the number of observed samples.
    /// </summary>
    private static readonly Counter<long> s_observedSamplesCounter = Telemetry.Meter.CreateCounter<long>(
        "listener.observed_samples",
        unit: "{samples}",
        description: "The number of observed samples from RuuviTag devices.");
    
    /// <summary>
    /// The device lookup service.
    /// </summary>
    protected IDeviceResolver DeviceResolver { get; }
    
    /// <summary>
    /// Specifies whether only samples from known devices should be processed.
    /// </summary>
    protected bool KnownDevicesOnly => _options.KnownDevicesOnly;
    
    /// <summary>
    /// Specifies whether extended advertisement formats should be enabled.
    /// </summary>
    protected bool EnableExtendedAdvertisementFormats => _options.EnableExtendedAdvertisementFormats;


    /// <summary>
    /// Creates a new <see cref="RuuviTagListener"/> instance.
    /// </summary>
    /// <param name="options">
    ///   The listener options.
    /// </param>
    /// <param name="deviceResolver">
    ///   The device lookup service.
    /// </param>
    /// <param name="timeProvider">
    ///   The time provider to use for timestamps.
    /// </param>
    /// <param name="logger">
    ///   The logger for the listener.
    /// </param>
    protected RuuviTagListener(RuuviTagListenerOptions options, IDeviceResolver? deviceResolver = null, TimeProvider? timeProvider = null, ILogger<RuuviTagListener>? logger = null) {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        DeviceResolver = deviceResolver ?? new NullDeviceResolver();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RuuviTagListener>.Instance;
        _listenerType = GetType().FullName!;
    }


    /// <summary>
    /// Waits until the listener has started listening for advertisements.
    /// </summary>
    /// <param name="cancellationToken">
    ///   The cancellation token for the operation.
    /// </param>
    public async ValueTask WaitForListenStartedAsync(CancellationToken cancellationToken = default) {
        await _listeningSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
    }


    /// <inheritdoc/>
    async IAsyncEnumerable<RuuviTagSample> IRuuviTagListener.ListenAsync([EnumeratorCancellation] CancellationToken cancellationToken) {
        _listeningSignal.Set();

        try {
            _ = Task.Run(async () => {
                try {
                    await RunAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
                catch (Exception e) {
                    LogRunError(e);
                }
            }, cancellationToken);
            
            await foreach (var item in _sampleChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) {
                var instanceTagList = new TagList() {
                    {
                        "listener.type", _listenerType
                    }, 
                    {
                        "hw.id", item.MacAddress
                    }, 
                    {
                        "hw.type", "ruuvitag"
                    }
                };
                
                s_observedSamplesCounter.Add(1, instanceTagList);
                yield return item;
            }
        }
        finally {
            _listeningSignal.Reset();
        }
    }


    /// <summary>
    /// Signals that new advertisement data has been received from a Ruuvi device.
    /// </summary>
    /// <param name="macAddress">
    ///   The MAC address of the device that sent the data.
    /// </param>
    /// <param name="rssi">
    ///   The RSSI of the received advertisement.
    /// </param>
    /// <param name="data">
    ///   The raw advertisement data received.
    /// </param>
    /// <remarks>
    ///   It is assumed that a check has already been performed on the advertisement data to
    ///   ensure that the manufacturer matches <see cref="Constants.ManufacturerId"/>.
    /// </remarks>
    protected void DataReceived(string macAddress, double rssi, Span<byte> data) {
        if (!_listeningSignal.IsSet) {
            return;
        }

        if (data.Length == 0) {
            return;
        }
        
        if (!EnableExtendedAdvertisementFormats && data[0] == Constants.DataFormatExtendedV1) {
            // Ignore data format E1 unless we've enabled extended advertisement formats.
            return;
        }

        var device = DeviceResolver.GetDeviceInformation(macAddress);
        if (KnownDevicesOnly && device is null) {
            // We're only interested in known devices, and this one is unknown.
            return;
        }

        var timestamp = _timeProvider.GetUtcNow();
        
        if (_logger.IsEnabled(LogLevel.Trace)) {
            var sb = new StringBuilder("0x");
            foreach (var b in data) {
                sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
            LogRawDeviceData(macAddress, timestamp, sb.ToString());
        }
                
        if (!RuuviTagUtilities.TryParsePayload(data, out var sample)) {
            return;
        }
        
        // Create the full sample from the parsed payload.
        var fullSample = new RuuviTagSample(device?.DeviceId, timestamp, rssi, sample) {
            MacAddress = sample.DataFormat switch {
                // If the payload uses data format 6 then the MAC address in the payload will
                // only contain the lower 3 bytes of the address. We will replace this with
                // the full MAC address from the advertisement.
                Constants.DataFormat6 => macAddress,
                _ => sample.MacAddress
            }
        };

        EmitSample(fullSample);
    }


    /// <summary>
    /// Emits the specified sample.
    /// </summary>
    /// <param name="sample">
    ///   The sample to emit.
    /// </param>
    /// <remarks>
    ///   This method is not intended to be called directly. Instead, implementers should call
    ///   <see cref="DataReceived"/> when new data is available.
    /// </remarks>
    protected void EmitSample(RuuviTagSample sample) {
        if (!_listeningSignal.IsSet) {
            return;
        }
        
        if (!_sampleChannel.Writer.TryWrite(sample)) {
            // Unable to write the sample to the channel.
            return;
        }

        LogSampleEmitted(sample.MacAddress!, sample.Timestamp ?? _timeProvider.GetUtcNow());
    }
    

    /// <summary>
    /// Runs the listener until cancelled.
    /// </summary>
    /// <param name="cancellationToken">
    ///   The cancellation token that can be cancelled when the listener should stop.
    /// </param>
    /// <returns>
    ///   A <see cref="Task"/> that represents the asynchronous operation.
    /// </returns>
    /// <remarks>
    ///   Implementers should call <see cref="DataReceived"/> when new data is available.
    /// </remarks>
    protected abstract Task RunAsync(CancellationToken cancellationToken);
    
    
    [LoggerMessage(1, LogLevel.Trace, "Raw device data from {address} @ {timestamp}: {byteString}.", SkipEnabledCheck = true)]
    partial void LogRawDeviceData(string address, DateTimeOffset timestamp, string byteString);
    
    [LoggerMessage(2, LogLevel.Trace, "Emitted sample for device {address} @ {timestamp}.")]
    partial void LogSampleEmitted(string address, DateTimeOffset timestamp);
    
    [LoggerMessage(3, LogLevel.Error, "An error occurred while running the RuuviTag listener.")]
    partial void LogRunError(Exception exception);

}
