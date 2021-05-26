using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using MQTTnet;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Extensions.ManagedClient;

namespace NRuuviTag.Mqtt {

    /// <summary>
    /// Observes measurements emitted by an <see cref="IRuuviTagListener"/> and publishes them to 
    /// an MQTT broker.
    /// </summary>
    public class MqttAgent {

        /// <summary>
        /// For creating device IDs if no callback for performing this task is specified.
        /// </summary>
        private static readonly HashAlgorithm s_deviceIdHash = SHA256.Create();

        /// <summary>
        /// Matches hostnames in <c>{name}</c> and <c>{name}:{port}</c> formats.
        /// </summary>
        private static readonly Regex s_hostnameParser = new Regex(@"^(?<hostname>[^\:]+?)(?:\:(?<port>\d+))?$");

        /// <summary>
        /// Device ID for all devices where the device ID cannot be determined.
        /// </summary>
        private const string UnknownDeviceId = "unknown";

        /// <summary>
        /// Logging.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// The <see cref="IRuuviTagListener"/> to observe.
        /// </summary>
        private readonly IRuuviTagListener _listener;

        /// <summary>
        /// MQTT client.
        /// </summary>
        private readonly IManagedMqttClient _mqttClient;

        /// <summary>
        /// MQTT client options.
        /// </summary>
        private readonly IManagedMqttClientOptions _mqttClientOptions;

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
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Indicates if the bridge is running or not.
        /// </summary>
        private int _running;


        /// <summary>
        /// Creates a new <see cref="MqttAgent"/> object.
        /// </summary>
        /// <param name="listener">
        ///   The <see cref="IRuuviTagListener"/> to observe for sensor readings.
        /// </param>
        /// <param name="options">
        ///   Bridge options.
        /// </param>
        /// <param name="factory">
        ///   The factory to use when creating MQTT clients.
        /// </param>
        /// <param name="logger">
        ///   The logger for the bridge.
        /// </param>
        public MqttAgent(IRuuviTagListener listener, MqttAgentOptions options, IMqttFactory factory, ILogger<MqttAgent>? logger = null) {
            _listener = listener ?? throw new ArgumentNullException(nameof(listener));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? (ILogger) Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

            _jsonOptions.Converters.Add(new RuuviTagSampleJsonConverter());

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
                clientOptionsBuilder = clientOptionsBuilder.WithWebSocketServer(hostname);
            }
            else {
                // Configure TCP MQTT connection.

                // Check in case hostname was specified in '<hostname>:<port>' format.
                var m = s_hostnameParser.Match(hostname);

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
                clientOptionsBuilder = clientOptionsBuilder.WithTls(tlsOptions => {
                    tlsOptions.UseTls = true;

                    if (_options.TlsOptions?.ClientCertificates != null) {
                        tlsOptions.Certificates = _options.TlsOptions.ClientCertificates;
                    }

                    tlsOptions.AllowUntrustedCertificates = _options.TlsOptions?.AllowUntrustedCertificates ?? false;
                    tlsOptions.IgnoreCertificateChainErrors = _options.TlsOptions?.IgnoreCertificateChainErrors ?? false;

                    if (_options.TlsOptions?.ValidateServerCertificate != null) {
                        tlsOptions.CertificateValidationHandler = context => _options.TlsOptions.ValidateServerCertificate.Invoke(context.Certificate, context.Chain, context.SslPolicyErrors);
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
            _mqttClient.UseConnectedHandler(OnConnected);
            _mqttClient.UseDisconnectedHandler(OnDisconnected);
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
                if (uri.Scheme.Equals(scheme, StringComparison.OrdinalIgnoreCase)) {
                    websocketUri = uri;
                    return true;
                }
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
        private void OnConnected(MqttClientConnectedEventArgs args) {
            _logger.LogInformation(Resources.LogMessage_MqttClientConnected);
        }


        /// <summary>
        /// MQTT client disconnected handler.
        /// </summary>
        /// <param name="args">
        ///   The event arguments.
        /// </param>
        private void OnDisconnected(MqttClientDisconnectedEventArgs args) {
            _logger.LogWarning(Resources.LogMessage_MqttClientDisconnected);
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
            if (macAddress == null) {
                throw new ArgumentNullException(nameof(macAddress));
            }

            return string.Join("", s_deviceIdHash.ComputeHash(Encoding.UTF8.GetBytes(macAddress)).Select(x => x.ToString("X2")));
        }


        /// <summary>
        /// Gets the device information for the specified sample.
        /// </summary>
        /// <param name="sample">
        ///   The sample.
        /// </param>
        /// <returns>
        ///   The device ID.
        /// </returns>
        private DeviceInfo GetDeviceInfo(RuuviTagSample sample) {
            if (string.IsNullOrWhiteSpace(sample.MacAddress)) {
                return new DeviceInfo() { DeviceId = UnknownDeviceId };
            }

            var deviceInfo = _options.GetDeviceInfo?.Invoke(sample);
            if (string.IsNullOrWhiteSpace(deviceInfo?.DeviceId)) {
                return _options.KnownDevicesOnly
                    ? new DeviceInfo() { DeviceId = UnknownDeviceId }
                    : new DeviceInfo() {
                        DeviceId = GetDefaultDeviceId(sample.MacAddress!),
                        DisplayName = deviceInfo?.DisplayName
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
        public string GetTopicNameForSample(RuuviTagSample sample) {
            if (sample == null) {
                throw new ArgumentNullException(nameof(sample));
            }

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
        private string GetTopicNameForSample(RuuviTagSample sample, DeviceInfo deviceInfo) {
            if (sample == null) {
                throw new ArgumentNullException(nameof(sample));
            }

            var topic = _topicTemplate;

            if (!topic.Contains("{deviceId}")) {
                // Publish channel does not contain any device ID placeholders, so just return it
                // as-is.
                return topic;
            }

            return topic.Replace("{deviceId}", deviceInfo.DeviceId);
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
            return new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(JsonSerializer.SerializeToUtf8Bytes(payload, _jsonOptions))
                .Build();
        }


        /// <summary>
        /// Builds the MQTT messages that should be published to the broker for a given 
        /// <see cref="RuuviTagSample"/>.
        /// </summary>
        /// <param name="sample">
        ///   The <see cref="RuuviTagSample"/> to be published.
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
            if (sample == null) {
                throw new ArgumentNullException(nameof(sample));
            }

            return BuildMqttMessages(sample, GetDeviceInfo(sample));
        }


        /// <summary>
        /// Builds the MQTT messages that should be published to the broker for a given 
        /// <see cref="RuuviTagSample"/>.
        /// </summary>
        /// <param name="sample">
        ///   The <see cref="RuuviTagSample"/> to be published.
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
        private IEnumerable<MqttApplicationMessage> BuildMqttMessages(RuuviTagSample sample, DeviceInfo deviceInfo) {
            if (sample == null) {
                throw new ArgumentNullException(nameof(sample));
            }

            var topic = GetTopicNameForSample(sample, deviceInfo);

            if (_options.PrepareForPublish != null) {
                sample = RuuviTagSample.FromExisting(sample);
                _options.PrepareForPublish.Invoke(sample);
            }

            if (_options.PublishType == PublishType.SingleTopic) {
                // Single topic publish.
                yield return BuildMqttMessage(topic, sample);
                yield break;
            }

            // Topic per measurement.

            if (sample.AccelerationX != null) {
                yield return BuildMqttMessage(topic + "/acceleration-x", sample.AccelerationX);
            }
            if (sample.AccelerationY != null) {
                yield return BuildMqttMessage(topic + "/acceleration-y", sample.AccelerationY);
            }
            if (sample.AccelerationZ != null) {
                yield return BuildMqttMessage(topic + "/acceleration-z", sample.AccelerationZ);
            }
            if (sample.BatteryVoltage != null) {
                yield return BuildMqttMessage(topic + "/battery-voltage", sample.BatteryVoltage);
            }
            if (sample.Humidity != null) {
                yield return BuildMqttMessage(topic + "/humidity", sample.Humidity);
            }
            if (sample.MeasurementSequence != null) {
                yield return BuildMqttMessage(topic + "/measurement-sequence", sample.MeasurementSequence);
            }
            if (sample.MovementCounter != null) {
                yield return BuildMqttMessage(topic + "/movement-counter", sample.MovementCounter);
            }
            if (sample.Pressure != null) {
                yield return BuildMqttMessage(topic + "/pressure", sample.Pressure);
            }
            if (sample.SignalStrength != null) {
                yield return BuildMqttMessage(topic + "/signal-strength", sample.SignalStrength);
            }
            if (sample.Temperature != null) {
                yield return BuildMqttMessage(topic + "/temperature", sample.Temperature);
            }
            if (sample.Timestamp != default) {
                yield return BuildMqttMessage(topic + "/timestamp", sample.Timestamp.UtcDateTime);
            }
            if (sample.TxPower != null) {
                yield return BuildMqttMessage(topic + "/tx-power", sample.TxPower);
            }
        }


        /// <summary>
        /// Runs the <see cref="MqttAgent"/> until the specified cancellation token requests 
        /// cancellation.
        /// </summary>
        /// <param name="cancellationToken">
        ///   The cancellation token to observe.
        /// </param>
        /// <returns>
        ///   A <see cref="Task"/> that will run the <see cref="MqttAgent"/> until <paramref name="cancellationToken"/> 
        ///   requests cancellation.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   The <see cref="MqttAgent"/> is already running.
        /// </exception>
        public async Task RunAsync(CancellationToken cancellationToken) {
            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) {
                // Already running.
                throw new InvalidOperationException(Resources.Error_MqttBridgeIsAlreadyRunning);
            }

            var publishInterval = _options.PublishInterval;
            var useBackgroundPublish = publishInterval > 0;
            var pendingPublish = useBackgroundPublish 
                ? new Dictionary<string, IEnumerable<MqttApplicationMessage>>(StringComparer.Ordinal)
                : null;

            // Publishes the specified MQTT messages.
            async Task PublishMessages(IEnumerable<MqttApplicationMessage> messages, CancellationToken ct) {
                try {
                    await _mqttClient.PublishAsync(messages).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException e) {
                    if (ct.IsCancellationRequested) {
                        // Cancellation requested; rethrow and let the caller handle it.
                        throw;
                    }
                    _logger.LogError(e, Resources.LogMessage_MqttPublishError);
                }
                catch (Exception e) {
                    _logger.LogError(e, Resources.LogMessage_MqttPublishError);
                }
            }

            // Publishes all pending MQTT messages and clears the pendingPublish dictionary.
            async Task PublishPendingMessages(CancellationToken ct) {
                if (!useBackgroundPublish) {
                    return;
                }

                MqttApplicationMessage[] messages;
                lock (pendingPublish!) {
                    messages = pendingPublish.SelectMany(x => x.Value).ToArray();
                    pendingPublish.Clear();
                }

                if (messages.Length == 0) {
                    return;
                }

                await PublishMessages(messages, cancellationToken).ConfigureAwait(false);
            }

            try {
                _logger.LogInformation(Resources.LogMessage_StartingMqttBridge);

                await _mqttClient.StartAsync(_mqttClientOptions).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                if (useBackgroundPublish) {
                    // We're using a scheduled publish interval - start the background task that
                    // will perform this job.
                    _ = Task.Run(async () => { 
                        try {
                            while (!cancellationToken.IsCancellationRequested) {
                                await Task.Delay(TimeSpan.FromSeconds(publishInterval), cancellationToken).ConfigureAwait(false);
                                await PublishPendingMessages(cancellationToken).ConfigureAwait(false);
                            }
                        }
                        catch (OperationCanceledException) { }
                    }, cancellationToken);
                }

                // Start the listener,
                await foreach (var item in _listener.ListenAsync(cancellationToken).ConfigureAwait(false)) {
                    try {
                        var deviceInfo = GetDeviceInfo(item);
                        if (_options.KnownDevicesOnly && string.Equals(deviceInfo.DeviceId, UnknownDeviceId, StringComparison.OrdinalIgnoreCase)) {
                            // Unknown device.
                            if (_logger.IsEnabled(LogLevel.Trace)) {
                                _logger.LogTrace(string.Format(CultureInfo.CurrentCulture, Resources.LogMessage_SkippingSampleFromUnknownDevice, item.MacAddress));
                            }
                            continue;
                        }

                        if (_logger.IsEnabled(LogLevel.Debug)) {
                            _logger.LogDebug($"{(string.IsNullOrWhiteSpace(deviceInfo.DisplayName) ? deviceInfo.DeviceId : deviceInfo.DisplayName)}: {JsonSerializer.Serialize(item)}");
                        }

                        var messages = BuildMqttMessages(item, deviceInfo).ToArray();
                        
                        if (useBackgroundPublish) {
                            // Add messages to pending publish list.
                            lock (pendingPublish!) {
                                pendingPublish[deviceInfo.DeviceId] = messages;
                            }
                        }
                        else {
                            // Publish immediately.
                            await PublishMessages(messages, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException e) {
                        if (cancellationToken.IsCancellationRequested) {
                            // Cancellation requested; rethrow and let the outer catch handle it.
                            throw;
                        }
                        _logger.LogError(e, Resources.LogMessage_MqttPublishError);
                    }
                    catch (Exception e) {
                        _logger.LogError(e, Resources.LogMessage_MqttPublishError);
                    }
                }
            }
            catch (OperationCanceledException) {
                if (!cancellationToken.IsCancellationRequested) {
                    // This exception was not caused by cancellationToken requesting cancellation.
                    throw;
                }
            }
            finally {
                if (useBackgroundPublish) {
                    // Flush any pending values.
                    await PublishPendingMessages(default).ConfigureAwait(false);
                }
                await _mqttClient.StopAsync().ConfigureAwait(false);
                _logger.LogInformation(Resources.LogMessage_MqttBridgeStopped);
                _running = 0;
            }
        }

    }
}
