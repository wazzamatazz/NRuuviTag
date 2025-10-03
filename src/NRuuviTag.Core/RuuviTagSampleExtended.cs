using System;
using System.Text.Json.Serialization;

namespace NRuuviTag {

    /// <summary>
    /// Extends <see cref="RuuviTagSample"/> to include a device display name and identifier.
    /// </summary>
    public record RuuviTagSampleExtended : RuuviTagSample {

        /// <summary>
        /// The identifier for the device.
        /// </summary>
        /// <remarks>
        ///   To allow the <see cref="DeviceId"/> to be used in e.g. MQTT topic names, it is 
        ///   recommended that device identifiers consist only of alphanumeric characters, 
        ///   hyphens, and underscores.
        /// </remarks>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DeviceId { get; init; }

        /// <summary>
        /// The display name for the device that emitted the sample.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DisplayName { get; init; }


        /// <summary>
        /// Creates a new <see cref="RuuviTagSampleExtended"/> instance.
        /// </summary>
        public RuuviTagSampleExtended() { }
        
        
        /// <summary>
        /// Creates a new <see cref="RuuviTagSampleExtended"/> instance.
        /// </summary>
        /// <param name="deviceId">
        ///   The identifier for the device.
        /// </param>
        /// <param name="displayName">
        ///   The display name for the device.
        /// </param>
        /// <param name="sample">
        ///   The <see cref="RuuviTagSample"/> to extend.
        /// </param>
        public RuuviTagSampleExtended(string? deviceId, string? displayName, RuuviTagSample sample) : base(sample) {
            DeviceId = deviceId;
            DisplayName = displayName;
        }

    }

}
