namespace NRuuviTag.Mqtt {

    /// <summary>
    /// Describes the MQTT publishing type for an <see cref="MqttBridge"/>.
    /// </summary>
    public enum PublishType {

        /// <summary>
        /// Samples are published to a single MQTT topic.
        /// </summary>
        SingleTopic,

        /// <summary>
        /// Samples are published to multiple topics, one per instrument (i.e. separate topics 
        /// for temperature, pressure, and so on).
        /// </summary>
        TopicPerMeasurement
        
    }
}
