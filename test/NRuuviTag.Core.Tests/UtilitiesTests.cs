using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NRuuviTag.Core.Tests;

[TestClass]
public class UtilitiesTests {

    // See https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-5-rawv2#case-valid-data
    private const string RawDataV2Valid = "0512FC5394C37C0004FFFC040CAC364200CDCBB8334C884F";

    // See https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-5-rawv2#case-maximum-values
    private const string RawDataV2ValidMaximum = "057FFFFFFEFFFE7FFF7FFF7FFFFFDEFEFFFECBB8334C884F";
        
    // See https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-5-rawv2#case-minimum-values
    private const string RawDataV2ValidMinimum = "058001000000008001800180010000000000CBB8334C884F";

    // See https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-5-rawv2#case-invalid-values
    private const string RawDataV2Invalid = "058000FFFFFFFF800080008000FFFFFFFFFFFFFFFFFFFFFF";
        
    // https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-e1#case-valid-data
    private const string ExtendedDataV1Valid   = "E1170C5668C79E0065007004BD11CA00C90A0213E0AC000000DECDEE100000000000CBB8334C884F";

    // https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-e1#case-maximum-values
    private const string ExtendedDataV1Maximum = "E17FFF9C40FFFE27102710271027109C40FAFADC28F0000000FFFFFE3F0000000000CBB8334C884F";
        
    // https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-e1#case-minimum-values
    private const string ExtendedDataV1Minimum = "E1800100000000000000000000000000000000000000000000000000000000000000CBB8334C884F";
        
    // https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-e1#case-invalid-values
    private const string ExtendedDataV1Invalid = "E18000FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF000000FFFFFFFE0000000000FFFFFFFFFFFF";


    private static readonly Dictionary<string, RuuviDataPayload> s_expectedPayloads = new Dictionary<string, RuuviDataPayload>() {
        [RawDataV2Valid] = new RuuviDataPayload {
            DataFormat = 5,
            Temperature = 24.3,
            Pressure = 1000.44,
            Humidity = 53.49,
            AccelerationX = 0.004,
            AccelerationY = -0.004,
            AccelerationZ = 1.036,
            TxPower = 4,
            BatteryVoltage = 2.977,
            MovementCounter = 66,
            MeasurementSequence = 205,
            MacAddress = "CB:B8:33:4C:88:4F"
        },
        [RawDataV2ValidMaximum] = new RuuviDataPayload {
            DataFormat = 5,
            Temperature = 163.835,
            Pressure = 1155.34,
            Humidity = 163.835,
            AccelerationX = 32.767,
            AccelerationY = 32.767,
            AccelerationZ = 32.767,
            TxPower = 20,
            BatteryVoltage = 3.646,
            MovementCounter = 254,
            MeasurementSequence = 65534,
            MacAddress = "CB:B8:33:4C:88:4F"
        },
        [RawDataV2ValidMinimum] = new RuuviDataPayload {
            DataFormat = 5,
            Temperature = -163.835,
            Pressure = 500d,
            Humidity = 0d,
            AccelerationX = -32.767,
            AccelerationY = -32.767,
            AccelerationZ = -32.767,
            TxPower = -40,
            BatteryVoltage = 1.6,
            MovementCounter = 0,
            MeasurementSequence = 0,
            MacAddress = "CB:B8:33:4C:88:4F"
        },
        [RawDataV2Invalid] = new RuuviDataPayload {
            DataFormat = 5
        },
        [ExtendedDataV1Valid] = new RuuviDataPayload {
            DataFormat = 0xE1,
            Temperature = 29.5,
            Pressure = 1011.02,
            Humidity = 55.3,
            PM10 = 10.1,
            PM25 = 11.2,
            PM40 = 121.3,
            PM100 = 455.4,
            CO2 = 201,
            VOC = 20,
            NOX = 4,
            Calibrated = true,
            Luminosity = 13027,
            MeasurementSequence = 14601710u,
            MacAddress = "CB:B8:33:4C:88:4F"
        },
        [ExtendedDataV1Maximum] = new RuuviDataPayload {
            DataFormat = 0xE1,
            Temperature = 163.835,
            Pressure = 1155.34,
            Humidity = 100,
            PM10 = 1000,
            PM25 = 1000,
            PM40 = 1000,
            PM100 = 1000,
            CO2 = 40000,
            VOC = 500,
            NOX = 500,
            Calibrated = false,
            Luminosity = 144284,
            MeasurementSequence = 16777214u,
            MacAddress = "CB:B8:33:4C:88:4F"
        },
        [ExtendedDataV1Minimum] = new RuuviDataPayload {
            DataFormat = 0xE1,
            Temperature = -163.835,
            Pressure = 500,
            Humidity = 0,
            PM10 = 0,
            PM25 = 0,
            PM40 = 0,
            PM100 = 0,
            CO2 = 0,
            VOC = 0,
            NOX = 0,
            Calibrated = true,
            Luminosity = 0,
            MeasurementSequence = 0u,
            MacAddress = "CB:B8:33:4C:88:4F"
        },
        [ExtendedDataV1Invalid] = new RuuviDataPayload {
            DataFormat = 0xE1,
            Calibrated = true
        }
    };
        

    [TestMethod]
    [DataRow(RawDataV2Valid)]
    [DataRow(RawDataV2ValidMaximum)]
    [DataRow(RawDataV2ValidMinimum)]
    [DataRow(RawDataV2Invalid)]
    [DataRow(ExtendedDataV1Valid)]
    [DataRow(ExtendedDataV1Maximum)]
    [DataRow(ExtendedDataV1Minimum)]
    [DataRow(ExtendedDataV1Invalid)]
    public void ShouldParsePayload(string payloadString) {
        var payload = Convert.FromHexString(payloadString);
        var sample = RuuviTagUtilities.ParsePayload(payload);

        // See notes for payload strings for expected values.
        Assert.AreEqual(s_expectedPayloads[payloadString], sample);
    }
        
}