namespace NRuuviTag.Mqtt {

    /// <summary>
    /// Describes the MQTT connection type used by an <see cref="MqttAgent"/>.
    /// </summary>
    public enum ConnectionType {

        /// <summary>
        /// TCP connection.
        /// </summary>
        Tcp,

        /// <summary>
        /// Websocket connection.
        /// </summary>
        Websocket

    }
}
