using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using MQTTnet;
using MQTTnet.Extensions.ManagedClient;

namespace NRuuviTag.Mqtt {
    public class MqttBridge {

        private static readonly HashAlgorithm s_deviceIdHash = SHA256.Create();

        private readonly ILogger _logger;

        private readonly IRuuviTagListener _listener;

        private readonly IManagedMqttClient _mqttClient;

        private readonly IManagedMqttClientOptions _mqttClientOptions;

        private readonly MqttBridgeOptions _options;

        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions();

        private int _running;

        public MqttBridge(IRuuviTagListener listener, MqttBridgeOptions options, ILogger<MqttBridge> logger) {
            _listener = listener ?? throw new ArgumentNullException(nameof(listener));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = (ILogger) logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

            _jsonOptions.Converters.Add(new RuuviTagSampleJsonConverter());

            var clientOptionsBuilder = new ManagedMqttClientOptionsBuilder();
            _mqttClientOptions = clientOptionsBuilder.Build();
        }


        private static string GetDefaultDeviceId(RuuviTagSample sample) {
            if (string.IsNullOrWhiteSpace(sample.MacAddress)) {
                return "unknown";
            }
            var hash = s_deviceIdHash.ComputeHash(Encoding.UTF8.GetBytes(sample.MacAddress));
            return string.Join("", hash.Select(x => x.ToString("X2")));
        }


        private string GetTopicForSample(RuuviTagSample sample) {
            if (!_options.PublishChannel.Contains("{deviceId}")) {
                return _options.PublishChannel;
            }

            var deviceId = _options.GetDeviceId?.Invoke(sample) ?? GetDefaultDeviceId(sample);
            return _options.PublishChannel.Replace("{deviceId}", deviceId);
        }


        private MqttApplicationMessage BuildMessage<T>(string topic, T payload) {
            return new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(JsonSerializer.SerializeToUtf8Bytes(payload, _jsonOptions))
                .Build();
        }


        private IEnumerable<MqttApplicationMessage> BuildMessages(RuuviTagSample sample) {
            var topic = GetTopicForSample(sample);

            if (_options.PublishType == PublishType.SingleChannel) {
                yield return BuildMessage(topic, sample);
                yield break;
            }

            // Channel per measurement

            yield return BuildMessage(topic + "/acceleration-x", sample.AccelerationX);
            yield return BuildMessage(topic + "/acceleration-y", sample.AccelerationY);
            yield return BuildMessage(topic + "/acceleration-z", sample.AccelerationZ);
            yield return BuildMessage(topic + "/battery-voltage", sample.BatteryVoltage);
            yield return BuildMessage(topic + "/humidity", sample.Humidity);
            yield return BuildMessage(topic + "/measurement-sequence", sample.MeasurementSequence);
            yield return BuildMessage(topic + "/movement-counter", sample.MovementCounter);
            yield return BuildMessage(topic + "/pressure", sample.Pressure);
            yield return BuildMessage(topic + "/signal-strength", sample.SignalStrength);
            yield return BuildMessage(topic + "/temperature", sample.Temperature);
            yield return BuildMessage(topic + "/timestamp", sample.Timestamp.UtcDateTime);
            yield return BuildMessage(topic + "/tx-power", sample.TxPower);
        }


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
                        await _mqttClient.PublishAsync(BuildMessages(item)).ConfigureAwait(false);
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
