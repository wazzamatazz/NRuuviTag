using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Linux.Bluetooth;

using Microsoft.Extensions.Logging;

namespace NRuuviTag.Listener.Linux;

/// <summary>
/// <see cref="IRuuviTagListener"/> that uses the BlueZ to listen for RuuviTag 
/// Bluetooth LE advertisements.
/// </summary>
public partial class BlueZListener : RuuviTagListener {

    /// <summary>
    /// Default Bluetooth adapter name.
    /// </summary>
    public const string DefaultBluetoothAdapter = "hci0";

    /// <summary>
    /// The Bluetooth adapter to monitor.
    /// </summary>
    private readonly string _adapterName;
    
    /// <summary>
    /// The time provider.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The logger for the listener.
    /// </summary>
    private readonly ILogger<BlueZListener> _logger;


    /// <summary>
    /// Creates a new <see cref="BlueZListener"/> object.
    /// </summary>
    /// <param name="options">
    ///   The options for the listener.
    /// </param>
    /// <param name="deviceLookup">
    ///   The device lookup service.
    /// </param>
    /// <param name="timeProvider">
    ///   The time provider.
    /// </param>
    /// <param name="logger">
    ///   The logger for the listener.
    /// </param>
    public BlueZListener(BlueZListenerOptions options, IDeviceResolver? deviceLookup = null, TimeProvider? timeProvider = null, ILogger<BlueZListener>? logger = null) : base(options, deviceLookup) {
        _adapterName = string.IsNullOrWhiteSpace(options?.AdapterName) 
            ? DefaultBluetoothAdapter 
            : options.AdapterName;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BlueZListener>.Instance;
    }


    /// <inheritdoc/>
    protected override async IAsyncEnumerable<RuuviTagSample> ListenAsync(
        [EnumeratorCancellation]
        CancellationToken cancellationToken
    ) {
        var channel = Channel.CreateUnbounded<RuuviTagSample>(new UnboundedChannelOptions() {
            SingleReader = true,
            SingleWriter = false
        });

        // Get the adapter from BlueZ.
        using var adapter = await BlueZManager.GetAdapterAsync(_adapterName).ConfigureAwait(false);
        var @lock = new Nito.AsyncEx.AsyncLock();
            
        // Registrations for devices that we are observing.
        var watchers = new Dictionary<string, IDisposable>(StringComparer.OrdinalIgnoreCase);

        // Set BlueZ discovery filter to receive LE advertisements and to allow duplicates.
        await adapter.SetDiscoveryFilterAsync(
            new Dictionary<string, object>() {
                ["Transport"] = "le",
                ["DuplicateData"] = true
            }).ConfigureAwait(false);
        
        // Handler for when BlueZ detects a new device.
        adapter.DeviceFound += async (_, args) => {
            var disposeDevice = false;

            try {
                if (cancellationToken.IsCancellationRequested) {
                    disposeDevice = true;
                    return;
                }

                var props = await args.Device.GetAllAsync().ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested) {
                    disposeDevice = true;
                    return;
                }

                if (props.ManufacturerData == null || !props.ManufacturerData.ContainsKey(Constants.ManufacturerId)) {
                    // This is not a Ruuvi device.
                    LogDeviceIgnored(props.Address, "not a Ruuvi device");
                    disposeDevice = true;
                    return;
                }

                var device = DeviceResolver.GetDeviceInformation(props.Address);
                if (device is null && KnownDevicesOnly) {
                    // We are not interested in this RuuviTag.
                    disposeDevice = true;
                    LogDeviceIgnored(props.Address, "device is not known and only known devices are allowed");
                    return;
                }

                LogDeviceFound(props.Address);
                    
                // Watch for changes to this device.
                if (!await AddDeviceWatcher(args.Device, props, @lock).ConfigureAwait(false)) {
                    disposeDevice = true;
                }
            }
            finally {
                if (disposeDevice) {
                    args.Device.Dispose();
                }
            }
        };

        try {
            // Start scanning.
            LogListenerStarting(_adapterName);
            await adapter.StartDiscoveryAsync().ConfigureAwait(false);
            // Emit samples as they are published to the channel.
            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) {
                yield return item;
            }
        }
        finally {
            // Stop scanning.
            await adapter.StopDiscoveryAsync().ConfigureAwait(false);
            channel.Writer.TryComplete();

            // Dispose of the watcher registrations.
            using var _ = await @lock.LockAsync(default).ConfigureAwait(false);
            foreach (var item in watchers.Values) {
                item.Dispose();
            }
            watchers.Clear();
        }

        yield break;

        // Adds a watcher for the specified device so that we can emit new samples when the
        // device properties change.
        async Task<bool> AddDeviceWatcher(global::Linux.Bluetooth.Device device, Device1Properties properties, Nito.AsyncEx.AsyncLock deviceLock) {
            if (cancellationToken.IsCancellationRequested) {
                return false;
            }

            using var _ = await @deviceLock.LockAsync(cancellationToken).ConfigureAwait(false);
            
            if (watchers.ContainsKey(properties.Address)) {
                return false;
            }

            // Emit initial scan result.
            EmitDeviceProperties(properties);

            watchers[properties.Address] = await device.WatchPropertiesAsync(changes => {
                UpdateDeviceProperties(properties, changes);
            }).ConfigureAwait(false);

            return true;
        }

            
        void UpdateDeviceProperties(Device1Properties properties, Tmds.DBus.PropertyChanges changes) {
            if (cancellationToken.IsCancellationRequested) {
                return;
            }

            // For each change, update the existing properties if a property that we are
            // interested in has changed value.

            var dirty = false;

            foreach (var item in changes.Changed) {
                switch (item.Key) {
                    case nameof(Device1Properties.RSSI):
                        properties.RSSI = Convert.ToInt16(item.Value);
                        dirty = true;
                        break;
                    case nameof(Device1Properties.ManufacturerData):
                        properties.ManufacturerData = (IDictionary<ushort, object>) item.Value;
                        dirty = true;
                        break;
                }
            }

            if (!dirty) {
                return;
            }

            EmitDeviceProperties(properties);
        }

        void EmitDeviceProperties(Device1Properties properties) {
            if (cancellationToken.IsCancellationRequested) {
                return;
            }

            try {
                if (!properties.ManufacturerData.TryGetValue(Constants.ManufacturerId, out var o) || o is not byte[] payload) {
                    throw new InvalidOperationException("Device properties did not contain manufacturer data.");
                }

                var timestamp = _timeProvider.GetUtcNow();
                
                var device = DeviceResolver.GetDeviceInformation(properties.Address);
                if (device is null && KnownDevicesOnly) {
                    // We are no longer interested in this device - it has probably been removed
                    // from the list of known devices since we started scanning.
                }

                if (_logger.IsEnabled(LogLevel.Trace)) {
                    var sb = new StringBuilder("0x");
                    foreach (var b in payload) {
                        sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
                    }
                    LogRawDeviceData(properties.Address, timestamp, sb.ToString());
                }

                if (!EnableDataFormat6 && payload.Length > 0 && payload[0] == Constants.DataFormat6) {
                    // Ignore data format 6 if configured to do so.
                    return;
                }
                
                if (!RuuviTagUtilities.TryParsePayload(payload, out var sample)) {
                    return;
                }
                
                // Create the full sample from the parsed payload.
                var fullSample = new RuuviTagSample(device?.DeviceId, timestamp, properties.RSSI, sample) {
                    MacAddress = sample.DataFormat switch {
                        // If the payload uses data format 6 then the MAC address in the payload will
                        // only contain the lower 3 bytes of the address. We will replace this with
                        // the full MAC address from the advertisement.
                        Constants.DataFormat6 => properties.Address,
                        _ => sample.MacAddress
                    }
                };
                
                if (channel.Writer.TryWrite(fullSample)) {
                    LogSampleEmitted(properties.Address, timestamp);
                }
            }
            catch (Exception error) {
                LogInvalidManufacturerData(properties.Address, error);
            }
        }
    }


    [LoggerMessage(1, LogLevel.Debug, "Starting listener using Bluetooth device {adapterName}.")]
    partial void LogListenerStarting(string adapterName);


    [LoggerMessage(2, LogLevel.Debug, "Found device {address}.")]
    partial void LogDeviceFound(string address);


    [LoggerMessage(3, LogLevel.Trace, "Ignoring device {address}: {reason}.")]
    partial void LogDeviceIgnored(string address, string reason);


    [LoggerMessage(4, LogLevel.Warning, "Invalid manufacturer data received for device {address}.")]
    partial void LogInvalidManufacturerData(string address, Exception error);


    [LoggerMessage(5, LogLevel.Trace, "Emitted sample for device {address} @ {timestamp}.")]
    partial void LogSampleEmitted(string address, DateTimeOffset timestamp);
    
    [LoggerMessage(6, LogLevel.Trace, "Raw device data from {address} @ {timestamp}: {byteString}.", SkipEnabledCheck = true)]
    partial void LogRawDeviceData(string address, DateTimeOffset timestamp, string byteString);

}
