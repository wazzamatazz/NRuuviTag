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

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<RuuviTagSample> ListenAsync(
        Func<string, bool>? filter,
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
            if (filter != null && !filter.Invoke(RuuviTagUtilities.ConvertMacAddressToString(args.BluetoothAddress))) {
                return;
            }

            channel.Writer.TryWrite(args);
        };

        var buffer = ArrayPool<byte>.Shared.Rent(255);
        try {
            watcher.Start();

            await foreach (var args in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) {
                foreach (var manufacturerData in args.Advertisement.ManufacturerData) {
                    if (manufacturerData.Data.Length > buffer.Length) {
                        ArrayPool<byte>.Shared.Return(buffer);
                        buffer = ArrayPool<byte>.Shared.Rent((int) manufacturerData.Data.Length);
                    }
                        
                    using (var reader = DataReader.FromBuffer(manufacturerData.Data)) {
                        reader.ReadBytes(buffer);
                    }

                    if (!RuuviTagUtilities.TryParsePayload(new Span<byte>(buffer, 0, (int) manufacturerData.Data.Length), out var sample)) {
                        continue;
                    }
                        
                    yield return new RuuviTagSample(args.Timestamp, args.RawSignalStrengthInDBm, sample);
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