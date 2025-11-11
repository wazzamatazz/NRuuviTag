using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
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
public partial class MqttPublisher : RuuviTagPublisher {
    
    /// <summary>
    /// Device ID for all devices where the device ID cannot be determined.
    /// </summary>
    private const string UnknownDeviceId = "unknown";

    /// <summary>
    /// Logging.
    /// </summary>
    private readonly ILogger<MqttPublisher> _logger;

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
    private readonly MqttPublisherOptions _options;

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
    /// Creates a new <see cref="MqttPublisher"/> object.
    /// </summary>
    /// <param name="listener">
    ///   The <see cref="IRuuviTagListener"/> to observe for sensor readings.
    /// </param>
    /// <param name="options">
    ///   Publisher options.
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
    public MqttPublisher(IRuuviTagListener listener, MqttPublisherOptions options, MqttFactory factory, ILoggerFactory? loggerFactory = null)
        : base(listener, options, loggerFactory?.CreateLogger<RuuviTagPublisher>()) {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        Validator.ValidateObject(options, new ValidationContext(options), true);

        _logger = loggerFactory?.CreateLogger<MqttPublisher>() ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MqttPublisher>.Instance;

        // If no client ID was specified, we'll generate one.
        var clientId = string.IsNullOrWhiteSpace(_options.ClientId) 
            ? Guid.NewGuid().ToString("N") 
            : _options.ClientId;

        // Set the template for the MQTT topic to post to. We can replace the {clientId}
        // placeholder immediately.
        _topicTemplate = string.IsNullOrWhiteSpace(_options.TopicName)
            ? MqttPublisherOptions.DefaultTopicName.Replace("{clientId}", clientId)
            : _options.TopicName.Replace("{clientId}", clientId);

        var clientOptionsBuilder = new MqttClientOptionsBuilder()
            .WithCleanSession()
            .WithClientId(clientId)
            .WithProtocolVersion(_options.ProtocolVersion);

        // If we can successfully parse this as an absolute http://, https://, ws://, or wss://
        // URI, we will treat this as a websocket connection.
        var isWebsocketConnection = IsWebsocketConnection(_options.Hostname, out var websocketUri);

        // Get the hostname string. For websocket connections, we need to strip the scheme from
        // the start.
        var hostname = isWebsocketConnection
            // websocketUri.Scheme.Length + 3 so that we move past the :// in the URL
            ? websocketUri!.ToString()[(websocketUri.Scheme.Length + 3)..]
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

        // Configure credentials if either a username or password has been provided.
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

        ReadOnlySpan<string> validSchemes = [
            Uri.UriSchemeHttps,
            Uri.UriSchemeHttp,
            "wss",
            "ws"
        ];
        
        foreach (var scheme in validSchemes) {
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
            MqttClientWebSocketOptions wsOptions => wsOptions.Uri,
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
        return string.Join("", SHA256.HashData(Encoding.UTF8.GetBytes(macAddress)).Select(x => x.ToString("X2")));
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
    public string GetTopicNameForSample(RuuviTagSample sample) {
        ArgumentNullException.ThrowIfNull(sample);

        return _topicTemplate.Contains("{deviceId}") 
            ? _topicTemplate.Replace("{deviceId}", sample.DeviceId ?? UnknownDeviceId)
            // Publish channel does not contain any device ID placeholders, so just return it
            // as-is.
            : _topicTemplate;
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
    ///   The number of messages returned depends on the <see cref="MqttPublisherOptions.PublishType"/> 
    ///   setting for the bridge.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="sample"/> is <see langword="null"/>.
    /// </exception>
    public IEnumerable<MqttApplicationMessage> BuildMqttMessages(RuuviTagSample sample) {
        ArgumentNullException.ThrowIfNull(sample);
        
        var topic = GetTopicNameForSample(sample);

        if (_options.PublishType == PublishType.SingleTopic) {
            // Single topic publish.
            yield return BuildMqttMessage(topic, sample);
            yield break;
        }

        // Topic per measurement.

        if (sample.AccelerationX is { } accelerationX) {
            yield return BuildMqttMessage(topic + "/acceleration-x", accelerationX);
        }
        if (sample.AccelerationY is { } accelerationY) {
            yield return BuildMqttMessage(topic + "/acceleration-y", accelerationY);
        }
        if (sample.AccelerationZ is { } accelerationZ) {
            yield return BuildMqttMessage(topic + "/acceleration-z", accelerationZ);
        }
        if (sample.BatteryVoltage is { } batteryVoltage) {
            yield return BuildMqttMessage(topic + "/battery-voltage", batteryVoltage);
        }
        if (sample.Calibrated is { } calibrated) {
            yield return BuildMqttMessage(topic + "/calibrated", calibrated);
        }
        if (sample.CO2 is { } co2) {
            yield return BuildMqttMessage(topic + "/co2", co2);
        }
        if (sample.DataFormat is { } dataFormat) {
            yield return BuildMqttMessage(topic + "/data-format", dataFormat);
        }
        if (sample.DeviceId is { } deviceId) {
            yield return BuildMqttMessage(topic + "/device-id", deviceId);
        }
        if (sample.Humidity is { } humidity) {
            yield return BuildMqttMessage(topic + "/humidity", humidity);
        }
        if (sample.Luminosity is { } luminosity) {
            yield return BuildMqttMessage(topic + "/luminosity", luminosity);
        }
        if (sample.MacAddress is { } macAddress) {
            yield return BuildMqttMessage(topic + "/mac-address", macAddress);
        }
        if (sample.MeasurementSequence is { } measurementSequence) {
            yield return BuildMqttMessage(topic + "/measurement-sequence", measurementSequence);
        }
        if (sample.MovementCounter is { } movementCounter) {
            yield return BuildMqttMessage(topic + "/movement-counter", movementCounter);
        }
        if (sample.NOX is { } nox) {
            yield return BuildMqttMessage(topic + "/nox", nox);
        }
        if (sample.PM10 is { } pm10) {
            yield return BuildMqttMessage(topic + "/pm-1.0", pm10);
        }
        if (sample.PM25 is { } pm25) {
            yield return BuildMqttMessage(topic + "/pm-2.5", pm25);
        }
        if (sample.PM40 is { } pm40) {
            yield return BuildMqttMessage(topic + "/pm-4.0", pm40);
        }
        if (sample.PM100 is { } pm100) {
            yield return BuildMqttMessage(topic + "/pm-10.0", pm100);
        }
        if (sample.Pressure is { } pressure) {
            yield return BuildMqttMessage(topic + "/pressure", pressure);
        }
        if (sample.SignalStrength is { } signalStrength) {
            yield return BuildMqttMessage(topic + "/signal-strength", signalStrength);
        }
        if (sample.Temperature is { } temperature) {
            yield return BuildMqttMessage(topic + "/temperature", temperature);
        }
        if (sample.Timestamp is { } timestamp) {
            yield return BuildMqttMessage(topic + "/timestamp", timestamp.UtcDateTime);
        }
        if (sample.TxPower is { } txPower) {
            yield return BuildMqttMessage(topic + "/tx-power", txPower);
        }
        if (sample.VOC is { } voc) {
            yield return BuildMqttMessage(topic + "/voc", voc);
        }
    }


    /// <inheritdoc/>
    protected override async Task RunAsync(ChannelReader<RuuviTagSample> samples, CancellationToken cancellationToken) {
        LogStartingMqttPublisher();

        await _mqttClient.StartAsync(_mqttClientOptions).ConfigureAwait(false);
        try {
            while (await samples.WaitToReadAsync(cancellationToken).ConfigureAwait(false)) {
                while (samples.TryRead(out var item)) {
                    LogDeviceSample(item.DeviceId, item);

                    try {
                        foreach (var message in BuildMqttMessages(item)) {
                            await _mqttClient.EnqueueAsync(message).ConfigureAwait(false);
                        }
                    }
                    catch (Exception e) {
                        LogMqttPublishError(e);
                    }
                }
            }
        }
        finally {
            await _mqttClient.StopAsync().ConfigureAwait(false);
            LogMqttPublisherStopped();
        }
    }
        
        
    /// <summary>
    /// Matches hostnames in <c>{name}</c> and <c>{name}:{port}</c> formats.
    /// </summary>
    [GeneratedRegex(@"^(?<hostname>[^\:]+?)(?:\:(?<port>\d+))?$")]
    private static partial Regex HostnameParser();


    [LoggerMessage(1, LogLevel.Information, "Starting MQTT publisher.")]
    partial void LogStartingMqttPublisher();

    [LoggerMessage(2, LogLevel.Information, "Stopped MQTT publisher.")]
    partial void LogMqttPublisherStopped();

    [LoggerMessage(3, LogLevel.Information, "Connected to MQTT broker: {hostname}")]
    partial void LogMqttClientConnected(string hostname);

    [LoggerMessage(4, LogLevel.Warning, "Disconnected from MQTT broker.")]
    partial void LogMqttClientDisconnected(Exception error);

    [LoggerMessage(5, LogLevel.Trace, "Skipping sample from device with unknown MAC address: {macAddress}")]
    partial void LogSkippingUnknownDeviceSample(string macAddress);

    [LoggerMessage(6, LogLevel.Trace, "Observed sample from device {deviceId}: {sample}")]
    partial void LogDeviceSample(string? deviceId, RuuviTagSample sample);

    [LoggerMessage(7, LogLevel.Error, "Error publishing message to MQTT broker.")]
    partial void LogMqttPublishError(Exception error);
        
}
