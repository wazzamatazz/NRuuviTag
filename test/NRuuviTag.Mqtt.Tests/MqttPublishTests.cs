using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MQTTnet;
using MQTTnet.Diagnostics.Logger;
using MQTTnet.Server;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace NRuuviTag.Mqtt.Tests;

[TestClass]
public class MqttPublishTests {

    private const ushort Port = 21883;

    // See https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-5-rawv2#case-valid-data
    private const string RawDataV2Valid = "0512FC5394C37C0004FFFC040CAC364200CDCBB8334C884F";

    private static MqttServer s_mqttServer = default!;

    private static ILoggerFactory s_loggerFactory = default!;

    private static event Action<MqttApplicationMessage>? MessageReceived;

    public TestContext TestContext { get; set; } = default!;


    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context) {
        s_loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Trace));

        s_mqttServer = CreateMqttServer(Port);

        s_mqttServer.InterceptingPublishAsync += args => {
            MessageReceived?.Invoke(args.ApplicationMessage);
            return Task.CompletedTask;
        };

        await s_mqttServer.StartAsync().ConfigureAwait(false);
    }


    [ClassCleanup]
    public static async Task ClassCleanup() {
        if (s_mqttServer == null) {
            return;
        }

        await s_mqttServer.StopAsync().ConfigureAwait(false);
        s_mqttServer.Dispose();
    }


    private static MqttServer CreateMqttServer(int port) {
        return new MqttServerFactory().CreateMqttServer(
            new MqttServerOptionsBuilder()
                .WithDefaultEndpointPort(port)
                .WithDefaultEndpoint()
                .Build(), 
            new TestLogger(s_loggerFactory.CreateLogger<MqttServer>())
        );
    }


    private static TestRuuviTagListener CreateListener() {
        return new TestRuuviTagListener(new RuuviTagListenerOptions(), new NullDeviceResolver());
    }
    

    [TestMethod]
    public async Task ShouldPublishSampleToSingleTopic() {
        var listener = CreateListener();

        var bridge = new MqttPublisher(listener, new MqttPublisherOptions() { 
            Hostname = "localhost:" + Port,
            ClientId = TestContext.TestName,
            ProtocolVersion = MQTTnet.Formatter.MqttProtocolVersion.V500,
            PublishType = PublishType.SingleTopic,
            TlsOptions = new MqttPublisherTlsOptions() {
                UseTls = false
            }
        }, new MqttClientFactory(), s_loggerFactory);

        var now = DateTimeOffset.Now;
        var signalStrength = -79;

        var sample = new RuuviTagSample(TestContext.TestName, now, signalStrength, RuuviTagUtilities.ParsePayload(Convert.FromHexString(RawDataV2Valid)));
        var expectedTopicName = bridge.GetTopicNameForSample(sample);

        var tcs = new TaskCompletionSource<MqttApplicationMessage?>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnMessageReceived(MqttApplicationMessage msg) {
            if (string.Equals(msg.Topic, expectedTopicName)) {
                tcs.TrySetResult(msg);
            }
        }

        try {
            MessageReceived += OnMessageReceived;
            using var ctSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            _ = bridge.RunAsync(ctSource.Token);
            listener.Publish(sample);

            _ = Task.Run(async () => { 
                try {
                    await Task.Delay(-1, ctSource.Token).ConfigureAwait(false);
                    tcs.TrySetResult(null);
                }
                catch (OperationCanceledException) {
                    tcs.TrySetCanceled();
                }
                catch (Exception e) {
                    tcs.TrySetException(e);
                }
            });

            var msg = await tcs.Task.ConfigureAwait(false);
            Assert.IsNotNull(msg);

            var json = msg.ConvertPayloadToString();
            var sampleFromMqtt = JsonSerializer.Deserialize<RuuviTagSample>(json, RuuviJsonSerializerContext.Default.RuuviTagSample);

            Assert.IsNotNull(sampleFromMqtt);
            Assert.AreEqual(sample, sampleFromMqtt);
        }
        finally {
            MessageReceived -= OnMessageReceived;
        }
    }


    [TestMethod]
    public async Task SingleTopicPublishShouldExcludeSpecifiedMeasurements() {
        var listener = CreateListener();

        var bridge = new MqttPublisher(listener, new MqttPublisherOptions() {
            Hostname = "localhost:" + Port,
            ClientId = TestContext.TestName,
            ProtocolVersion = MQTTnet.Formatter.MqttProtocolVersion.V500,
            PublishType = PublishType.SingleTopic,
            TlsOptions = new MqttPublisherTlsOptions() {
                UseTls = false
            },
            PrepareForPublish = s => s with { AccelerationX = null }
        }, new MqttClientFactory(), s_loggerFactory);

        var now = DateTimeOffset.Now;
        var signalStrength = -79;

        var sample = new RuuviTagSample(TestContext.TestName, now, signalStrength, RuuviTagUtilities.ParsePayload(Convert.FromHexString(RawDataV2Valid)));
        var expectedTopicName = bridge.GetTopicNameForSample(sample);

        var tcs = new TaskCompletionSource<MqttApplicationMessage?>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnMessageReceived(MqttApplicationMessage msg) {
            if (string.Equals(msg.Topic, expectedTopicName)) {
                tcs.TrySetResult(msg);
            }
        }

        try {
            MessageReceived += OnMessageReceived;
            using var ctSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            _ = bridge.RunAsync(ctSource.Token);
            listener.Publish(sample);

            _ = Task.Run(async () => {
                try {
                    await Task.Delay(-1, ctSource.Token).ConfigureAwait(false);
                    tcs.TrySetResult(null);
                }
                catch (OperationCanceledException) {
                    tcs.TrySetCanceled();
                }
                catch (Exception e) {
                    tcs.TrySetException(e);
                }
            });

            var msg = await tcs.Task.ConfigureAwait(false);
            Assert.IsNotNull(msg);

            var json = msg.ConvertPayloadToString();
            var sampleFromMqtt = JsonSerializer.Deserialize<RuuviTagSample>(json, RuuviJsonSerializerContext.Default.RuuviTagSample);

            Assert.IsNotNull(sampleFromMqtt);
            Assert.AreEqual(sample with { AccelerationX = null }, sampleFromMqtt);
        }
        finally {
            MessageReceived -= OnMessageReceived;
        }
    }


    [TestMethod]
    public async Task ShouldPublishSampleToMultipleTopics() {
        var listener = CreateListener();

        var bridge = new MqttPublisher(listener, new MqttPublisherOptions() {
            Hostname = "localhost:" + Port,
            ClientId = TestContext.TestName,
            ProtocolVersion = MQTTnet.Formatter.MqttProtocolVersion.V500,
            PublishType = PublishType.TopicPerMeasurement,
            TlsOptions = new MqttPublisherTlsOptions() {
                UseTls = false
            }
        }, new MqttClientFactory(), s_loggerFactory);

        var now = DateTimeOffset.Now;
        var signalStrength = -79;
        var sample = new RuuviTagSample(TestContext.TestName, now, signalStrength, RuuviTagUtilities.ParsePayload(Convert.FromHexString(RawDataV2Valid)));
            
        var expectedTopicPrefix = bridge.GetTopicNameForSample(sample);
        const int expectedMessageCount = 15;

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receivedMessages = new List<MqttApplicationMessage>();

        try {
            MessageReceived += OnMessageReceived;
            using var ctSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                
            _ = bridge.RunAsync(ctSource.Token);
            listener.Publish(sample);

            _ = Task.Run(async () => {
                try {
                    await Task.Delay(-1, ctSource.Token).ConfigureAwait(false);
                    tcs.TrySetResult(false);
                }
                catch (OperationCanceledException) {
                    tcs.TrySetCanceled();
                }
                catch (Exception e) {
                    tcs.TrySetException(e);
                }
            });

            var success = await tcs.Task.ConfigureAwait(false);
            Assert.IsTrue(success);

            Assert.HasCount(expectedMessageCount, receivedMessages);

            var sampleFromMqtt = new RuuviTagSample();
                
            foreach (var msg in receivedMessages) {
                var json = msg.ConvertPayloadToString();
                var fieldName = msg.Topic[expectedTopicPrefix.Length..];

                switch (fieldName) {
                    case "/acceleration-x":
                        sampleFromMqtt = sampleFromMqtt with {
                            AccelerationX = JsonSerializer.Deserialize<double>(json)
                        };
                        break;
                    case "/acceleration-y":
                        sampleFromMqtt = sampleFromMqtt with {
                            AccelerationY = JsonSerializer.Deserialize<double>(json)
                        };
                        break;
                    case "/acceleration-z":
                        sampleFromMqtt = sampleFromMqtt with {
                            AccelerationZ = JsonSerializer.Deserialize<double>(json)
                        };
                        break;
                    case "/battery-voltage":
                        sampleFromMqtt = sampleFromMqtt with {
                            BatteryVoltage = JsonSerializer.Deserialize<double>(json)
                        };
                        break;
                    case "/data-format":
                        sampleFromMqtt = sampleFromMqtt with {
                            DataFormat = JsonSerializer.Deserialize<byte>(json)
                        };
                        break;
                    case "/device-id":
                        sampleFromMqtt = sampleFromMqtt with {
                            DeviceId = JsonSerializer.Deserialize<string>(json)
                        };
                        break;
                    case "/humidity":
                        sampleFromMqtt = sampleFromMqtt with {
                            Humidity = JsonSerializer.Deserialize<double>(json)
                        };
                        break;
                    case "/mac-address":
                        sampleFromMqtt = sampleFromMqtt with {
                            MacAddress = JsonSerializer.Deserialize<string>(json)
                        };
                        break;
                    case "/measurement-sequence":
                        sampleFromMqtt = sampleFromMqtt with {
                            MeasurementSequence = JsonSerializer.Deserialize<uint>(json)
                        };
                        break;
                    case "/movement-counter":
                        sampleFromMqtt = sampleFromMqtt with {
                            MovementCounter = JsonSerializer.Deserialize<byte>(json)
                        };
                        break;
                    case "/pressure":
                        sampleFromMqtt = sampleFromMqtt with {
                            Pressure = JsonSerializer.Deserialize<double>(json)
                        };
                        break;
                    case "/signal-strength":
                        sampleFromMqtt = sampleFromMqtt with {
                            SignalStrength = JsonSerializer.Deserialize<double>(json)
                        };
                        break;
                    case "/temperature":
                        sampleFromMqtt = sampleFromMqtt with {
                            Temperature = JsonSerializer.Deserialize<double>(json)
                        };
                        break;
                    case "/timestamp":
                        sampleFromMqtt = sampleFromMqtt with {
                            Timestamp = JsonSerializer.Deserialize<DateTimeOffset>(json)
                        };
                        break;
                    case "/tx-power":
                        sampleFromMqtt = sampleFromMqtt with {
                            TxPower = JsonSerializer.Deserialize<double>(json)
                        };
                        break;
                    default:
                        Assert.Fail("Unexpected field: " + fieldName);
                        break;
                }
            }

            Assert.AreEqual(sample, sampleFromMqtt);
        }
        finally {
            MessageReceived -= OnMessageReceived;
        }

        return;

        void OnMessageReceived(MqttApplicationMessage msg) {
            if (!msg.Topic.StartsWith(expectedTopicPrefix!)) {
                return;
            }

            lock (receivedMessages) {
                receivedMessages.Add(msg);
                if (receivedMessages.Count >= expectedMessageCount) {
                    tcs.TrySetResult(true);
                }
            }
        }
    }


    [TestMethod]
    public async Task MultipleTopicPublishShouldExcludeSpecifiedMeasurements() {
        var listener = CreateListener();

        var bridge = new MqttPublisher(listener, new MqttPublisherOptions() {
            Hostname = "localhost:" + Port,
            ClientId = TestContext.TestName,
            ProtocolVersion = MQTTnet.Formatter.MqttProtocolVersion.V500,
            PublishType = PublishType.TopicPerMeasurement,
            TlsOptions = new MqttPublisherTlsOptions() {
                UseTls = false
            },
            PrepareForPublish = s => s with { AccelerationX = null, AccelerationY = null }
        }, new MqttClientFactory(), s_loggerFactory);

        var now = DateTimeOffset.Now;
        var signalStrength = -79;
        var sample = new RuuviTagSample(TestContext.TestName, now, signalStrength, RuuviTagUtilities.ParsePayload(Convert.FromHexString(RawDataV2Valid)));
            
        var expectedTopicPrefix = bridge.GetTopicNameForSample(sample);
        const int expectedMessageCount = 13;

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receivedMessages = new List<MqttApplicationMessage>();

        try {
            MessageReceived += OnMessageReceived;
            using var ctSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            _ = bridge.RunAsync(ctSource.Token);
            listener.Publish(sample);

            _ = Task.Run(async () => {
                try {
                    await Task.Delay(-1, ctSource.Token).ConfigureAwait(false);
                    tcs.TrySetResult(false);
                }
                catch (OperationCanceledException) {
                    tcs.TrySetCanceled();
                }
                catch (Exception e) {
                    tcs.TrySetException(e);
                }
            });

            var success = await tcs.Task.ConfigureAwait(false);
            Assert.IsTrue(success);

            Assert.HasCount(expectedMessageCount, receivedMessages);

            var sampleFromMqtt = new RuuviTagSample();
                
            foreach (var msg in receivedMessages) {
                var json = msg.ConvertPayloadToString();
                var fieldName = msg.Topic[expectedTopicPrefix.Length..];

                switch (fieldName) {
                    case "/acceleration-x":
                        sampleFromMqtt = sampleFromMqtt with {
                            AccelerationX = JsonSerializer.Deserialize<double>(json)
                        };
                        break;
                    case "/acceleration-y":
                        sampleFromMqtt = sampleFromMqtt with {
                            AccelerationY = JsonSerializer.Deserialize<double>(json)
                        };
                        break;
                    case "/acceleration-z":
                        sampleFromMqtt = sampleFromMqtt with {
                            AccelerationZ = JsonSerializer.Deserialize<double>(json)
                        };
                        break;
                    case "/battery-voltage":
                        sampleFromMqtt = sampleFromMqtt with {
                            BatteryVoltage = JsonSerializer.Deserialize<double>(json)
                        };
                        break;
                    case "/data-format":
                        sampleFromMqtt = sampleFromMqtt with {
                            DataFormat = JsonSerializer.Deserialize<byte>(json)
                        };
                        break;
                    case "/device-id":
                        sampleFromMqtt = sampleFromMqtt with {
                            DeviceId = JsonSerializer.Deserialize<string>(json)
                        };
                        break;
                    case "/humidity":
                        sampleFromMqtt = sampleFromMqtt with {
                            Humidity = JsonSerializer.Deserialize<double>(json)
                        };
                        break;
                    case "/mac-address":
                        sampleFromMqtt = sampleFromMqtt with {
                            MacAddress = JsonSerializer.Deserialize<string>(json)
                        };
                        break;
                    case "/measurement-sequence":
                        sampleFromMqtt = sampleFromMqtt with {
                            MeasurementSequence = JsonSerializer.Deserialize<ushort>(json)
                        };
                        break;
                    case "/movement-counter":
                        sampleFromMqtt = sampleFromMqtt with {
                            MovementCounter = JsonSerializer.Deserialize<byte>(json)
                        };
                        break;
                    case "/pressure":
                        sampleFromMqtt = sampleFromMqtt with {
                            Pressure = JsonSerializer.Deserialize<double>(json)
                        };
                        break;
                    case "/signal-strength":
                        sampleFromMqtt = sampleFromMqtt with {
                            SignalStrength = JsonSerializer.Deserialize<double>(json)
                        };
                        break;
                    case "/temperature":
                        sampleFromMqtt = sampleFromMqtt with {
                            Temperature = JsonSerializer.Deserialize<double>(json)
                        };
                        break;
                    case "/timestamp":
                        sampleFromMqtt = sampleFromMqtt with {
                            Timestamp = JsonSerializer.Deserialize<DateTimeOffset>(json)
                        };
                        break;
                    case "/tx-power":
                        sampleFromMqtt = sampleFromMqtt with {
                            TxPower = JsonSerializer.Deserialize<double>(json)
                        };
                        break;
                    default:
                        Assert.Fail("Unexpected field: " + fieldName);
                        break;
                }
            }

            Assert.AreEqual(sample with { AccelerationX = null, AccelerationY = null }, sampleFromMqtt);
        }
        finally {
            MessageReceived -= OnMessageReceived;
        }

        return;

        void OnMessageReceived(MqttApplicationMessage msg) {
            if (!msg.Topic.StartsWith(expectedTopicPrefix!)) {
                return;
            }

            lock (receivedMessages) {
                receivedMessages.Add(msg);
                if (receivedMessages.Count >= expectedMessageCount) {
                    tcs.TrySetResult(true);
                }
            }
        }
    }


    [TestMethod]
    public async Task ShouldEnqueueMessagesWhileDisconnected() {
        var port = Port + 1;
        using var server = CreateMqttServer(port);
        
        var listener = CreateListener();

        var bridge = new MqttPublisher(listener, new MqttPublisherOptions() {
            Hostname = "localhost:" + port,
            ClientId = TestContext.TestName,
            ProtocolVersion = MQTTnet.Formatter.MqttProtocolVersion.V500,
            PublishType = PublishType.SingleTopic,
            TlsOptions = new MqttPublisherTlsOptions() {
                UseTls = false
            }
        }, new MqttClientFactory(), s_loggerFactory);

        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bridge.MqttClient.ConnectedAsync += args => {
            connected.TrySetResult();
            return Task.CompletedTask;
        };
        
        using var ctSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        _ = bridge.RunAsync(ctSource.Token);
        
        var now = DateTimeOffset.Now;
        var signalStrength = -79;

        var sample = new RuuviTagSample(TestContext.TestName, now, signalStrength, RuuviTagUtilities.ParsePayload(Convert.FromHexString(RawDataV2Valid)));
        var expectedTopicName = bridge.GetTopicNameForSample(sample);
        await listener.PublishAsync(sample);

        var received = new TaskCompletionSource<RuuviTagSample>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.InterceptingPublishAsync += args => {
            if (!string.Equals(args.ApplicationMessage.Topic, expectedTopicName, StringComparison.Ordinal)) {
                return Task.CompletedTask;
            }
            
            var json = args.ApplicationMessage.ConvertPayloadToString();
            received.TrySetResult(JsonSerializer.Deserialize<RuuviTagSample>(json, RuuviJsonSerializerContext.Default.RuuviTagSample)!);
            
            return Task.CompletedTask;
        };

        await server.StartAsync();
        await connected.Task;
        
        var sampleFromMqtt = await received.Task.WaitAsync(ctSource.Token);
        
        Assert.IsNotNull(sampleFromMqtt);
        Assert.AreEqual(sample, sampleFromMqtt);
    }


    private class TestLogger : IMqttNetLogger {

        private readonly ILogger _logger;

        public bool IsEnabled { get; set; } = true;


        public TestLogger(ILogger logger) {
            _logger = logger;
        }


        public void Publish(MqttNetLogLevel logLevel, string source, string message, object[] parameters, Exception exception) {
            var msLogLevel = logLevel switch {
                MqttNetLogLevel.Error => LogLevel.Error,
                MqttNetLogLevel.Warning => LogLevel.Warning,
                MqttNetLogLevel.Info => LogLevel.Information,
                _ => LogLevel.Debug
            };

            _logger.Log(msLogLevel, exception, message, parameters);
        }
    }

}
