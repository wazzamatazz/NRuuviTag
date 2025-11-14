using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
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
    /// The listener options.
    /// </summary>
    private readonly BlueZListenerOptions _options;
    
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
    public BlueZListener(BlueZListenerOptions options, IDeviceResolver? deviceLookup = null, TimeProvider? timeProvider = null, ILogger<BlueZListener>? logger = null) : base(options, deviceLookup, timeProvider, logger) {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _adapterName = string.IsNullOrWhiteSpace(_options.AdapterName) 
            ? DefaultBluetoothAdapter 
            : _options.AdapterName;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BlueZListener>.Instance;
    }


    /// <inheritdoc/>
    protected override async Task RunAsync(CancellationToken cancellationToken) {
        // Get the adapter from BlueZ.
        using var adapter = await BlueZManager.GetAdapterAsync(_adapterName).ConfigureAwait(false);
            
        // Registrations for devices that we are observing.
        var watchers = new ConcurrentDictionary<string, IDisposable>(StringComparer.OrdinalIgnoreCase);

        // Set BlueZ discovery filter to receive LE advertisements and to allow duplicates.
        await adapter.SetDiscoveryFilterAsync(
            new Dictionary<string, object>() {
                ["Transport"] = "le",
                ["DuplicateData"] = _options.AllowDuplicateAdvertisements
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

                if (watchers.ContainsKey(props.Address)) {
                    // We are already watching this device.
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
            // Wait until cancelled.
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        finally {
            // Stop scanning.
            try {
                await adapter.StopDiscoveryAsync().ConfigureAwait(false);
            }
            catch {
                // Ignore errors during stop.
            }

            // Dispose of the watcher registrations.
            foreach (var item in watchers.Values) {
                item.Dispose();
            }
            watchers.Clear();
        }

        return;

        // Adds a watcher for the specified device so that we can emit new samples when the
        // device properties change.
        async Task<bool> AddDeviceWatcher(global::Linux.Bluetooth.Device device, Device1Properties properties) {
            if (cancellationToken.IsCancellationRequested) {
                return false;
            }
            
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

                DataReceived(properties.Address, properties.RSSI, payload);
            }
            catch (Exception error) {
                LogInvalidManufacturerData(properties.Address, error);
            }
        }
    }


    [LoggerMessage(11, LogLevel.Debug, "Starting listener using Bluetooth device {adapterName}.")]
    partial void LogListenerStarting(string adapterName);


    [LoggerMessage(12, LogLevel.Debug, "Found device {address}.")]
    partial void LogDeviceFound(string address);


    [LoggerMessage(13, LogLevel.Trace, "Ignoring device {address}: {reason}.")]
    partial void LogDeviceIgnored(string address, string reason);


    [LoggerMessage(14, LogLevel.Warning, "Invalid manufacturer data received for device {address}.")]
    partial void LogInvalidManufacturerData(string address, Exception error);

}
