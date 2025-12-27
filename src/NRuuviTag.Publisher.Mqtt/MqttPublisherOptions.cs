using System;
using System.ComponentModel.DataAnnotations;

using MQTTnet.Formatter;

namespace NRuuviTag.Mqtt;

/// <summary>
/// Options for <see cref="MqttPublisher"/>.
/// </summary>
public class MqttPublisherOptions : RuuviTagPublisherOptions {

    /// <summary>
    /// The default value for <see cref="TopicName"/>.
    /// </summary>
    /// <seealso cref="TopicName"/>
    public const string DefaultTopicName = "{clientId}/devices/{deviceId}";

    /// <summary>
    /// Broker hostname (and optional port).
    /// </summary>
    public string? Hostname { get; set; } = "localhost";

    /// <summary>
    /// TLS-related options.
    /// </summary>
    public MqttPublisherTlsOptions TlsOptions { get; set; } = new MqttPublisherTlsOptions();

    /// <summary>
    /// The MQTT client ID to use.
    /// </summary>
    public string? ClientId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The user name for the connection.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// The password for the connection.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// The MQTT protocol version to use.
    /// </summary>
    public MqttProtocolVersion ProtocolVersion { get; set; } = MqttProtocolVersion.V500;

    /// <summary>
    /// The publishing type for the <see cref="MqttPublisher"/>.
    /// </summary>
    public PublishType PublishType { get; set; }

    /// <summary>
    /// The topic that MQTT messages will be published to. When <see cref="PublishType"/> is 
    /// <see cref="PublishType.TopicPerMeasurement"/>, the <see cref="TopicName"/> is 
    /// used as a prefix for the individual measurement channels.
    /// </summary>
    /// <remarks>
    ///   The topic name can include <c>{clientId}</c> and <c>{deviceId}</c> as placeholders. 
    ///   At runtime, <c>{clientId}</c> will be replaced with the <see cref="ClientId"/> for 
    ///   the MQTT connection, and <c>{deviceId}</c> will be replaced with the device ID for 
    ///   the sample that is being published. The <see cref="GetDeviceInfo"/> callback can be 
    ///   used to define the device ID to use for a given <see cref="RuuviDataPayload"/>.
    /// </remarks>
    /// <seealso cref="DefaultTopicName"/>
    [Required]
    public string TopicName { get; set; } = DefaultTopicName;
    
    /// <summary>
    /// The managed MQTT client options to use.
    /// </summary>
    public ManagedMqttClientOptions? ClientOptions { get; set; }

}
