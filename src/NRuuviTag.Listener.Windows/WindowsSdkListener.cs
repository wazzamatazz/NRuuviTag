using System;
using System.Buffers;
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
    protected override async Task RunAsync( CancellationToken cancellationToken) {
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
                    
                    if (manufacturerData.Data.Length > buffer.Length) {
                        ArrayPool<byte>.Shared.Return(buffer);
                        buffer = ArrayPool<byte>.Shared.Rent((int) manufacturerData.Data.Length);
                    }
                        
                    using (var reader = DataReader.FromBuffer(manufacturerData.Data)) {
                        reader.ReadBytes(buffer);
                    }

                    DataReceived(macAddress, args.RawSignalStrengthInDBm, new Span<byte>(buffer, 0, (int) manufacturerData.Data.Length));
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
