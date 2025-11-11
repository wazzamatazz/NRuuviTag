using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace NRuuviTag.Listener.Windows;

/// <summary>
/// <see cref="IRuuviTagListener"/> that uses the Windows 10 SDK to listen for RuuviTag 
/// Bluetooth LE advertisements.
/// </summary>
public class WindowsSdkListener : RuuviTagListener {
    
    /// <summary>
    /// Creates a new <see cref="WindowsSdkListener"/> instance.
    /// </summary>
    /// <param name="options">
    ///   The listener options.
    /// </param>
    /// <param name="deviceLookup">
    ///   The device lookup service.
    /// </param>
    public WindowsSdkListener(WindowsSdkListenerOptions options, IDeviceResolver? deviceLookup = null) 
        : base(options, deviceLookup) {}
    

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<RuuviTagSample> ListenAsync(
        [EnumeratorCancellation]
        CancellationToken cancellationToken
    ) {
        var channel = Channel.CreateUnbounded<BluetoothLEAdvertisementReceivedEventArgs>(new UnboundedChannelOptions() { 
            SingleReader = true,
            SingleWriter = false
        });

        var watcher = new BluetoothLEAdvertisementWatcher();

        var manufacturerDataFilter = new BluetoothLEManufacturerData() {
            CompanyId = Constants.ManufacturerId
        };

        watcher.AdvertisementFilter.Advertisement.ManufacturerData.Add(manufacturerDataFilter);
        watcher.Received += (_, args) => {
            var device = DeviceResolver.GetDeviceInformation(RuuviTagUtilities.ConvertMacAddressToString(args.BluetoothAddress));
            if (device is null && KnownDevicesOnly) {
                return;
            }

            channel.Writer.TryWrite(args);
        };

        var buffer = ArrayPool<byte>.Shared.Rent(255);
        try {
            watcher.Start();

            await foreach (var args in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) {
                foreach (var manufacturerData in args.Advertisement.ManufacturerData) {
                    var macAddress = RuuviTagUtilities.ConvertMacAddressToString(args.BluetoothAddress);
                    var device = DeviceResolver.GetDeviceInformation(macAddress);
                    if (device is null && KnownDevicesOnly) {
                        // We are no longer interested in this device - it has probably been removed
                        // from the list of known devices since we started scanning.
                        continue;
                    }
                    
                    if (manufacturerData.Data.Length > buffer.Length) {
                        ArrayPool<byte>.Shared.Return(buffer);
                        buffer = ArrayPool<byte>.Shared.Rent((int) manufacturerData.Data.Length);
                    }
                        
                    using (var reader = DataReader.FromBuffer(manufacturerData.Data)) {
                        reader.ReadBytes(buffer);
                    }
                    
                    if (!EnableDataFormat6 && manufacturerData.Data.Length > 0 && buffer[0] == Constants.DataFormat6) {
                        // Ignore data format 6 if configured to do so.
                        continue;
                    }

                    if (!RuuviTagUtilities.TryParsePayload(new Span<byte>(buffer, 0, (int) manufacturerData.Data.Length), out var sample)) {
                        continue;
                    }
                    
                    // Create the full sample from the parsed payload.
                    var fullSample = new RuuviTagSample(device?.DeviceId, args.Timestamp, args.RawSignalStrengthInDBm, sample) {
                        MacAddress = sample.DataFormat switch {
                            // If the payload uses data format 6 then the MAC address in the payload will
                            // only contain the lower 3 bytes of the address. We will replace this with
                            // the full MAC address from the advertisement.
                            Constants.DataFormat6 => macAddress,
                            _ => sample.MacAddress
                        }
                    };
                        
                    yield return fullSample;
                }
            }
        }
        finally {
            ArrayPool<byte>.Shared.Return(buffer);
            watcher.Stop();
            channel.Writer.TryComplete();
        }

    }

}
