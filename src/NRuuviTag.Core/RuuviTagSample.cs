using System;
using System.Globalization;
using System.Linq;

namespace NRuuviTag {

    /// <summary>
    /// Describes a sample retrieved from a RuuviTag sensor.
    /// </summary>
    public class RuuviTagSample {

        /// <summary>
        /// Sample time.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Signal strength (dBm).
        /// </summary>
        public double SignalStrength { get; set; }

        /// <summary>
        /// Payload data format (see https://docs.ruuvi.com/communication/bluetooth-advertisements).
        /// </summary>
        public byte DataFormat { get; set; }

        /// <summary>
        /// Temperature (deg C).
        /// </summary>
        public double? Temperature { get; set; }

        /// <summary>
        /// Humidity (%).
        /// </summary>
        public double? Humidity { get; set; }

        /// <summary>
        /// Pressure (hPa).
        /// </summary>
        public double? Pressure { get; set; }

        /// <summary>
        /// X-acceleration (g).
        /// </summary>
        public double? AccelerationX { get; set; }

        /// <summary>
        /// Y-acceleration (g).
        /// </summary>
        public double? AccelerationY { get; set; }

        /// <summary>
        /// Z-acceleration (g).
        /// </summary>
        public double? AccelerationZ { get; set; }

        /// <summary>
        /// Voltage (V).
        /// </summary>
        public double? Voltage { get; set; }

        /// <summary>
        /// TX power (dBm).
        /// </summary>
        public double? TxPower { get; set; }

        /// <summary>
        /// Movement counter (counts).
        /// </summary>
        public byte? MovementCounter { get; set; }
        
        /// <summary>
        /// Measurement sequence.
        /// </summary>
        public ushort? MeasurementSequence { get; set; }

        /// <summary>
        /// MAC address of device.
        /// </summary>
        public string? MacAddress { get; set; }

    }
}
