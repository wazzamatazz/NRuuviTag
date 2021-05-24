using System;
using System.Collections.Generic;
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
    public class MqttBridge {

        /// <summary>
        /// For creating device IDs if no callback for performing this task is specified.
        /// </summary>
        private static readonly HashAlgorithm s_deviceIdHash = SHA256.Create();

        /// <summary>
        /// Matches hostnames in <c>{name}</c> and <c>{name}:{port}</c> formats.
        /// </summary>
        private static readonly Regex s_hostnameParser = new Regex(@"^(?<hostname>[^:]+?)(?::(?<port>\d+))$");

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
        private readonly MqttBridgeOptions _options;

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
        /// Creates a new <see cref="MqttBridge"/> object.
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
        public MqttBridge(IRuuviTagListener listener, MqttBridgeOptions options, IMqttFactory factory, ILogger<MqttBridge>? logger = null) {
            _listener = listener ?? throw new ArgumentNullException(nameof(listener));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? (ILogger) Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

            _jsonOptions.Converters.Add(new RuuviTagSampleJsonConverter());

            var clientOptionsBuilder = new MqttClientOptionsBuilder()
                .WithCleanSession(true)
                .WithClientId(string.IsNullOrWhiteSpace(_options.ClientId) ? Guid.NewGuid().ToString("N") : _options.ClientId);

            if (_options.ConnectionType == ConnectionType.Websocket) {
                clientOptionsBuilder = clientOptionsBuilder.WithWebSocketServer(_options.Hostname);
            }
            else {
                // Check in case hostname was specified in '<hostname>:<port>' format.
                var m = s_hostnameParser.Match(_options.Hostname);

                // Get hostname; use 'localhost' as a fallback.
                var hostname = m.Groups["hostname"]?.Value ?? "localhost";
                var port = ushort.TryParse(m.Groups["port"]?.Value, out var p)
                    ? p
                    : (ushort?) null;

                clientOptionsBuilder = clientOptionsBuilder.WithTcpServer(hostname, port);
            }

            if (_options.UseTls) {
                clientOptionsBuilder = clientOptionsBuilder.WithTls();
            }

            if (!string.IsNullOrWhiteSpace(_options.UserName) || !string.IsNullOrWhiteSpace(_options.Password)) {
                clientOptionsBuilder = clientOptionsBuilder.WithCredentials(_options.UserName, _options.Password);
            }

            var managedClientOptionsBuilder = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions(clientOptionsBuilder.Build());

            _mqttClientOptions = managedClientOptionsBuilder.Build();

            _mqttClient = factory.CreateManagedMqttClient();
            _mqttClient.UseConnectedHandler(OnConnected);
            _mqttClient.UseDisconnectedHandler(OnDisconnected);
        }


        /// <summary>
        /// MQTT client connected handler.
        /// </summary>
        /// <param name="args">
        ///   The event arguments.
        /// </param>
        private void OnConnected(MqttClientConnectedEventArgs args) {
            _logger.LogWarning(Resources.LogMessage_MqttClientConnected);
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
        /// Gets the device ID for the specified sample. This method is used as a fallback if 
        /// <see cref="MqttBridgeOptions.GetDeviceId"/> is <see langword="null"/> or does not 
        /// return a device ID to the specified sample.
        /// </summary>
        /// <param name="sample">
        ///   The sample.
        /// </param>
        /// <returns>
        ///   The device ID.
        /// </returns>
        private static string GetDefaultDeviceId(RuuviTagSample sample) {
            if (string.IsNullOrWhiteSpace(sample.MacAddress)) {
                return UnknownDeviceId;
            }
            var hash = s_deviceIdHash.ComputeHash(Encoding.UTF8.GetBytes(sample.MacAddress));
            return string.Join("", hash.Select(x => x.ToString("X2")));
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

            var topic = string.IsNullOrWhiteSpace(_options.TopicName)
                ? MqttBridgeOptions.DefaultTopicName
                : _options.TopicName;

            if (!topic.Contains("{deviceId}")) {
                // Publish channel does not contain any device ID placeholders, so just return it
                // as-is.
                return topic;
            }

            // Get the device ID for the sample using the options callback (if defined), and fall
            // back to GetDefaultDeviceId if required.
            var deviceId = _options.GetDeviceId?.Invoke(sample);
            if (string.IsNullOrWhiteSpace(deviceId)) {
                deviceId = GetDefaultDeviceId(sample);
            }

            return topic.Replace("{deviceId}", deviceId);
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
        ///   The number of messages returned depends on the <see cref="MqttBridgeOptions.PublishType"/> 
        ///   setting for the bridge.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="sample"/> is <see langword="null"/>.
        /// </exception>
        public IEnumerable<MqttApplicationMessage> BuildMqttMessages(RuuviTagSample sample) {
            if (sample == null) {
                throw new ArgumentNullException(nameof(sample));
            }

            var topic = GetTopicNameForSample(sample);

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
        /// Runs the <see cref="MqttBridge"/> until the specified cancellation token requests 
        /// cancellation.
        /// </summary>
        /// <param name="cancellationToken">
        ///   The cancellation token to observe.
        /// </param>
        /// <returns>
        ///   A <see cref="Task"/> that will run the <see cref="MqttBridge"/> until <paramref name="cancellationToken"/> 
        ///   requests cancellation.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   The <see cref="MqttBridge"/> is already running.
        /// </exception>
        public async Task RunAsync(CancellationToken cancellationToken) {
            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) {
                // Already running.
                throw new InvalidOperationException(Resources.Error_MqttBridgeIsAlreadyRunning);
            }

            try {
                _logger.LogInformation(Resources.LogMessage_StartingMqttBridge);

                await _mqttClient.StartAsync(_mqttClientOptions).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                await foreach (var item in _listener.ListenAsync(cancellationToken).ConfigureAwait(false)) {
                    try {
                        await _mqttClient.PublishAsync(BuildMqttMessages(item)).ConfigureAwait(false);
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
                await _mqttClient.StopAsync().ConfigureAwait(false);
                _logger.LogInformation(Resources.LogMessage_MqttBridgeStopped);
                _running = 0;
            }
        }

    }
}
