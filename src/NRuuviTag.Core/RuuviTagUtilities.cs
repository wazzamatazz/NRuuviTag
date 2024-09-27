using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NRuuviTag {

    /// <summary>
    /// Utility methods.
    /// </summary>
    public static partial class RuuviTagUtilities {

        /// <summary>
        /// Gets raw bytes for an instrument reading from the specified payload data.
        /// </summary>
        /// <param name="data">
        ///   The RuuviTag payload.
        /// </param>
        /// <param name="count">
        ///   The number of bytes to return.
        /// </param>
        /// <param name="offset">
        ///   The offset from the start of the <paramref name="data"/> for the first byte to 
        ///   return.
        /// </param>
        /// <param name="reverseIfLittleEndian">
        ///   RuuviTag payloads use Big-endian byte ordering for instrument readings. When <paramref name="reverseIfLittleEndian"/> 
        ///   is <see langword="true"/>, the order of the selected bytes will be reversed if this 
        ///   is a Little-endian system.
        /// </param>
        /// <returns>
        ///   The requested bytes.
        /// </returns>
        private static Span<byte> GetRawInstrumentBytes(Span<byte> data, int count, int offset, bool reverseIfLittleEndian = true) {
            var slice = data.Slice(offset, count);

            if (reverseIfLittleEndian && BitConverter.IsLittleEndian) {
                slice.Reverse();
            }

            return slice;
        }


        /// <summary>
        /// Creates a new <see cref="RuuviTagSample"/> from a <paramref name="payload"/> that uses 
        /// RuuviTag's RAWv2 format (data format 5).
        /// </summary>
        /// <param name="timestamp">
        ///   The timestamp for the sample.
        /// </param>
        /// <param name="signalStrength">
        ///   The signal strength for the sample, in dBm.
        /// </param>
        /// <param name="payload">
        ///   The 24-byte payload received from the RuuviTag.
        /// </param>
        /// <returns>
        ///   A new <see cref="RuuviTagSample"/> object.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="payload"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="payload"/> is not exactly 24 bytes in length.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="payload"/> does not contain RAWv2 data.
        /// </exception>
        /// <remarks>
        ///   See https://docs.ruuvi.com/communication/bluetooth-advertisements for more 
        ///   information about available data formats.
        /// </remarks>
        /// <seealso cref="Constants.DataFormatRawV2"/>
        public static RuuviTagSample CreateSampleFromRawV2Payload(DateTimeOffset timestamp, double signalStrength, Span<byte> payload) {
            if (payload == null) {
                throw new ArgumentNullException(nameof(payload));
            }
            if (payload.Length < 24) {
                throw new ArgumentException(Resources.Error_UnexpectedPayloadLength, nameof(payload));
            }
            if (payload[0] != Constants.DataFormatRawV2) {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.Error_UnexpectedDataFormat, Constants.DataFormatRawV2, payload[0]), nameof(payload));
            }

            byte[]? buffer = null;

            if (BitConverter.IsLittleEndian) {
                // We will be modifying the byte order of various parts of the payload. We'll copy
                // the payload to a buffer and work with that instead so that we don't modify the
                // original span.
                buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(24);
                payload.Slice(0, 24).CopyTo(buffer);
                payload = buffer;
            }

            if (payload.Length > 24) {
                payload = payload.Slice(0, 24);
            }

            try {
                var result = new RuuviTagSample() {
                    Timestamp = timestamp,
                    SignalStrength = signalStrength,
                    DataFormat = Constants.DataFormatRawV2
                };

                var tempRaw = BitConverter.ToInt16(GetRawInstrumentBytes(payload, 2, 1));
                result.Temperature = tempRaw == short.MaxValue
                    ? null
                    : Math.Round(tempRaw * 0.005, 3);

                var humidityRaw = BitConverter.ToUInt16(GetRawInstrumentBytes(payload, 2, 3));
                result.Humidity = humidityRaw == ushort.MaxValue
                    ? null
                    : Math.Round(humidityRaw * 0.0025, 4);

                var pressureRaw = BitConverter.ToUInt16(GetRawInstrumentBytes(payload, 2, 5));
                result.Pressure = pressureRaw == ushort.MaxValue
                    ? null
                    : Math.Round(((double) pressureRaw + 50000) / 100, 2);

                var accelXRaw = BitConverter.ToInt16(GetRawInstrumentBytes(payload, 2, 7));
                result.AccelerationX = accelXRaw == short.MaxValue
                    ? null
                    : Math.Round(accelXRaw * 0.001, 3);

                var accelYRaw = BitConverter.ToInt16(GetRawInstrumentBytes(payload, 2, 9));
                result.AccelerationY = accelYRaw == short.MaxValue
                    ? null
                    : Math.Round(accelYRaw * 0.001, 3);

                var accelZRaw = BitConverter.ToInt16(GetRawInstrumentBytes(payload, 2, 11));
                result.AccelerationZ = accelZRaw == short.MaxValue
                    ? null
                    : Math.Round(accelZRaw * 0.001, 3);

                var powerInfoRaw = BitConverter.ToUInt16(GetRawInstrumentBytes(payload, 2, 13));
                var voltageRaw = powerInfoRaw / 32; // 11 most-significant bits are voltage
                var txPowerRaw = powerInfoRaw % 32; // 5 least-significant bits are TX power

                result.BatteryVoltage = voltageRaw == 2047
                    ? null
                    : Math.Round((voltageRaw + 1600) * 0.001, 3);

                result.TxPower = txPowerRaw == 31
                    ? null
                    : -40 + (2 * txPowerRaw);

                var movementCounterRaw = payload[15];
                result.MovementCounter = movementCounterRaw == byte.MaxValue
                    ? null
                    : movementCounterRaw;

                var measurementSequenceRaw = BitConverter.ToUInt16(GetRawInstrumentBytes(payload, 2, 16));
                result.MeasurementSequence = measurementSequenceRaw == ushort.MaxValue
                    ? null
                    : measurementSequenceRaw;

                // MAC address is always Big-endian, so no need to reverse the byte order if this is a
                // Little-endian system.
                var macAddressRaw = GetRawInstrumentBytes(payload, 6, 18, false);
                result.MacAddress = ConvertMacAddressBytesToString(macAddressRaw, 0);

                return result;
            }
            finally {
                if (buffer != null) {
                    System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }


        /// <summary>
        /// Creates a new <see cref="RuuviTagSample"/> from a RuuviTag advertisement <paramref name="payload"/>.
        /// </summary>
        /// <param name="timestamp">
        ///   The timestamp for the sample.
        /// </param>
        /// <param name="signalStrength">
        ///   The signal strength for the sample, in dBm.
        /// </param>
        /// <param name="payload">
        ///   The 24-byte payload received from the RuuviTag.
        /// </param>
        /// <returns>
        ///   A new <see cref="RuuviTagSample"/> object.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="payload"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="payload"/> is not exactly 24 bytes in length.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="payload"/> specifies a data format that is unknown.
        /// </exception>
        /// <remarks>
        ///   The format is specified using the first byte in the <paramref name="payload"/>. See 
        ///   https://docs.ruuvi.com/communication/bluetooth-advertisements for more information 
        ///   about available data formats.
        /// </remarks>
        /// <seealso cref="Constants.DataFormatRawV1"/>
        /// <seealso cref="Constants.DataFormatUrl"/>
        /// <seealso cref="Constants.DataFormatRawV2"/>
        /// <seealso cref="Constants.DataFormatEncryptedEnvironmental"/>
        public static RuuviTagSample CreateSampleFromPayload(DateTimeOffset timestamp, double signalStrength, Span<byte> payload) {
            if (payload == null) {
                throw new ArgumentNullException(nameof(payload));
            }
            if (payload.Length < 24) {
                throw new ArgumentException(Resources.Error_UnexpectedPayloadLength, nameof(payload));
            }

            switch (payload[0]) {
                case Constants.DataFormatRawV2:
                    return CreateSampleFromRawV2Payload(timestamp, signalStrength, payload);
                default:
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.Error_UnknownDataFormat, payload[0]), nameof(payload));
            }
        }


        /// <summary>
        /// Converts a <see cref="ulong"/> MAC address to its <see cref="string"/> equivalent.
        /// </summary>
        /// <param name="address">
        ///   The <see cref="ulong"/> MAC address.
        /// </param>
        /// <returns>
        ///   The string representation of the MAC address.
        /// </returns>
        public static string ConvertMacAddressToString(ulong address) {
            // MAC address is 6 bytes long but a ulong is 8 bytes long so we need to rent 8 bytes.
            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(8);
            var span = buffer.AsSpan(0, 8);

            try {
                if (!BitConverter.TryWriteBytes(span, address)) {
                    return null!;
                }

                if (BitConverter.IsLittleEndian) {
                    // Reverse the byte order on Little-endian systems; MAC addresses always use
                    // Big-endian ordering.
                    span.Reverse();
                }

                // The two most-significant bytes in the sequence are always use to pad the 6-byte
                // MAC address into an 8-byte ulong.
                return ConvertMacAddressBytesToString(span, 2);
            }
            finally {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }


        /// <summary>
        /// Converts the specified MAC address bytes to their <see cref="string"/> equivalent.
        /// </summary>
        /// <param name="bytes">
        ///   The MAC address bytes.
        /// </param>
        /// <param name="offset">
        ///   The offset in the <paramref name="bytes"/> sequence where the MAC address starts.
        /// </param>
        /// <param name="reverse">
        ///   When <see langword="true"/>, the MAC address bytes end at the specified <paramref name="offset"/> 
        ///   instead of starting at that position.
        /// </param>
        /// <returns>
        ///   The string representation of the MAC address.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   The byte sequence is less than 6 bytes long starting from the provided <paramref name="offset"/>.
        /// </exception>
        /// <remarks>
        ///   The <paramref name="bytes"/> are assumed to already be in Big-endian order.
        /// </remarks>
        private static string ConvertMacAddressBytesToString(Span<byte> bytes, int offset) {
            const int MacAddressLength = 6;

            if (offset + MacAddressLength > bytes.Length) {
                throw new ArgumentOutOfRangeException(nameof(bytes), $"Sequence must contain at least {MacAddressLength} bytes starting from the specified offset position.");
            }

            var sb = new StringBuilder();

            for (var i = offset; i < offset + MacAddressLength; i++) {
                sb.Append(bytes[i].ToString("X2"));
                if (i < offset + MacAddressLength - 1) {
                    sb.Append(":");
                }
            }

            return sb.ToString();
        }


        /// <summary>
        /// Converts a <see cref="string"/> MAC address to its <see cref="ulong"/> equivalent.
        /// </summary>
        /// <param name="address">
        ///   The <see cref="string"/> MAC address.
        /// </param>
        /// <returns>
        ///   The <see cref="ulong"/> representation of the MAC address.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   <paramref name="address"/> is not a valid MAC address.
        /// </exception>
        /// <remarks>
        ///   The <paramref name="address"/> must be specified in the format <c>XX:XX:XX:XX:XX:XX:XX:XX</c> 
        ///   or <c>XX-XX-XX-XX-XX-XX-XX-XX</c>. Leading zero-bytes can be omitted e.g. 
        ///   <c>00:00:12:34:56:78:9A:BC</c> can be specified as <c>12:34:56:78:9A:BC</c>.
        /// </remarks>
        public static ulong ConvertMacAddressToUInt64(string address) {
            if (!TryConvertMacAddressToUInt64(address, out var numericAddress)) {
                throw new ArgumentOutOfRangeException(nameof(address));
            }

            return numericAddress;
        }


        /// <summary>
        /// Converts a <see cref="string"/> MAC address to its <see cref="ulong"/> equivalent.
        /// </summary>
        /// <param name="address">
        ///   The <see cref="string"/> MAC address.
        /// </param>
        /// <returns>
        ///   The <see cref="ulong"/> representation of the MAC address.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="address"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   <paramref name="address"/> is not a valid MAC address.
        /// </exception>
        /// <remarks>
        ///   The <paramref name="address"/> must be specified in the format <c>XX:XX:XX:XX:XX:XX:XX:XX</c> 
        ///   or <c>XX-XX-XX-XX-XX-XX-XX-XX</c>. Leading zero-bytes can be omitted e.g. 
        ///   <c>00:00:12:34:56:78:9A:BC</c> can be specified as <c>12:34:56:78:9A:BC</c>.
        /// </remarks>
        public static bool TryConvertMacAddressToUInt64(string address, out ulong numericAddress) {
            if (address == null) {
                numericAddress = 0;
                return false;
            }

            var m = s_macAddressMatcher.Match(address);
            if (!m.Success) {
                numericAddress = 0;
                return false;
            }

            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(8);
            try {
                if (BitConverter.IsLittleEndian) { 
                    var index = 0;
                    foreach (var b in m.Groups["byte"].Captures.Select(x => byte.Parse(x.Value, NumberStyles.HexNumber))) {
                        buffer[index++] = b;
                    }

                    while (index < 8) {
                        buffer[index++] = 0;
                    }
                }
                else {
                    var index = 8;
                    foreach (var b in m.Groups["byte"].Captures.Select(x => byte.Parse(x.Value, NumberStyles.HexNumber)).Reverse()) {
                        buffer[--index] = b;
                    }

                    while (index > 0) {
                        buffer[--index] = 0;
                    }
                }

                numericAddress = BitConverter.ToUInt64(buffer);
                return true;
            }
            finally {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }


        /// <summary>
        /// Matches a MAC address string.
        /// </summary>
        private static readonly Regex s_macAddressMatcher = new Regex(@"^(?<byte>[0-9a-f]{2})(?:(?::|-)(?<byte>[0-9a-f]{2})){0,7}$", RegexOptions.Singleline | RegexOptions.IgnoreCase);

    }
}
