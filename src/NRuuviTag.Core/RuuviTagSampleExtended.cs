using System;

namespace NRuuviTag {

    /// <summary>
    /// Extends <see cref="RuuviTagSample"/> to include a device display name and identifier.
    /// </summary>
    public class RuuviTagSampleExtended : RuuviTagSample {

        /// <summary>
        /// The identifier for the device.
        /// </summary>
        /// <remarks>
        ///   To allow the <see cref="DeviceId"/> to be used in e.g. MQTT topic names, it is 
        ///   recommended that device identifiers consist only of alphanumeric characters, 
        ///   hyphens, and underscores.
        /// </remarks>
        public string? DeviceId { get; set; }

        /// <summary>
        /// The display name for the device that emitted the sample.
        /// </summary>
        public string? DisplayName { get; set; }


        /// <summary>
        /// Creates a new <see cref="RuuviTagSampleExtended"/> from an existing sample.
        /// </summary>
        /// <param name="sample">
        ///   The sample.
        /// </param>
        /// <param name="deviceId">
        ///   The device identifier. To allow the identifier to be used in e.g. MQTT topic names, 
        ///   it is recommended that device identifiers consist only of alphanumeric characters, 
        ///   hyphens, and underscores.
        /// </param>
        /// <param name="displayName">
        ///   The display name.
        /// </param>
        /// <returns>
        ///   A new <see cref="RuuviTagSampleExtended"/> object.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="sample"/> is <see langword="null"/>.
        /// </exception>
        public static RuuviTagSampleExtended Create(RuuviTagSample sample, string? deviceId, string? displayName) {
            if (sample == null) {
                throw new ArgumentNullException(nameof(sample));
            }

            return new RuuviTagSampleExtended() {
                AccelerationX = sample.AccelerationX,
                AccelerationY = sample.AccelerationY,
                AccelerationZ = sample.AccelerationZ,
                BatteryVoltage = sample.BatteryVoltage,
                DataFormat = sample.DataFormat,
                DeviceId = deviceId,
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
