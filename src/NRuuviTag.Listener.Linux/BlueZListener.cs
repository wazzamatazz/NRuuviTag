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
    /// The logger for the listener.
    /// </summary>
    private readonly ILogger<BlueZListener> _logger;


    /// <summary>
    /// Creates a new <see cref="BlueZListener"/> object.
    /// </summary>
    /// <param name="adapterName">
    ///   The Bluetooth adapter to monitor.
    /// </param>
    /// <param name="logger">
    ///   The logger for the listener.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <paramref name="adapterName"/> is <see langword="null"/> of white space.
    /// </exception>
    public BlueZListener(string adapterName = DefaultBluetoothAdapter, ILogger<BlueZListener>? logger = null) {
        if (string.IsNullOrWhiteSpace(adapterName)) {
            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.Error_AdapterNameIsRequired, DefaultBluetoothAdapter), nameof(adapterName));
        }
        _adapterName = adapterName;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BlueZListener>.Instance;
    }


    /// <inheritdoc/>
    protected override async IAsyncEnumerable<RuuviTagSample> ListenAsync(
        Func<string, bool>? filter, 
        [EnumeratorCancellation]
        CancellationToken cancellationToken
    ) {
        var channel = Channel.CreateUnbounded<RuuviTagSample>(new UnboundedChannelOptions() {
            SingleReader = true,
            SingleWriter = false
        });

        var running = true;

        // Get the adapter from BlueZ.
        using var adapter = await BlueZManager.GetAdapterAsync(_adapterName).ConfigureAwait(false);
        using var @lock = new SemaphoreSlim(1, 1);
            
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
                if (!running || cancellationToken.IsCancellationRequested) {
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

                if (filter != null && !filter.Invoke(props.Address)) {
                    // We are not interested in this RuuviTag.
                    disposeDevice = true;
                    LogDeviceIgnored(props.Address, "failed filter check");
                    return;
                }

                LogDeviceFound(props.Address);
                    
                // Watch for changes to this device.
                if (!await AddDeviceWatcher(args.Device, props).ConfigureAwait(false)) {
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
            running = false;
            channel.Writer.TryComplete();

            // Dispose of the watcher registrations.
            await @lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try {
                foreach (var item in watchers.Values) {
                    item.Dispose();
                }
                watchers.Clear();
            }
            finally {
                @lock.Release();
            }
        }

        yield break;

        // Adds a watcher for the specified device so that we can emit new samples when the
        // device properties change.
        async Task<bool> AddDeviceWatcher(global::Linux.Bluetooth.Device device, Device1Properties properties) {
            if (!running || cancellationToken.IsCancellationRequested) {
                return false;
            }

            await @lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
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
            finally {
                @lock.Release();
            }
        }

            
        void UpdateDeviceProperties(Device1Properties properties, Tmds.DBus.PropertyChanges changes) {
            if (!running || cancellationToken.IsCancellationRequested) {
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
            if (!running || cancellationToken.IsCancellationRequested) {
                return;
            }

            try {
                if (!properties.ManufacturerData.TryGetValue(Constants.ManufacturerId, out var o) || o is not byte[] payload) {
                    throw new InvalidOperationException("Device properties did not contain manufacturer data.");
                }

                var timestamp = DateTimeOffset.Now;

                if (_logger.IsEnabled(LogLevel.Trace)) {
                    var sb = new StringBuilder("0x");
                    foreach (var b in payload) {
                        sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
                    }
                    LogRawDeviceData(properties.Address, timestamp, sb.ToString());
                }
                
                if (!RuuviTagUtilities.TryParsePayload(payload, out var sample)) {
                    return;
                }
                    
                if (channel!.Writer.TryWrite(new RuuviTagSample(timestamp, properties.RSSI, sample))) {
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
