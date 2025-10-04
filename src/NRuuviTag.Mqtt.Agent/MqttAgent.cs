using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;

namespace NRuuviTag.Mqtt;

/// <summary>
/// Observes measurements emitted by an <see cref="IRuuviTagListener"/> and publishes them to 
/// an MQTT broker.
/// </summary>
public partial class MqttAgent : RuuviTagPublisher {

    /// <summary>
    /// For creating device IDs if no callback for performing this task is specified.
    /// </summary>
    private static readonly HashAlgorithm s_deviceIdHash = SHA256.Create();

    /// <summary>
    /// Device ID for all devices where the device ID cannot be determined.
    /// </summary>
    private const string UnknownDeviceId = "unknown";

    /// <summary>
    /// Logging.
    /// </summary>
    private readonly ILogger<MqttAgent> _logger;

    /// <summary>
    /// MQTT client.
    /// </summary>
    private readonly IManagedMqttClient _mqttClient;

    /// <summary>
    /// MQTT client options.
    /// </summary>
    private readonly ManagedMqttClientOptions _mqttClientOptions;

    /// <summary>
    /// MQTT bridge options.
    /// </summary>
    private readonly MqttAgentOptions _options;

    /// <summary>
    /// The template for the MQTT topic that messages will be published to.
    /// </summary>
    private readonly string _topicTemplate;

    /// <summary>
    /// JSON serializer options for serializing message payloads.
    /// </summary>
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions() { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };


    /// <summary>
    /// Creates a new <see cref="MqttAgent"/> object.
    /// </summary>
    /// <param name="listener">
    ///   The <see cref="IRuuviTagListener"/> to observe for sensor readings.
    /// </param>
    /// <param name="options">
    ///   Agent options.
    /// </param>
    /// <param name="factory">
    ///   The factory to use when creating MQTT clients.
    /// </param>
    /// <param name="loggerFactory">
    ///   The logger factory to use.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="listener"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ValidationException">
    ///   <paramref name="options"/> fails validation.
    /// </exception>
    public MqttAgent(IRuuviTagListener listener, MqttAgentOptions options, MqttFactory factory, ILoggerFactory? loggerFactory = null)
        : base(listener, options?.SampleRate ?? 0, BuildFilterDelegate(options!), loggerFactory?.CreateLogger<RuuviTagPublisher>()) {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        Validator.ValidateObject(options, new ValidationContext(options), true);

        _logger = loggerFactory?.CreateLogger<MqttAgent>() ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MqttAgent>.Instance;

        // If no client ID was specified, we'll generate one.
        var clientId = string.IsNullOrWhiteSpace(_options.ClientId) 
            ? Guid.NewGuid().ToString("N") 
            : _options.ClientId;

        // Set the template for the MQTT topic to post to. We can replace the {clientId}
        // placeholder immediately.
        _topicTemplate = string.IsNullOrWhiteSpace(_options.TopicName)
            ? MqttAgentOptions.DefaultTopicName.Replace("{clientId}", clientId)
            : _options.TopicName.Replace("{clientId}", clientId);

        var clientOptionsBuilder = new MqttClientOptionsBuilder()
            .WithCleanSession(true)
            .WithClientId(clientId)
            .WithProtocolVersion(_options.ProtocolVersion);

        // If we can successfully parse this as an absolute http://, https://, ws://, or wss://
        // URI, we will treat this as a websocket connection.
        var isWebsocketConnection = IsWebsocketConnection(_options.Hostname, out var websocketUri);

        // Get the hostname string. For websocket connections, we need to strip the scheme from
        // the start.
        var hostname = isWebsocketConnection
            // websocketUri.Scheme.Length + 3 so that we move past the :// in the URL
            ? websocketUri!.ToString().Substring(websocketUri.Scheme.Length + 3)
            : _options.Hostname;

        // We will tell the client to use TLS if it has been explicitly specified in the options,
        // or if a websocket connection is being used and the scheme in the websocket URL is
        // https:// or wss://
        var useTls = (_options.TlsOptions?.UseTls ?? false) || (isWebsocketConnection && IsTlsWebsocketUri(websocketUri!));

        if (isWebsocketConnection) {
            // Configure websocket MQTT connection.
            clientOptionsBuilder = clientOptionsBuilder.WithWebSocketServer(builder => {
                builder.WithUri(hostname);
            });
        }
        else {
            // Configure TCP MQTT connection.

            // Check in case hostname was specified in '<hostname>:<port>' format.
            var m = HostnameParser().Match(hostname!);

            // Get hostname from match; use 'localhost' as a fallback.
            var host = m.Groups["hostname"]?.Value;
            if (string.IsNullOrWhiteSpace(host)) {
                host = "localhost";
            }

            // Get port if defined.
            var port = ushort.TryParse(m.Groups["port"]?.Value, out var p)
                ? p
                : (ushort?) null;

            clientOptionsBuilder = clientOptionsBuilder.WithTcpServer(host, port);
        }

        // Configure TLS if required.
        if (useTls) {
            clientOptionsBuilder = clientOptionsBuilder.WithTlsOptions(builder => {
                builder.UseTls();

                if (_options.TlsOptions?.ClientCertificates != null) {
                    builder.WithClientCertificates(_options.TlsOptions.ClientCertificates);
                }

                if (_options.TlsOptions?.AllowUntrustedCertificates ?? false) {
                    builder.WithAllowUntrustedCertificates();
                }

                if (_options.TlsOptions?.IgnoreCertificateChainErrors ?? false) {
                    builder.WithIgnoreCertificateChainErrors();
                }

                if (_options.TlsOptions?.ValidateServerCertificate != null) {
                    builder.WithCertificateValidationHandler(context => _options.TlsOptions.ValidateServerCertificate.Invoke(context.Certificate, context.Chain, context.SslPolicyErrors));
                }
            });
        }

        // Configure credentials if either a user name or password has been provided.
        if (!string.IsNullOrWhiteSpace(_options.UserName) || !string.IsNullOrWhiteSpace(_options.Password)) {
            clientOptionsBuilder = clientOptionsBuilder.WithCredentials(_options.UserName, _options.Password);
        }

        // Build MQTT client options.
        var managedClientOptionsBuilder = new ManagedMqttClientOptionsBuilder()
            .WithClientOptions(clientOptionsBuilder.Build());
        _mqttClientOptions = managedClientOptionsBuilder.Build();

        // Build MQTT client.
        _mqttClient = factory.CreateManagedMqttClient();
        _mqttClient.ConnectedAsync += OnConnectedAsync;
        _mqttClient.DisconnectedAsync += OnDisconnectedAsync;
    }


    /// <summary>
    /// Builds a filter delegate that can restrict listening to broadcasts from only known 
    /// devices if required.
    /// </summary>
    /// <param name="options">
    ///   The options.
    /// </param>
    /// <returns>
    ///   The filter delegate.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    private static Func<string, bool> BuildFilterDelegate(MqttAgentOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.KnownDevicesOnly) {
            return addr => true;
        }

        var getDeviceInfo = options.GetDeviceInfo;
        return getDeviceInfo == null
            ? _ => false
            : addr => getDeviceInfo.Invoke(addr) != null;
    }


    /// <summary>
    /// Tests if the specified hostname string represents a websocket URL.
    /// </summary>
    /// <param name="hostname">
    ///   The hostname.
    /// </param>
    /// <param name="websocketUri">
    ///   The websocket URL.
    /// </param>
    /// <returns>
    ///   <see langword="true"/> if <paramref name="hostname"/> is a websocket URL, or 
    ///   <see langword="false"/> otherwise.
    /// </returns>
    /// <remarks>
    ///   <paramref name="hostname"/> will be considered to be a websocket URL if it can be 
    ///   parsed as an absolute URI with a scheme of <c>http</c>, <c>https</c>, <c>ws</c>, or 
    ///   <c>wss</c>.
    /// </remarks>
    private static bool IsWebsocketConnection(string? hostname, out Uri? websocketUri) {
        websocketUri = null;
        if (string.IsNullOrWhiteSpace(hostname)) {
            return false;
        }

        if (!Uri.TryCreate(hostname, UriKind.Absolute, out var uri)) {
            return false;
        }

        foreach (var scheme in new[] {
                     Uri.UriSchemeHttps,
                     Uri.UriSchemeHttp,
                     "wss",
                     "ws"
                 }) {
            if (!uri.Scheme.Equals(scheme, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            websocketUri = uri;
            return true;
        }

        return false;
    }


    /// <summary>
    /// Tests if the specified <see cref="Uri"/> requires TLS.
    /// </summary>
    /// <param name="uri">
    ///   The websocket URL.
    /// </param>
    /// <returns>
    ///   <see langword="true"/> if the <paramref name="uri"/> requires TLS, or 
    ///   <see langword="false"/> otherwise.
    /// </returns>
    /// <remarks>
    ///   The method will return <see langword="true"/> if <paramref name="uri"/> has a <see cref="Uri.Scheme"/> 
    ///   of <c>https</c> or <c>wss</c>.
    /// </remarks>
    private static bool IsTlsWebsocketUri(Uri uri) {
        return uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) || uri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase);
    }


    /// <summary>
    /// MQTT client connected handler.
    /// </summary>
    /// <param name="args">
    ///   The event arguments.
    /// </param>
    private Task OnConnectedAsync(MqttClientConnectedEventArgs args) {
        var hostname = _mqttClientOptions.ClientOptions.ChannelOptions switch {
            MqttClientTcpOptions tcpOptions => tcpOptions.RemoteEndpoint switch {
                System.Net.DnsEndPoint dns => $"{dns.Host}:{dns.Port}",
                System.Net.IPEndPoint ip => $"{ip.Address}:{ip.Port}",
                _ => "<unknown>"
            },
            MqttClientWebSocketOptions wsOptions => wsOptions.Uri.ToString(),
            _ => "<unknown>"
        };
        LogMqttClientConnected(hostname);
            
        return Task.CompletedTask;
    }


    /// <summary>
    /// MQTT client disconnected handler.
    /// </summary>
    /// <param name="args">
    ///   The event arguments.
    /// </param>
    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args) {
        LogMqttClientDisconnected(args.Exception);
        return Task.CompletedTask;
    }


    /// <summary>
    /// Gets the default device ID to use for the specified MAC address.
    /// </summary>
    /// <param name="macAddress">
    ///   The MAC address.
    /// </param>
    /// <returns>
    ///   The device ID.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="macAddress"/> is <see langword="null"/>.
    /// </exception>
    public static string GetDefaultDeviceId(string macAddress) {
        ArgumentNullException.ThrowIfNull(macAddress);

        return string.Join("", s_deviceIdHash.ComputeHash(Encoding.UTF8.GetBytes(macAddress)).Select(x => x.ToString("X2")));
    }


    /// <summary>
    /// Gets the device information for the specified sample.
    /// </summary>
    /// <param name="sample">
    ///   The sample.
    /// </param>
    /// <returns>
    ///   The device information.
    /// </returns>
    private Device GetDeviceInfo(RuuviDataPayload sample) {
        if (string.IsNullOrWhiteSpace(sample?.MacAddress)) {
            return new Device() { DeviceId = UnknownDeviceId };
        }

        var deviceInfo = _options.GetDeviceInfo?.Invoke(sample.MacAddress!);
        if (string.IsNullOrWhiteSpace(deviceInfo?.DeviceId)) {
            return _options.KnownDevicesOnly
                ? new Device() { DeviceId = UnknownDeviceId, MacAddress = sample.MacAddress }
                : new Device() {
                    DeviceId = GetDefaultDeviceId(sample.MacAddress!),
                    DisplayName = deviceInfo?.DisplayName,
                    MacAddress = sample.MacAddress
                };
        }

        return deviceInfo!;
    }


    /// <summary>
    /// Gets the MQTT topic name that the specified <paramref name="sample"/> will be 
    /// published to.
    /// </summary>
    /// <param name="sample">
    ///   The sample.
    /// </param>
    /// <returns>
    ///   The topic name for the sample.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="sample"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///   If the publishing mode for the bridge is <see cref="PublishType.TopicPerMeasurement"/>, 
    ///   the value returned by this method is the topic prefix to use.
    /// </remarks>
    public string GetTopicNameForSample(RuuviDataPayload sample) {
        ArgumentNullException.ThrowIfNull(sample);

        return GetTopicNameForSample(sample, GetDeviceInfo(sample));
    }


    /// <summary>
    /// Gets the MQTT topic name that the specified <paramref name="sample"/> will be 
    /// published to.
    /// </summary>
    /// <param name="sample">
    ///   The sample.
    /// </param>
    /// <param name="deviceInfo">
    ///   The device information for the sample.
    /// </param>
    /// <returns>
    ///   The topic name for the sample.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="sample"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///   If the publishing mode for the bridge is <see cref="PublishType.TopicPerMeasurement"/>, 
    ///   the value returned by this method is the topic prefix to use.
    /// </remarks>
    private string GetTopicNameForSample(RuuviDataPayload sample, Device deviceInfo) {
        ArgumentNullException.ThrowIfNull(sample);

        if (!_topicTemplate.Contains("{deviceId}")) {
            // Publish channel does not contain any device ID placeholders, so just return it
            // as-is.
            return _topicTemplate;
        }

        return _topicTemplate.Replace("{deviceId}", deviceInfo.DeviceId);
    }


    /// <summary>
    /// Builds a single MQTT message to be published to the broker.
    /// </summary>
    /// <typeparam name="T">
    ///   The value type of the message payload.
    /// </typeparam>
    /// <param name="topic">
    ///   The topic for the message.
    /// </param>
    /// <param name="payload">
    ///   The payload for the message. The payload will be serialized to JSON.
    /// </param>
    /// <returns>
    ///   A new <see cref="MqttApplicationMessage"/> object.
    /// </returns>
    private MqttApplicationMessage BuildMqttMessage<T>(string topic, T payload) {
        var builder = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(JsonSerializer.SerializeToUtf8Bytes(payload, _jsonOptions));

        if (_mqttClient.Options.ClientOptions.ProtocolVersion >= MQTTnet.Formatter.MqttProtocolVersion.V500) {
            builder = builder.WithContentType("application/json");
        }

        return builder.Build();
    }


    /// <summary>
    /// Builds the MQTT messages that should be published to the broker for a given 
    /// <see cref="RuuviDataPayload"/>.
    /// </summary>
    /// <param name="sample">
    ///   The <see cref="RuuviDataPayload"/> to be published.
    /// </param>
    /// <returns>
    ///   An <see cref="IEnumerable{MqttApplicationMessage}"/> that contains the messages to 
    ///   publish to the broker.
    /// </returns>
    /// <remarks>
    ///   The number of messages returned depends on the <see cref="MqttAgentOptions.PublishType"/> 
    ///   setting for the bridge.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="sample"/> is <see langword="null"/>.
    /// </exception>
    public IEnumerable<MqttApplicationMessage> BuildMqttMessages(RuuviTagSample sample) {
        ArgumentNullException.ThrowIfNull(sample);

        return BuildMqttMessages(sample, GetDeviceInfo(sample));
    }


    /// <summary>
    /// Builds the MQTT messages that should be published to the broker for a given 
    /// <see cref="RuuviDataPayload"/>.
    /// </summary>
    /// <param name="sample">
    ///   The <see cref="RuuviDataPayload"/> to be published.
    /// </param>
    /// <param name="deviceInfo">
    ///   The device ID for the sample.
    /// </param>
    /// <returns>
    ///   An <see cref="IEnumerable{MqttApplicationMessage}"/> that contains the messages to 
    ///   publish to the broker.
    /// </returns>
    /// <remarks>
    ///   The number of messages returned depends on the <see cref="MqttAgentOptions.PublishType"/> 
    ///   setting for the bridge.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="sample"/> is <see langword="null"/>.
    /// </exception>
    private IEnumerable<MqttApplicationMessage> BuildMqttMessages(RuuviTagSample sample, Device deviceInfo) {
        ArgumentNullException.ThrowIfNull(sample);

        var sampleWithDisplayName = new RuuviTagSampleExtended(deviceInfo.DeviceId, deviceInfo.DisplayName, sample);
        var topic = GetTopicNameForSample(sampleWithDisplayName, deviceInfo);

        sampleWithDisplayName = _options.PrepareForPublish is null
            ? sampleWithDisplayName
            : _options.PrepareForPublish.Invoke(sampleWithDisplayName);

        if (_options.PublishType == PublishType.SingleTopic) {
            // Single topic publish.
            yield return BuildMqttMessage(topic, sampleWithDisplayName);
            yield break;
        }

        // Topic per measurement.

        if (sampleWithDisplayName.AccelerationX is { } accelerationX) {
            yield return BuildMqttMessage(topic + "/acceleration-x", accelerationX);
        }
        if (sampleWithDisplayName.AccelerationY is { } accelerationY) {
            yield return BuildMqttMessage(topic + "/acceleration-y", accelerationY);
        }
        if (sampleWithDisplayName.AccelerationZ is { } accelerationZ) {
            yield return BuildMqttMessage(topic + "/acceleration-z", accelerationZ);
        }
        if (sampleWithDisplayName.BatteryVoltage is { } batteryVoltage) {
            yield return BuildMqttMessage(topic + "/battery-voltage", batteryVoltage);
        }
        if (sampleWithDisplayName.Calibrated is { } calibrated) {
            yield return BuildMqttMessage(topic + "/calibrated", calibrated);
        }
        if (sampleWithDisplayName.CO2 is { } co2) {
            yield return BuildMqttMessage(topic + "/co2", co2);
        }
        if (sampleWithDisplayName.DataFormat is { } dataFormat) {
            yield return BuildMqttMessage(topic + "/data-format", dataFormat);
        }
        if (sampleWithDisplayName.DisplayName is { } displayName) {
            yield return BuildMqttMessage(topic + "/display-name", displayName);
        }
        if (sampleWithDisplayName.Humidity is { } humidity) {
            yield return BuildMqttMessage(topic + "/humidity", humidity);
        }
        if (sampleWithDisplayName.Luminosity is { } luminosity) {
            yield return BuildMqttMessage(topic + "/luminosity", luminosity);
        }
        if (sampleWithDisplayName.MacAddress is { } macAddress) {
            yield return BuildMqttMessage(topic + "/mac-address", macAddress);
        }
        if (sampleWithDisplayName.MeasurementSequence is { } measurementSequence) {
            yield return BuildMqttMessage(topic + "/measurement-sequence", measurementSequence);
        }
        if (sampleWithDisplayName.MovementCounter is { } movementCounter) {
            yield return BuildMqttMessage(topic + "/movement-counter", movementCounter);
        }
        if (sampleWithDisplayName.NOX is { } nox) {
            yield return BuildMqttMessage(topic + "/nox", nox);
        }
        if (sampleWithDisplayName.PM10 is { } pm10) {
            yield return BuildMqttMessage(topic + "/pm-1.0", pm10);
        }
        if (sampleWithDisplayName.PM25 is { } pm25) {
            yield return BuildMqttMessage(topic + "/pm-2.5", pm25);
        }
        if (sampleWithDisplayName.PM40 is { } pm40) {
            yield return BuildMqttMessage(topic + "/pm-4.0", pm40);
        }
        if (sampleWithDisplayName.PM100 is { } pm100) {
            yield return BuildMqttMessage(topic + "/pm-10.0", pm100);
        }
        if (sampleWithDisplayName.Pressure is { } pressure) {
            yield return BuildMqttMessage(topic + "/pressure", pressure);
        }
        if (sampleWithDisplayName.SignalStrength is { } signalStrength) {
            yield return BuildMqttMessage(topic + "/signal-strength", signalStrength);
        }
        if (sampleWithDisplayName.Temperature is { } temperature) {
            yield return BuildMqttMessage(topic + "/temperature", temperature);
        }
        if (sampleWithDisplayName.Timestamp is { } timestamp) {
            yield return BuildMqttMessage(topic + "/timestamp", timestamp.UtcDateTime);
        }
        if (sampleWithDisplayName.TxPower is { } txPower) {
            yield return BuildMqttMessage(topic + "/tx-power", txPower);
        }
        if (sampleWithDisplayName.VOC is { } voc) {
            yield return BuildMqttMessage(topic + "/voc", voc);
        }
    }


    /// <inheritdoc/>
    protected override async Task RunAsync(IAsyncEnumerable<RuuviTagSample> samples, CancellationToken cancellationToken) {
        LogStartingMqttAgent();

        await _mqttClient.StartAsync(_mqttClientOptions).ConfigureAwait(false);
        try {
            await foreach (var item in samples.WithCancellation(cancellationToken).ConfigureAwait(false)) {
                var deviceInfo = GetDeviceInfo(item);
                if (_options.KnownDevicesOnly && string.Equals(deviceInfo.DeviceId, UnknownDeviceId, StringComparison.OrdinalIgnoreCase)) {
                    // Unknown device.
                    LogSkippingUnknownDeviceSample(item.MacAddress!);
                    continue;
                }

                if (_logger.IsEnabled(LogLevel.Trace)) {
                    LogDeviceSample(deviceInfo.DeviceId!, deviceInfo.DisplayName, JsonSerializer.Serialize(item));
                }

                try {
                    foreach (var message in BuildMqttMessages(item, deviceInfo)) {
                        await _mqttClient.EnqueueAsync(message).ConfigureAwait(false);
                    }
                }
                catch (Exception e) {
                    LogMqttPublishError(e);
                }
            }
        }
        finally {
            await _mqttClient.StopAsync().ConfigureAwait(false);
            LogMqttAgentStopped();
        }
    }
        
        
    /// <summary>
    /// Matches hostnames in <c>{name}</c> and <c>{name}:{port}</c> formats.
    /// </summary>
    [GeneratedRegex(@"^(?<hostname>[^\:]+?)(?:\:(?<port>\d+))?$")]
    private static partial Regex HostnameParser();


    [LoggerMessage(1, LogLevel.Information, "Starting MQTT agent.")]
    partial void LogStartingMqttAgent();

    [LoggerMessage(2, LogLevel.Information, "Stopped MQTT agent.")]
    partial void LogMqttAgentStopped();

    [LoggerMessage(3, LogLevel.Information, "Connected to MQTT broker: {hostname}")]
    partial void LogMqttClientConnected(string hostname);

    [LoggerMessage(4, LogLevel.Warning, "Disconnected from MQTT broker.")]
    partial void LogMqttClientDisconnected(Exception error);

    [LoggerMessage(5, LogLevel.Trace, "Skipping sample from device with unknown MAC address: {macAddress}")]
    partial void LogSkippingUnknownDeviceSample(string macAddress);

    [LoggerMessage(6, LogLevel.Trace, "Observed sample from device {deviceId} ('{displayName}'): {sampleJson}", SkipEnabledCheck = true)]
    partial void LogDeviceSample(string deviceId, string? displayName, string sampleJson);

    [LoggerMessage(7, LogLevel.Error, "Error publishing message to MQTT broker.")]
    partial void LogMqttPublishError(Exception error);
        
}