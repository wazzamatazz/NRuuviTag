namespace NRuuviTag.Mqtt {

    /// <summary>
    /// Describes a device that is being observed by an <see cref="MqttAgent"/>.
    /// </summary>
    public class MqttDeviceInfo : Device {

        /// <summary>
        /// The MQTT device ID, to be used in topic names etc. associated with the device.
        /// </summary>
        public string DeviceId { get; set; } = default!;

    }
}
