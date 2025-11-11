using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NRuuviTag;

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

    
    private static double? GetTemperatureFromFromRawBytes(Span<byte> buffer, int offset) {
        ReadOnlySpan<byte> outOfRange = [0x00, 0x80];
        var raw = GetRawInstrumentBytes(buffer, 2, offset);
        return raw.SequenceEqual(outOfRange)
            ? null
            : Math.Round(BitConverter.ToInt16(raw) * 0.005, 3);
    }


    private static double? GetHumidityFromRawBytes(Span<byte> buffer, int offset) {
        ReadOnlySpan<byte> outOfRange = [0xFF, 0xFF];
        var raw = GetRawInstrumentBytes(buffer, 2, offset);
        return raw.SequenceEqual(outOfRange)
            ? null
            : Math.Round(BitConverter.ToUInt16(raw) * 0.0025, 4);
    }


    private static double? GetPressureFromRawBytes(Span<byte> buffer, int offset) {
        ReadOnlySpan<byte> outOfRange = [0xFF, 0xFF];
        var raw = GetRawInstrumentBytes(buffer, 2, offset);
        return raw.SequenceEqual(outOfRange)
            ? null
            : Math.Round((BitConverter.ToUInt16(raw) + 50_000d) / 100, 2);
    }
    
    
    private static double? GetAccelerationFromRawBytes(Span<byte> buffer, int offset) {
        ReadOnlySpan<byte> outOfRange = [0x00, 0x80];
        var raw = GetRawInstrumentBytes(buffer, 2, offset);
        return raw.SequenceEqual(outOfRange)
            ? null
            : Math.Round(BitConverter.ToInt16(raw) * 0.001, 3);
    }
    
    
    private static double? GetPMFromRawBytes(Span<byte> buffer, int offset) {
        var raw = BitConverter.ToUInt16(GetRawInstrumentBytes(buffer, 2, offset));
        return raw == ushort.MaxValue
            ? null
            : Math.Round(raw * 0.1, 1);
    }
    
    
    private static double? GetCO2FromRawBytes(Span<byte> buffer, int offset) {
        var raw = BitConverter.ToUInt16(GetRawInstrumentBytes(buffer, 2, offset));
        return raw == ushort.MaxValue
            ? null
            : raw;
    }
    
    
    private static ushort? GetVOCNOXIndexFromRawBytes(Span<byte> buffer, int offset, int flagByteOffset, byte flagMask) {
        var flags = buffer[flagByteOffset];
        var flagIsSet = (flags & flagMask) != 0;
        
        // Index is a 9-bit value consisting of the raw byte and the flag as the least-significant bit.

        var raw = (ushort) ((buffer[offset] << 1) | (flagIsSet ? 0b_0000_0001 : 0b_0000_0000));
        return raw == 511u // 0x1FF
            ? null
            : raw;
    }
    
    
    private static double? GetLuminosityFromRawBytes(Span<byte> buffer, int offset) {
        // Luminosity is a 3-byte value. We need to pad it to 4 bytes to convert it to a uint.
        Span<byte> padded = [0x00, ..buffer.Slice(offset, 3)];
        
        var raw = BitConverter.ToUInt32(GetRawInstrumentBytes(padded, 4, 0));
        return raw == 16_777_215u // 0xFFFFFF
            ? null
            : Math.Round(raw * 0.01, 2);
    }


    /// <summary>
    /// Extrapolates luminosity from a single byte using a logarithmic scale (Data Format 6).
    /// </summary>
    private static double? Get8BitLuminosityFromRawBytes(Span<byte> buffer, int offset) {
        var raw = buffer[offset];
        if (raw == byte.MaxValue) {
            return null;
        }

        var delta = Math.Log(65_535 + 1) / 254;
        var code = Math.Round(Math.Log(raw + 1) / delta);
        return Math.Exp(code * delta) - 1;
    }


    private static (double? BatteryVoltage, double? TxPower) GetPowerInfoFromRawBytes(Span<byte> buffer, int offset) {
        var raw = BitConverter.ToUInt16(GetRawInstrumentBytes(buffer, 2, offset));
        var voltageRaw = raw / 32; // 11 most-significant bits are voltage
        var txPowerRaw = raw % 32; // 5 least-significant bits are TX power

        double? batteryVoltage = voltageRaw == 2047
            ? null
            : Math.Round((voltageRaw + 1600) * 0.001, 3);

        double? txPower = txPowerRaw == 31
            ? null
            : -40 + (2 * txPowerRaw);
        
        return (batteryVoltage, txPower);
    }
    
    
    private static byte? GetMovementCounterFromRawBytes(Span<byte> buffer, int offset) {
        var raw = buffer[offset];
        return raw == byte.MaxValue
            ? null
            : raw;
    }
    
    
    private static byte? Get8BitMeasurementSequenceFromRawBytes(Span<byte> buffer, int offset) {
        return buffer[offset];
    }
    
    
    private static ushort? Get16BitMeasurementSequenceFromRawBytes(Span<byte> buffer, int offset) {
        var raw = BitConverter.ToUInt16(GetRawInstrumentBytes(buffer, 2, offset));
        return raw == ushort.MaxValue
            ? null
            : raw;
    }
    
    
    private static uint? Get24BitMeasurementSequenceFromRawBytes(Span<byte> buffer, int offset) {
        Span<byte> padded = [0x00, ..buffer.Slice(offset, 3)];
        
        var raw = BitConverter.ToUInt32(GetRawInstrumentBytes(padded, 4, 0));
        return raw == 16_777_215u // 0xFFFFFF
            ? null
            : raw;
    }
    
    
    private static string? GetMacAddressFromRawBytes(Span<byte> buffer, int offset) {
        ReadOnlySpan<byte> outOfRange = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];
        
        // MAC address is always Big-endian, so no need to reverse the byte order if this is a
        // Little-endian system.
        var raw = GetRawInstrumentBytes(buffer, 6, offset, reverseIfLittleEndian: false);
        
        return raw.SequenceEqual(outOfRange)
            ? null
            : ConvertMacAddressBytesToString(raw, 0);
    }


    private static string? Get24BitMacAddressFromRawBytes(Span<byte> buffer, int offset) {
        ReadOnlySpan<byte> outOfRange = [0xFF, 0xFF, 0xFF];

        // MAC address is always Big-endian, so no need to reverse the byte order if this is a
        // Little-endian system.
        var raw = GetRawInstrumentBytes(buffer, 3, offset, reverseIfLittleEndian: false);
        
        return raw.SequenceEqual(outOfRange)
            ? null
            : ConvertMacAddressBytesToString(raw, 0);
    }
    

    /// <summary>
    /// Creates a new <see cref="RuuviDataPayload"/> from a <paramref name="payload"/> that uses 
    /// Ruuvi's RAWv2 format (data format 5).
    /// </summary>
    /// <param name="payload">
    ///   The 24-byte payload received from the device.
    /// </param>
    /// <returns>
    ///   A new <see cref="RuuviDataPayload"/> object.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="payload"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="payload"/> is less than 24 bytes in length.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="payload"/> does not contain RAWv2 data.
    /// </exception>
    /// <remarks>
    ///   See https://docs.ruuvi.com/communication/bluetooth-advertisements for more 
    ///   information about available data formats.
    /// </remarks>
    /// <seealso cref="Constants.DataFormatRawV2"/>
    public static RuuviDataPayload ParseRawV2Payload(Span<byte> payload) {
        if (payload.Length < 24) {
            throw new ArgumentException(Resources.Error_UnexpectedPayloadLength, nameof(payload));
        }

        if (payload[0] != Constants.DataFormatRawV2) {
            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.Error_UnexpectedDataFormat, Constants.DataFormatRawV2, payload[0]), nameof(payload));
        }

        Span<byte> buffer = stackalloc byte[24];
        payload[..24].CopyTo(buffer);
        
        var temperature = GetTemperatureFromFromRawBytes(buffer, 1);
        var humidity = GetHumidityFromRawBytes(buffer, 3);
        var pressure = GetPressureFromRawBytes(buffer, 5);
        var accelerationX = GetAccelerationFromRawBytes(buffer, 7);
        var accelerationY = GetAccelerationFromRawBytes(buffer, 9);
        var accelerationZ = GetAccelerationFromRawBytes(buffer, 11);
        var (batteryVoltage, txPower) = GetPowerInfoFromRawBytes(buffer, 13);
        var movementCounter = GetMovementCounterFromRawBytes(buffer, 15);
        var measurementSequence = Get16BitMeasurementSequenceFromRawBytes(buffer, 16);
        var macAddress = GetMacAddressFromRawBytes(buffer, 18);

        return new RuuviDataPayload() {
            DataFormat = Constants.DataFormatRawV2,
            Temperature = temperature,
            Humidity = humidity,
            Pressure = pressure,
            AccelerationX = accelerationX,
            AccelerationY = accelerationY,
            AccelerationZ = accelerationZ,
            BatteryVoltage = batteryVoltage,
            TxPower = txPower,
            MovementCounter = movementCounter,
            MeasurementSequence = measurementSequence,
            MacAddress = macAddress
        };
    }
    
    
    /// <summary>
    /// Creates a new <see cref="RuuviDataPayload"/> from a <paramref name="payload"/> that uses 
    /// Ruuvi's data format 6.
    /// </summary>
    /// <param name="payload">
    ///   The 20-byte payload received from the device.
    /// </param>
    /// <returns>
    ///   A new <see cref="RuuviDataPayload"/> object.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="payload"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="payload"/> is less than 20 bytes in length.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="payload"/> does not contain format 6 data.
    /// </exception>
    /// <remarks>
    ///   See https://docs.ruuvi.com/communication/bluetooth-advertisements for more 
    ///   information about available data formats.
    /// </remarks>
    /// <seealso cref="Constants.DataFormat6"/>
    public static RuuviDataPayload ParseDataFormat6Payload(Span<byte> payload) {
        if (payload.Length < 20) {
            throw new ArgumentException(Resources.Error_UnexpectedPayloadLength, nameof(payload));
        }

        if (payload[0] != Constants.DataFormat6) {
            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.Error_UnexpectedDataFormat, Constants.DataFormat6, payload[0]), nameof(payload));
        }
        
        Span<byte> buffer = stackalloc byte[20];
        payload[..20].CopyTo(buffer);
        
        var temperature = GetTemperatureFromFromRawBytes(buffer, 1);
        var humidity = GetHumidityFromRawBytes(buffer, 3);
        var pressure = GetPressureFromRawBytes(buffer, 5);
        var pm25 = GetPMFromRawBytes(buffer, 7);
        var co2 = GetCO2FromRawBytes(buffer, 9);
        var calibrated = (buffer[16] & 0b_0000_0001) == 0;
        var vocIndex = GetVOCNOXIndexFromRawBytes(buffer, 11, 16, 0b_0100_0000);
        var noxIndex = GetVOCNOXIndexFromRawBytes(buffer, 12, 16, 0b_1000_0000);
        var luminosity = Get8BitLuminosityFromRawBytes(buffer, 13);
        // 14: reserved
        var measurementSequence = Get8BitMeasurementSequenceFromRawBytes(buffer, 15);
        // 16: flags
        var macAddress = Get24BitMacAddressFromRawBytes(buffer, 17);

        return new RuuviDataPayload() {
            DataFormat = Constants.DataFormat6,
            Calibrated = calibrated,
            Temperature = temperature,
            Humidity = humidity,
            Pressure = pressure,
            PM25 = pm25,
            CO2 = co2,
            VOC = vocIndex,
            NOX = noxIndex,
            Luminosity = luminosity,
            MeasurementSequence = measurementSequence,
            MacAddress = macAddress
        };
    }


    /// <summary>
    /// Creates a new <see cref="RuuviDataPayload"/> from a <paramref name="payload"/> that uses 
    /// Ruuvi's Extended v1 format (data format E1).
    /// </summary>
    /// <param name="payload">
    ///   The 40-byte payload received from the device.
    /// </param>
    /// <returns>
    ///   A new <see cref="RuuviDataPayload"/> object.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="payload"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="payload"/> is less than 40 bytes in length.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="payload"/> does not contain Extended v1 data.
    /// </exception>
    /// <remarks>
    ///   See https://docs.ruuvi.com/communication/bluetooth-advertisements for more 
    ///   information about available data formats.
    /// </remarks>
    /// <seealso cref="Constants.DataFormatExtendedV1"/>
    public static RuuviDataPayload ParseExtendedV1Payload(Span<byte> payload) {
        if (payload.Length < 40) {
            throw new ArgumentException(Resources.Error_UnexpectedPayloadLength, nameof(payload));
        }

        if (payload[0] != Constants.DataFormatExtendedV1) {
            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.Error_UnexpectedDataFormat, Constants.DataFormatExtendedV1, payload[0]), nameof(payload));
        }
        
        Span<byte> buffer = stackalloc byte[40];
        payload[..40].CopyTo(buffer);
        
        var temperature = GetTemperatureFromFromRawBytes(buffer, 1);
        var humidity = GetHumidityFromRawBytes(buffer, 3);
        var pressure = GetPressureFromRawBytes(buffer, 5);
        var pm10 = GetPMFromRawBytes(buffer, 7);
        var pm25 = GetPMFromRawBytes(buffer, 9);
        var pm40 = GetPMFromRawBytes(buffer, 11);
        var pm100 = GetPMFromRawBytes(buffer, 13);
        var co2 = GetCO2FromRawBytes(buffer, 15);
        var calibrated = (buffer[28] & 0b_0000_0001) == 0;
        var vocIndex = GetVOCNOXIndexFromRawBytes(buffer, 17, 28, 0b_0100_0000);
        var noxIndex = GetVOCNOXIndexFromRawBytes(buffer, 18, 28, 0b_1000_0000);
        var luminosity = GetLuminosityFromRawBytes(buffer, 19);
        var measurementSequence = Get24BitMeasurementSequenceFromRawBytes(buffer, 25);
        var macAddress = GetMacAddressFromRawBytes(buffer, 34);

        return new RuuviDataPayload() {
            DataFormat = Constants.DataFormatExtendedV1,
            Calibrated = calibrated,
            Temperature = temperature,
            Humidity = humidity,
            Pressure = pressure,
            PM10 = pm10,
            PM25 = pm25,
            PM40 = pm40,
            PM100 = pm100,
            CO2 = co2,
            VOC = vocIndex,
            NOX = noxIndex,
            Luminosity = luminosity,
            MeasurementSequence = measurementSequence,
            MacAddress = macAddress
        };
    }
    
    
    /// <summary>
    /// Creates a new <see cref="RuuviDataPayload"/> from a Ruuvi advertisement <paramref name="payload"/>.
    /// </summary>
    /// <param name="payload">
    ///   The payload received from the Ruuvi device.
    /// </param>
    /// <returns>
    ///   A new <see cref="RuuviDataPayload"/> object.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///   <paramref name="payload"/> has an unexpected length.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="payload"/> specifies a data format that is unknown.
    /// </exception>
    /// <remarks>
    ///   The format is specified using the first byte in the <paramref name="payload"/>. See 
    ///   https://docs.ruuvi.com/communication/bluetooth-advertisements for more information 
    ///   about available data formats.
    /// </remarks>
    /// <seealso cref="Constants.DataFormatRawV2"/>
    /// <seealso cref="Constants.DataFormat6"/>
    /// <seealso cref="Constants.DataFormatExtendedV1"/>
    public static RuuviDataPayload ParsePayload(Span<byte> payload) {
        if (payload.Length == 0) {
            // No data format specified.
            throw new ArgumentException(Resources.Error_UnexpectedPayloadLength, nameof(payload));
        }
        
        return payload[0] switch {
            Constants.DataFormatRawV2 => ParseRawV2Payload(payload),
            Constants.DataFormat6 => ParseDataFormat6Payload(payload),
            Constants.DataFormatExtendedV1 => ParseExtendedV1Payload(payload),
            _ => throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.Error_UnknownDataFormat, payload[0]), nameof(payload))
        };
    }
    
    
    /// <summary>
    /// Tries to create a new <see cref="RuuviDataPayload"/> from a Ruuvi advertisement <paramref name="payload"/>.
    /// </summary>
    /// <param name="payload">
    ///   The payload received from the Ruuvi device.
    /// </param>
    /// <param name="sample">
    ///   The resulting <see cref="RuuviDataPayload"/> if the method returns <see langword="true"/>; otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    ///   <see langword="true"/> if the payload was successfully parsed; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///   The format is specified using the first byte in the <paramref name="payload"/>. See 
    ///   https://docs.ruuvi.com/communication/bluetooth-advertisements for more information 
    ///   about available data formats.
    /// </remarks>
    /// <seealso cref="Constants.DataFormatRawV2"/>
    /// <seealso cref="Constants.DataFormat6"/>
    /// <seealso cref="Constants.DataFormatExtendedV1"/>
    public static bool TryParsePayload(Span<byte> payload, [NotNullWhen(true)] out RuuviDataPayload? sample) {
        sample = null;
        if (payload.Length == 0) {
            // No data format specified.
            return false;
        }
        
        var dataFormat = payload[0];
        var isKnownFormat = dataFormat switch {
            Constants.DataFormatRawV2 => true,
            Constants.DataFormat6 => true,
            Constants.DataFormatExtendedV1 => true,
            _ => false
        };
        
        if (!isKnownFormat) {
            return false;
        }
        
        try {
            sample = ParsePayload(payload);
            return true;
        }
        catch {
            sample = null!;
            return false;
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
        // MAC address is 6 bytes long but a ulong is 8 bytes long so we need 8 bytes.
        Span<byte> buffer = stackalloc byte[8];

        if (!BitConverter.TryWriteBytes(buffer, address)) {
            return null!;
        }

        if (BitConverter.IsLittleEndian) {
            // Reverse the byte order on Little-endian systems; MAC addresses always use
            // Big-endian ordering.
            buffer.Reverse();
        }

        // The two most-significant bytes in the sequence are always use to pad the 6-byte
        // MAC address into an 8-byte ulong.
        return ConvertMacAddressBytesToString(buffer, 2);
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
    /// <returns>
    ///   The string representation of the MAC address.
    /// </returns>
    /// <remarks>
    ///   The <paramref name="bytes"/> are assumed to already be in Big-endian order.
    /// </remarks>
    private static string ConvertMacAddressBytesToString(Span<byte> bytes, int offset) {
        const int maxMacAddressLength = 6;
        var macAddressLength = bytes.Length - offset;
        if (macAddressLength > maxMacAddressLength) {
            macAddressLength = maxMacAddressLength;
        }
        
        if (macAddressLength <= 0) {
            return string.Empty;
        }

        var sb = new StringBuilder();

        for (var i = offset; i < offset + macAddressLength; i++) {
            sb.Append(bytes[i].ToString("X2"));
            if (i < offset + macAddressLength - 1) {
                sb.Append(':');
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
        return !TryConvertMacAddressToUInt64(address, out var numericAddress) 
            ? throw new ArgumentOutOfRangeException(nameof(address)) 
            : numericAddress;
    }


    /// <summary>
    /// Converts a <see cref="string"/> MAC address to its <see cref="ulong"/> equivalent.
    /// </summary>
    /// <param name="address">
    ///   The <see cref="string"/> MAC address.
    /// </param>
    /// <param name="numericAddress">
    ///   The <see cref="ulong"/> representation of the MAC address.
    /// </param>
    /// <returns>
    ///   <see langword="true"/> if the conversion succeeded; otherwise, <see langword="false"/>.
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

        var m = MacAddressMatcher().Match(address);
        if (!m.Success) {
            numericAddress = 0;
            return false;
        }

        Span<byte> buffer = stackalloc byte[8];
        
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
    
    
    /// <summary>
    /// Matches a MAC address string.
    /// </summary>
    [GeneratedRegex(@"^(?<byte>[0-9a-f]{2})(?:(?::|-)(?<byte>[0-9a-f]{2})){0,7}$", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex MacAddressMatcher();
    
}
