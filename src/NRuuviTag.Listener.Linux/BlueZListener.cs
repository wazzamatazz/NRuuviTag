using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using HashtagChris.DotNetBlueZ;

namespace NRuuviTag.Listener.Linux {

    /// <summary>
    /// <see cref="IRuuviTagListener"/> that uses the BlueZ to listen for RuuviTag 
    /// Bluetooth LE advertisements.
    /// </summary>
    public class BlueZListener : RuuviTagListener {

        /// <summary>
        /// Default Bluetooth adapter name.
        /// </summary>
        public const string DefaultBluetoothAdapter = "hci0";

        /// <summary>
        /// The Bluetooth adapter to monitor.
        /// </summary>
        private readonly string _adapterName;


        /// <summary>
        /// Creates a new <see cref="BlueZListener"/> object.
        /// </summary>
        /// <param name="adapterName">
        ///   The Bluetooth adapter to monitor.
        /// </param>
        /// <exception cref="ArgumentException">
        ///   <paramref name="adapterName"/> is <see langword="null"/> of white space.
        /// </exception>
        public BlueZListener(string adapterName = DefaultBluetoothAdapter) {
            if (string.IsNullOrWhiteSpace(adapterName)) {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.Error_AdapterNameIsRequired, DefaultBluetoothAdapter), nameof(adapterName));
            }
            _adapterName = adapterName;
        }


        /// <inheritdoc/>
        public override async IAsyncEnumerable<RuuviTagSample> ListenAsync(
            Func<string, bool>? filter, 
            [EnumeratorCancellation]
            CancellationToken cancellationToken
        ) {
            var channel = Channel.CreateUnbounded<RuuviTagSample>(new UnboundedChannelOptions() {
                SingleReader = true,
                SingleWriter = false
            });

            var running = true;

            void EmitDeviceProperties(Device1Properties properties) {
                if (!running || cancellationToken.IsCancellationRequested) {
                    return;
                }

                if (!properties.ManufacturerData.TryGetValue(Constants.ManufacturerId, out var o) || o is not byte[] payload) {
                    return;
                }

                var sample = RuuviTagUtilities.CreateSampleFromPayload(DateTimeOffset.Now, properties.RSSI, payload);
                channel!.Writer.TryWrite(sample);
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

            // Get the adapter from BlueZ.
            using (var adapter = await BlueZManager.GetAdapterAsync(_adapterName).ConfigureAwait(false))
            using (var @lock = new SemaphoreSlim(1, 1)) {

                // Registrations for devices that we are observing.
                var watchers = new Dictionary<string, IDisposable>(StringComparer.OrdinalIgnoreCase);

                // Adds a watcher for the specified device so that we can emit new samples when the
                // device properties change.
                async Task<bool> AddDeviceWatcher(HashtagChris.DotNetBlueZ.Device device, Device1Properties properties) {
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

                // Handler for when BlueZ detects a new device.
                adapter.DeviceFound += async (sender, args) => {
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
                            // This is not a RuuviTag.
                            disposeDevice = true;
                            return;
                        }

                        if (filter != null && !filter.Invoke(props.Address)) {
                            // We are not interested in this RuuviTag.
                            disposeDevice = true;
                            return;
                        }

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
                    await @lock.WaitAsync().ConfigureAwait(false);
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
            }
        }

    }
}
