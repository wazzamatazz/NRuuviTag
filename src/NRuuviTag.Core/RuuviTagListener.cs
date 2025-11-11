using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Nito.AsyncEx;

namespace NRuuviTag;

/// <summary>
/// Base <see cref="IRuuviTagListener"/> implementation.
/// </summary>
public abstract class RuuviTagListener : IRuuviTagListener {

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
    /// Indicates whether the listener is actively listening.
    /// </summary>
    private readonly AsyncManualResetEvent _listeningSignal = new AsyncManualResetEvent();

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
    /// Specifies whether Data Format 6 advertisements should be ignored.
    /// </summary>
    protected bool EnableDataFormat6 => _options.EnableDataFormat6;


    /// <summary>
    /// Creates a new <see cref="RuuviTagListener"/> instance.
    /// </summary>
    /// <param name="options">
    ///   The listener options.
    /// </param>
    /// <param name="deviceResolver">
    ///   The device lookup service.
    /// </param>
    protected RuuviTagListener(RuuviTagListenerOptions options, IDeviceResolver? deviceResolver = null) {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        DeviceResolver = deviceResolver ?? new NullDeviceResolver();
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
            await foreach (var item in ListenAsync(cancellationToken).ConfigureAwait(false)) {
                if (item is null) {
                    continue;
                }

                // Ignore data format 6 if configured to do so.
                if (!EnableDataFormat6 && item is { DataFormat: Constants.DataFormat6 }) {
                    continue;
                }
                
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
    /// Listens for advertisements broadcast by Ruuvi devices until cancelled.
    /// </summary>
    /// <param name="cancellationToken">
    ///   A cancellation token that can be cancelled when the listener should stop.
    /// </param>
    /// <returns>
    ///   An <see cref="IAsyncEnumerable{T}"/> that will emit the received samples as they occur.
    /// </returns>
    /// <remarks>
    ///   Implementers should use <see cref="DeviceResolver"/> to retrieve device information for an
    ///   advertisement. Unknown devices should be skipped if <see cref="KnownDevicesOnly"/> is
    ///   <see langword="true"/>.
    /// </remarks>
    protected abstract IAsyncEnumerable<RuuviTagSample> ListenAsync(CancellationToken cancellationToken);

}
