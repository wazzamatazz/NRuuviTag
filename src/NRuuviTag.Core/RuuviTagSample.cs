using System;

namespace NRuuviTag {

    /// <summary>
    /// Describes a sample received from a RuuviTag sensor.
    /// </summary>
    /// <remarks>
    ///   See https://docs.ruuvi.com/communication/bluetooth-advertisements for details about 
    ///   Bluetooth LE advertisements.
    /// </remarks>
    /// <seealso cref="RuuviTagUtilities.CreateSampleFromPayload"/>
    public class RuuviTagSample {

        /// <summary>
        /// Sample time.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Signal strength (dBm).
        /// </summary>
        public double? SignalStrength { get; set; }

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
        /// Battery voltage (V).
        /// </summary>
        public double? BatteryVoltage { get; set; }

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


        /// <summary>
        /// Creates a copy of an existing <see cref="RuuviTagSample"/>.
        /// </summary>
        /// <param name="sample">
        ///   The sample to copy.
        /// </param>
        /// <returns>
        ///   A copy of <paramref name="sample"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="sample"/> is <see langword="null"/>.
        /// </exception>
        public static RuuviTagSample FromExisting(RuuviTagSample sample) {
            if (sample == null) {
                throw new ArgumentNullException(nameof(sample));
            }

            return new RuuviTagSample() { 
                AccelerationX = sample.AccelerationX,
                AccelerationY = sample.AccelerationY,
                AccelerationZ = sample.AccelerationZ,
                BatteryVoltage = sample.BatteryVoltage,
                DataFormat = sample.DataFormat,
                Humidity = sample.Humidity,
                MacAddress = sample.MacAddress,
                MeasurementSequence = sample.MeasurementSequence,
                MovementCounter = sample.MovementCounter,
                Pressure = sample.Pressure,
                SignalStrength = sample.SignalStrength,
                Temperature = sample.Temperature,
                Timestamp = sample.Timestamp,
                TxPower = sample.TxPower
            };
        }

    }
}
