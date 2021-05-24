namespace NRuuviTag.Mqtt {

    /// <summary>
    /// Describes the MQTT publishing type for an <see cref="MqttBridge"/>.
    /// </summary>
    public enum PublishType {

        /// <summary>
        /// Samples are published to a single MQTT channel.
        /// </summary>
        SingleChannel,

        /// <summary>
        /// Samples are published to multiple channels, one per instrument (i.e. separate channels 
        /// for temperature, pressure, and so on).
        /// </summary>
        ChannelPerMeasurement
        
    }
}
