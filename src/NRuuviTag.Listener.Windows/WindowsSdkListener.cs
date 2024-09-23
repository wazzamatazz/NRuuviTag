using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace NRuuviTag.Listener.Windows {

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
            watcher.Received += (sender, args) => { 
                if (filter != null && !filter.Invoke(RuuviTagUtilities.ConvertMacAddressToString(args.BluetoothAddress))) {
                    return;
                }

                channel.Writer.TryWrite(args);
            };

            try {
                watcher.Start();

                // Payload of a RuuviTag advertisement is 24 bytes long. Note that this does not
                // include standard advertisement header content such as the manufacturer ID.
                var payloadBuffer = new byte[24];

                await foreach (var args in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) {
                    foreach (var manufacturerData in args.Advertisement.ManufacturerData) {
                        // If the payload is not 24 bytes long, we need to create a new buffer of
                        // the correct size. This would only occur if e.g. a custom data format
                        // was being used or if Ruuvi changed the payload format in the future.
                        var data = manufacturerData.Data.Length == payloadBuffer.Length
                            ? payloadBuffer
                            : new byte[manufacturerData.Data.Length];

                        using (var reader = DataReader.FromBuffer(manufacturerData.Data)) {
                            reader.ReadBytes(data);
                        }

                        var sample = RuuviTagUtilities.CreateSampleFromPayload(args.Timestamp, args.RawSignalStrengthInDBm, data);
                        yield return sample;
                    }
                }
            }
            finally {
                watcher.Stop();
                channel.Writer.TryComplete();
            }

        }

    }
}
