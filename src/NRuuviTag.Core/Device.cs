namespace NRuuviTag {

    /// <summary>
    /// Describes a RuuviTag device.
    /// </summary>
    public class Device {

        /// <summary>
        /// The MAC address for the device.
        /// </summary>
        public string? MacAddress { get; set; }

        /// <summary>
        /// The identifier for the device. This identifier can be used in e.g. MQTT topic names 
        /// when publishing device values to a destination.
        /// </summary>
        public string? DeviceId { get; set; }

        /// <summary>
        /// The display name for the device.
        /// </summary>
        public string? DisplayName { get; set; }

    }
}
