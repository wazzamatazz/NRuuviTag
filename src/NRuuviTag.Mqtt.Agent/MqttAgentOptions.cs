using System;
using System.ComponentModel.DataAnnotations;

using MQTTnet.Formatter;

namespace NRuuviTag.Mqtt {

    /// <summary>
    /// Options for <see cref="MqttAgent"/>.
    /// </summary>
    public class MqttAgentOptions {

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
        public MqttAgentTlsOptions TlsOptions { get; set; } = new MqttAgentTlsOptions();

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
        /// The publishing type for the <see cref="MqttAgent"/>.
        /// </summary>
        public PublishType PublishType { get; set; }

        /// <summary>
        /// The fastest rate (in seconds) that values will be sampled at for each observed device. 
        /// Less than zero means that all observed values are immediately passed to the <see cref="MqttAgent"/> 
        /// for processing.
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// The topic that MQTT messages will be published to. When <see cref="PublishType"/> is 
        /// <see cref="PublishType.TopicPerMeasurement"/>, the <see cref="TopicName"/> is 
        /// used as a prefix for the individual measurement channels.
        /// </summary>
        /// <remarks>
        ///   The topic name can include <c>{clientId}</c> and <c>{deviceId}</c> as placeholders. 
        ///   At runtime, <c>{clientId}</c> will be replaced with the <see cref="ClientId"/> for 
        ///   the MQTT connection, and <c>{deviceId}</c> will be replaced with the device ID for 
        ///   the sample that is being published. The <see cref="GetDeviceId"/> callback can be 
        ///   used to define the device ID to use for a given <see cref="RuuviTagSample"/>.
        /// </remarks>
        /// <seealso cref="DefaultTopicName"/>
        [Required]
        public string TopicName { get; set; } = DefaultTopicName;

        /// <summary>
        /// A callback that is used to retrieve the device information to use for a given 
        ///MAC address.
        /// </summary>
        /// <remarks>
        ///   If <see cref="GetDeviceInfo"/> is <see langword="null"/>, a default <see cref="Device"/> 
        ///   will be generated for the sample.
        /// </remarks>
        public Func<string, Device?>? GetDeviceInfo { get; set; }

        /// <summary>
        /// When <see langword="true"/>, only samples from known devices will be published. See 
        /// remarks for details.
        /// </summary>
        /// <remarks>
        ///   When <see cref="KnownDevicesOnly"/> is enabled, a sample will be discarded if 
        ///   <see cref="GetDeviceInfo"/> is <see langword="null"/>, or if it returns <see langword="null"/> 
        ///   for a given sample.
        /// </remarks>
        public bool KnownDevicesOnly { get; set; }

        /// <summary>
        /// A callback that is used to prepare a sample prior to publishing it to an MQTT topic or 
        /// topics.
        /// </summary>
        /// <remarks>
        /// 
        /// <para>
        ///   Use the <see cref="PrepareForPublish"/> callback to modify a <see cref="RuuviTagSample"/> 
        ///   instance prior to it being published to the MQTT broker (e.g. to perform unit conversion). 
        ///   Set any property on a sample to <see langword="null"/> to exclude that property from the 
        ///   publish.
        /// </para>
        /// 
        /// </remarks>
        public Action<RuuviTagSampleExtended>? PrepareForPublish { get; set; }

    }

}
