using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NRuuviTag.Core.Tests {
    [TestClass]
    public class UtilitiesTests {

        [TestMethod]
        public void ShouldParseValidRawV2Payload() {

            // Payload taken from https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-5-rawv2#case-valid-data
            var payload = new byte[] { 
                0x05,
                0x12,
                0xFC,
                0x53,
                0x94,
                0xC3,
                0x7C,
                0x00,
                0x04,
                0xFF,
                0xFC,
                0x04,
                0x0C,
                0xAC,
                0x36,
                0x42,
                0x00,
                0xCD,
                0xCB,
                0xB8,
                0x33,
                0x4C,
                0x88,
                0x4F
            };

            var now = DateTimeOffset.Now;
            var signalStrength = -79;

            var sample = RuuviTagUtilities.CreateSampleFromPayload(now, signalStrength, payload);
            Assert.AreEqual(now, sample.Timestamp);
            Assert.AreEqual(signalStrength, sample.SignalStrength);

            // Expected values taken from https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-5-rawv2#case-valid-data

            Assert.AreEqual((byte) 5, sample.DataFormat);
            Assert.AreEqual(24.3, sample.Temperature);
            Assert.AreEqual(1000.44, sample.Pressure);
            Assert.AreEqual(53.49, sample.Humidity);
            Assert.AreEqual(0.004, sample.AccelerationX);
            Assert.AreEqual(-0.004, sample.AccelerationY);
            Assert.AreEqual(1.036, sample.AccelerationZ);
            Assert.AreEqual(4, sample.TxPower);
            Assert.AreEqual(2.977, sample.BatteryVoltage);
            Assert.AreEqual((byte) 66, sample.MovementCounter);
            Assert.AreEqual((ushort) 205, sample.MeasurementSequence);
            Assert.AreEqual("CB:B8:33:4C:88:4F", sample.MacAddress);
        }

    }
}
