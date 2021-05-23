using System;
using System.Globalization;
using System.Linq;

namespace NRuuviTag {

    /// <summary>
    /// Utility methods.
    /// </summary>
    public static class RuuviTagUtilities {

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
        private static byte[] GetRawInstrumentBytes(byte[] data, int count, int offset, bool reverseIfLittleEndian = true) {
            var result = new byte[count];
            for (var i = 0; i < count; i++) {
                result[i] = data[i + offset];
            }

            return reverseIfLittleEndian && BitConverter.IsLittleEndian
                ? result.Reverse().ToArray()
                : result;
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
        public static RuuviTagSample CreateSampleFromRawV2Payload(DateTimeOffset timestamp, double signalStrength, byte[] payload) {
            if (payload == null) {
                throw new ArgumentNullException(nameof(payload));
            }
            if (payload.Length != 24) {
                throw new ArgumentException(Resources.Error_UnexpectedPayloadLength, nameof(payload));
            }
            if (payload[0] != Constants.DataFormatRawV2) {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.Error_UnexpectedDataFormat, Constants.DataFormatRawV2, payload[0]), nameof(payload));
            }

            var result = new RuuviTagSample() {
                Timestamp = timestamp,
                SignalStrength = signalStrength,
                DataFormat = Constants.DataFormatRawV2
            };

            var tempRaw = BitConverter.ToInt16(GetRawInstrumentBytes(payload, 2, 1), 0);
            result.Temperature = tempRaw == short.MaxValue
                ? null
                : Math.Round(tempRaw * 0.005, 3);

            var humidityRaw = BitConverter.ToUInt16(GetRawInstrumentBytes(payload, 2, 3), 0);
            result.Humidity = humidityRaw == ushort.MaxValue
                ? null
                : Math.Round(humidityRaw * 0.0025, 4);

            var pressureRaw = BitConverter.ToUInt16(GetRawInstrumentBytes(payload, 2, 5), 0);
            result.Pressure = pressureRaw == ushort.MaxValue
                ? null
                : Math.Round(((double) pressureRaw + 50000) / 100, 2);

            var accelXRaw = BitConverter.ToInt16(GetRawInstrumentBytes(payload, 2, 7), 0);
            result.AccelerationX = accelXRaw == short.MaxValue
                ? null
                : Math.Round(accelXRaw * 0.001, 3);

            var accelYRaw = BitConverter.ToInt16(GetRawInstrumentBytes(payload, 2, 9), 0);
            result.AccelerationY = accelYRaw == short.MaxValue
                ? null
                : Math.Round(accelYRaw * 0.001, 3);

            var accelZRaw = BitConverter.ToInt16(GetRawInstrumentBytes(payload, 2, 11), 0);
            result.AccelerationZ = accelZRaw == short.MaxValue
                ? null
                : Math.Round(accelZRaw * 0.001, 3);

            var powerInfoRaw = BitConverter.ToUInt16(GetRawInstrumentBytes(payload, 2, 13), 0);
            var voltageRaw = powerInfoRaw / 32; // 11 most-significant bits are voltage
            var txPowerRaw = powerInfoRaw % 32; // 5 least-significant bits are TX power

            result.Voltage = voltageRaw == 2047
                ? null
                : Math.Round((voltageRaw + 1600) * 0.001, 3);

            result.TxPower = txPowerRaw == 31
                ? null
                : -40 + (2 * txPowerRaw);

            var movementCounterRaw = payload[15];
            result.MovementCounter = movementCounterRaw == byte.MaxValue
                ? null
                : movementCounterRaw;

            var measurementSequenceRaw = BitConverter.ToUInt16(GetRawInstrumentBytes(payload, 2, 16), 0);
            result.MeasurementSequence = measurementSequenceRaw == ushort.MaxValue
                ? null
                : measurementSequenceRaw;

            // MAC address is always Big-endian, so no need to reverse the byte order if this is a
            // Little-endian system.
            var macAddressRaw = GetRawInstrumentBytes(payload, 6, 18, false);
            result.MacAddress = string.Join(":", macAddressRaw.Select(x => x.ToString("X2")));

            return result;
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
        public static RuuviTagSample CreateSampleFromPayload(DateTimeOffset timestamp, double signalStrength, byte[] payload) {
            if (payload == null) {
                throw new ArgumentNullException(nameof(payload));
            }
            if (payload.Length != 24) {
                throw new ArgumentException(Resources.Error_UnexpectedPayloadLength, nameof(payload));
            }

            switch (payload[0]) {
                case Constants.DataFormatRawV2:
                    return CreateSampleFromRawV2Payload(timestamp, signalStrength, payload);
                default:
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.Error_UnknownDataFormat, payload[0]), nameof(payload));
            }
        }

    }
}
