using System;

namespace NRuuviTag {

    /// <summary>
    /// Extends <see cref="RuuviTagSample"/> to include a device display name.
    /// </summary>
    public class RuuviTagSampleWithDisplayName : RuuviTagSample {

        /// <summary>
        /// The display name for the device that emitted the sample.
        /// </summary>
        public string? DisplayName { get; set; }


        /// <summary>
        /// Creates a new <see cref="RuuviTagSampleWithDisplayName"/> from an existing sample.
        /// </summary>
        /// <param name="sample">
        ///   The sample.
        /// </param>
        /// <param name="displayName">
        ///   The display name.
        /// </param>
        /// <returns>
        ///   A new <see cref="RuuviTagSampleWithDisplayName"/> object.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="sample"/> is <see langword="null"/>.
        /// </exception>
        public static RuuviTagSampleWithDisplayName Create(RuuviTagSample sample, string? displayName) {
            if (sample == null) {
                throw new ArgumentNullException(nameof(sample));
            }

            return new RuuviTagSampleWithDisplayName() {
                AccelerationX = sample.AccelerationX,
                AccelerationY = sample.AccelerationY,
                AccelerationZ = sample.AccelerationZ,
                BatteryVoltage = sample.BatteryVoltage,
                DataFormat = sample.DataFormat,
                DisplayName = displayName,
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
