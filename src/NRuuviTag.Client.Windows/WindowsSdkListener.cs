using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace NRuuviTag.Client.Windows {

    /// <summary>
    /// Client that can be used to listen for Bluetooth LE advertisements from RuuviTags using the 
    /// Windows SDK.
    /// </summary>
    public class WindowsSdkListener : IRuuviTagListener {

        /// <summary>
        /// Converts a <see cref="ulong"/> RuuviTag MAC address to its string representation.
        /// </summary>
        /// <param name="address">
        ///   The <see cref="ulong"/> MAC address.
        /// </param>
        /// <returns>
        ///   The string representation of the MAC address.
        /// </returns>
        private static string GetMacAddressAsString(ulong address) {
            var bytes = BitConverter.GetBytes(address);
            return ToMacAddressString(BitConverter.IsLittleEndian
                ? bytes.Reverse()
                : bytes
            );
        }


        /// <summary>
        /// Converts a <see cref="ulong"/> RuuviTag MAC address to its string representation.
        /// </summary>
        /// <param name="address">
        ///   The bytes from the <see cref="ulong"/> MAC address.
        /// </param>
        /// <returns>
        ///   The string representation of the MAC address.
        /// </returns>
        private static string ToMacAddressString(IEnumerable<byte> bytes) {
            return string.Join(":", bytes.Select(x => x.ToString("X2")));
        }


        /// <inheritdoc/>
        public async IAsyncEnumerable<RuuviTagSample> ListenAsync(
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
                if (filter != null && !filter.Invoke(GetMacAddressAsString(args.BluetoothAddress))) {
                    return;
                }

                channel.Writer.TryWrite(args);
            };

            try {
                watcher.Start();

                await foreach (var args in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) {
                    foreach (var manufacturerData in args.Advertisement.ManufacturerData) {
                        var data = new byte[manufacturerData.Data.Length];
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
