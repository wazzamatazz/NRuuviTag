using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using MQTTnet;
using MQTTnet.Server;

namespace NRuuviTag.Mqtt.Tests {

    [TestClass]
    public class MqttPublishTests {

        private const ushort Port = 21883;

        /// <summary>
        /// Raw RuuviTag payload taken from https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-5-rawv2#case-valid-data
        /// </summary>
        private static readonly byte[] s_rawPayload = {
            0x05, 0x12, 0xFC, 0x53, 0x94, 0xC3, 0x7C, 0x00,
            0x04, 0xFF, 0xFC, 0x04, 0x0C, 0xAC, 0x36, 0x42,
            0x00, 0xCD, 0xCB, 0xB8, 0x33, 0x4C, 0x88, 0x4F
        };

        private static IMqttServer s_mqttServer = default!;

        private static event Action<MqttApplicationMessageReceivedEventArgs>? MessageReceived;

        public TestContext TestContext { get; set; } = default!;


        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context) {
            s_mqttServer = new MqttFactory().CreateMqttServer();
            s_mqttServer.UseApplicationMessageReceivedHandler(args => MessageReceived?.Invoke(args));

            var optionsBuilder = new MqttServerOptionsBuilder()
                .WithDefaultEndpointPort(Port);

            await s_mqttServer.StartAsync(optionsBuilder.Build()).ConfigureAwait(false);
        }


        [ClassCleanup]
        public static async Task ClassCleanup() {
            if (s_mqttServer == null) {
                return;
            }

            await s_mqttServer.StopAsync().ConfigureAwait(false);
            s_mqttServer.Dispose();
        }


        [TestMethod]
        public async Task ShouldPublishSampleToSingleTopic() {
            var listener = new TestRuuviTagListener();

            var bridge = new MqttAgent(listener, new MqttAgentOptions() { 
                Hostname = "localhost:" + Port,
                ClientId = TestContext.TestName,
                ProtocolVersion = MQTTnet.Formatter.MqttProtocolVersion.V500,
                PublishType = PublishType.SingleTopic,
                TlsOptions = new MqttAgentTlsOptions() {
                    UseTls = false
                }
            }, new MqttFactory());

            var now = DateTimeOffset.Now;
            var signalStrength = -79;

            var sample = RuuviTagUtilities.CreateSampleFromPayload(now, signalStrength, s_rawPayload);
            var expectedTopicName = bridge.GetTopicNameForSample(sample);

            var tcs = new TaskCompletionSource<MqttApplicationMessage?>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnMessageReceived(MqttApplicationMessageReceivedEventArgs args) {
                if (string.Equals(args.ApplicationMessage.Topic, expectedTopicName)) {
                    tcs.TrySetResult(args.ApplicationMessage);
                }
            }

            try {
                MessageReceived += OnMessageReceived;
                using (var ctSource = new CancellationTokenSource(TimeSpan.FromSeconds(15))) {
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
                    var sampleFromMqtt = JsonSerializer.Deserialize<RuuviTagSample>(json, new JsonSerializerOptions() { 
                        PropertyNameCaseInsensitive = true 
                    });

                    Assert.IsNotNull(sampleFromMqtt);
                    Assert.AreEqual(sample.AccelerationX, sampleFromMqtt.AccelerationX);
                    Assert.AreEqual(sample.AccelerationY, sampleFromMqtt.AccelerationY);
                    Assert.AreEqual(sample.AccelerationZ, sampleFromMqtt.AccelerationZ);
                    Assert.AreEqual(sample.BatteryVoltage, sampleFromMqtt.BatteryVoltage);
                    Assert.AreEqual(sample.Humidity, sampleFromMqtt.Humidity);
                    Assert.AreEqual(sample.MeasurementSequence, sampleFromMqtt.MeasurementSequence);
                    Assert.AreEqual(sample.MovementCounter, sampleFromMqtt.MovementCounter);
                    Assert.AreEqual(sample.Pressure, sampleFromMqtt.Pressure);
                    Assert.AreEqual(sample.SignalStrength, sampleFromMqtt.SignalStrength);
                    Assert.AreEqual(sample.Temperature, sampleFromMqtt.Temperature);
                    Assert.AreEqual(sample.Timestamp, sampleFromMqtt.Timestamp);
                    Assert.AreEqual(sample.TxPower, sampleFromMqtt.TxPower);
                }
            }
            finally {
                MessageReceived -= OnMessageReceived;
            }
        }


        [TestMethod]
        public async Task SingleTopicPublishShouldExcludeSpecifiedMeasurements() {
            var listener = new TestRuuviTagListener();

            var bridge = new MqttAgent(listener, new MqttAgentOptions() {
                Hostname = "localhost:" + Port,
                ClientId = TestContext.TestName,
                ProtocolVersion = MQTTnet.Formatter.MqttProtocolVersion.V500,
                PublishType = PublishType.SingleTopic,
                TlsOptions = new MqttAgentTlsOptions() {
                    UseTls = false
                },
                PrepareForPublish = s => {
                    s.AccelerationX = null;
                }
            }, new MqttFactory());

            var now = DateTimeOffset.Now;
            var signalStrength = -79;

            var sample = RuuviTagUtilities.CreateSampleFromPayload(now, signalStrength, s_rawPayload);
            var expectedTopicName = bridge.GetTopicNameForSample(sample);

            var tcs = new TaskCompletionSource<MqttApplicationMessage?>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnMessageReceived(MqttApplicationMessageReceivedEventArgs args) {
                if (string.Equals(args.ApplicationMessage.Topic, expectedTopicName)) {
                    tcs.TrySetResult(args.ApplicationMessage);
                }
            }

            try {
                MessageReceived += OnMessageReceived;
                using (var ctSource = new CancellationTokenSource(TimeSpan.FromSeconds(15))) {
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
                    var sampleFromMqtt = JsonSerializer.Deserialize<RuuviTagSample>(json, new JsonSerializerOptions() {
                        PropertyNameCaseInsensitive = true
                    });

                    Assert.IsNotNull(sampleFromMqtt);
                    Assert.IsNull(sampleFromMqtt.AccelerationX);
                    Assert.AreEqual(sample.AccelerationY, sampleFromMqtt.AccelerationY);
                    Assert.AreEqual(sample.AccelerationZ, sampleFromMqtt.AccelerationZ);
                    Assert.AreEqual(sample.BatteryVoltage, sampleFromMqtt.BatteryVoltage);
                    Assert.AreEqual(sample.Humidity, sampleFromMqtt.Humidity);
                    Assert.AreEqual(sample.MeasurementSequence, sampleFromMqtt.MeasurementSequence);
                    Assert.AreEqual(sample.MovementCounter, sampleFromMqtt.MovementCounter);
                    Assert.AreEqual(sample.Pressure, sampleFromMqtt.Pressure);
                    Assert.AreEqual(sample.SignalStrength, sampleFromMqtt.SignalStrength);
                    Assert.AreEqual(sample.Temperature, sampleFromMqtt.Temperature);
                    Assert.AreEqual(sample.Timestamp, sampleFromMqtt.Timestamp);
                    Assert.AreEqual(sample.TxPower, sampleFromMqtt.TxPower);
                }
            }
            finally {
                MessageReceived -= OnMessageReceived;
            }
        }


        [TestMethod]
        public async Task ShouldPublishSampleToMultipleTopics() {
            var listener = new TestRuuviTagListener();

            var bridge = new MqttAgent(listener, new MqttAgentOptions() {
                Hostname = "localhost:" + Port,
                ClientId = TestContext.TestName,
                ProtocolVersion = MQTTnet.Formatter.MqttProtocolVersion.V500,
                PublishType = PublishType.TopicPerMeasurement,
                TlsOptions = new MqttAgentTlsOptions() {
                    UseTls = false
                }
            }, new MqttFactory());

            var now = DateTimeOffset.Now;
            var signalStrength = -79;

            var sample = RuuviTagUtilities.CreateSampleFromPayload(now, signalStrength, s_rawPayload);
            var expectedTopicPrefix = bridge.GetTopicNameForSample(sample);
            const int expectedMessageCount = 12;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var receivedMessages = new List<MqttApplicationMessage>();

            void OnMessageReceived(MqttApplicationMessageReceivedEventArgs args) {
                if (args.ApplicationMessage.Topic.StartsWith(expectedTopicPrefix!)) {
                    lock (receivedMessages) {
                        receivedMessages.Add(args.ApplicationMessage);
                        if (receivedMessages.Count >= expectedMessageCount) {
                            tcs.TrySetResult(true);
                        }
                    }
                }
            }

            try {
                MessageReceived += OnMessageReceived;
                using (var ctSource = new CancellationTokenSource(TimeSpan.FromSeconds(15))) {
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

                    Assert.AreEqual(expectedMessageCount, receivedMessages.Count);

                    var sampleFromMqtt = new RuuviTagSample();
                    foreach (var msg in receivedMessages) {
                        var json = msg.ConvertPayloadToString();
                        var fieldName = msg.Topic.Substring(expectedTopicPrefix.Length);

                        switch (fieldName) {
                            case "/acceleration-x":
                                sampleFromMqtt.AccelerationX = JsonSerializer.Deserialize<double>(json);
                                break;
                            case "/acceleration-y":
                                sampleFromMqtt.AccelerationY = JsonSerializer.Deserialize<double>(json);
                                break;
                            case "/acceleration-z":
                                sampleFromMqtt.AccelerationZ = JsonSerializer.Deserialize<double>(json);
                                break;
                            case "/battery-voltage":
                                sampleFromMqtt.BatteryVoltage = JsonSerializer.Deserialize<double>(json);
                                break;
                            case "/humidity":
                                sampleFromMqtt.Humidity = JsonSerializer.Deserialize<double>(json);
                                break;
                            case "/measurement-sequence":
                                sampleFromMqtt.MeasurementSequence = JsonSerializer.Deserialize<ushort>(json);
                                break;
                            case "/movement-counter":
                                sampleFromMqtt.MovementCounter = JsonSerializer.Deserialize<byte>(json);
                                break;
                            case "/pressure":
                                sampleFromMqtt.Pressure = JsonSerializer.Deserialize<double>(json);
                                break;
                            case "/signal-strength":
                                sampleFromMqtt.SignalStrength = JsonSerializer.Deserialize<double>(json);
                                break;
                            case "/temperature":
                                sampleFromMqtt.Temperature = JsonSerializer.Deserialize<double>(json);
                                break;
                            case "/timestamp":
                                sampleFromMqtt.Timestamp = JsonSerializer.Deserialize<DateTimeOffset>(json);
                                break;
                            case "/tx-power":
                                sampleFromMqtt.TxPower = JsonSerializer.Deserialize<double>(json);
                                break;
                        }
                    }

                    Assert.AreEqual(sample.AccelerationX, sampleFromMqtt.AccelerationX);
                    Assert.AreEqual(sample.AccelerationY, sampleFromMqtt.AccelerationY);
                    Assert.AreEqual(sample.AccelerationZ, sampleFromMqtt.AccelerationZ);
                    Assert.AreEqual(sample.BatteryVoltage, sampleFromMqtt.BatteryVoltage);
                    Assert.AreEqual(sample.Humidity, sampleFromMqtt.Humidity);
                    Assert.AreEqual(sample.MeasurementSequence, sampleFromMqtt.MeasurementSequence);
                    Assert.AreEqual(sample.MovementCounter, sampleFromMqtt.MovementCounter);
                    Assert.AreEqual(sample.Pressure, sampleFromMqtt.Pressure);
                    Assert.AreEqual(sample.SignalStrength, sampleFromMqtt.SignalStrength);
                    Assert.AreEqual(sample.Timestamp, sampleFromMqtt.Timestamp);
                    Assert.AreEqual(sample.Temperature, sampleFromMqtt.Temperature);
                    Assert.AreEqual(sample.TxPower, sampleFromMqtt.TxPower);
                }
            }
            finally {
                MessageReceived -= OnMessageReceived;
            }
        }


        [TestMethod]
        public async Task MultipleTopicPublishShouldExcludeSpecifiedMeasurements() {
            var listener = new TestRuuviTagListener();

            var bridge = new MqttAgent(listener, new MqttAgentOptions() {
                Hostname = "localhost:" + Port,
                ClientId = TestContext.TestName,
                ProtocolVersion = MQTTnet.Formatter.MqttProtocolVersion.V500,
                PublishType = PublishType.TopicPerMeasurement,
                TlsOptions = new MqttAgentTlsOptions() {
                    UseTls = false
                },
                PrepareForPublish = s => {
                    s.AccelerationX = null;
                }
            }, new MqttFactory());

            var now = DateTimeOffset.Now;
            var signalStrength = -79;

            var sample = RuuviTagUtilities.CreateSampleFromPayload(now, signalStrength, s_rawPayload);
            var expectedTopicPrefix = bridge.GetTopicNameForSample(sample);
            const int expectedMessageCount = 11;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var receivedMessages = new List<MqttApplicationMessage>();

            void OnMessageReceived(MqttApplicationMessageReceivedEventArgs args) {
                if (args.ApplicationMessage.Topic.StartsWith(expectedTopicPrefix!)) {
                    lock (receivedMessages) {
                        receivedMessages.Add(args.ApplicationMessage);
                        if (receivedMessages.Count >= expectedMessageCount) {
                            tcs.TrySetResult(true);
                        }
                    }
                }
            }

            try {
                MessageReceived += OnMessageReceived;
                using (var ctSource = new CancellationTokenSource(TimeSpan.FromSeconds(15))) {
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

                    Assert.AreEqual(expectedMessageCount, receivedMessages.Count);

                    var sampleFromMqtt = new RuuviTagSample();
                    foreach (var msg in receivedMessages) {
                        var json = msg.ConvertPayloadToString();
                        var fieldName = msg.Topic.Substring(expectedTopicPrefix.Length);

                        switch (fieldName) {
                            case "/acceleration-x":
                                sampleFromMqtt.AccelerationX = JsonSerializer.Deserialize<double>(json);
                                break;
                            case "/acceleration-y":
                                sampleFromMqtt.AccelerationY = JsonSerializer.Deserialize<double>(json);
                                break;
                            case "/acceleration-z":
                                sampleFromMqtt.AccelerationZ = JsonSerializer.Deserialize<double>(json);
                                break;
                            case "/battery-voltage":
                                sampleFromMqtt.BatteryVoltage = JsonSerializer.Deserialize<double>(json);
                                break;
                            case "/humidity":
                                sampleFromMqtt.Humidity = JsonSerializer.Deserialize<double>(json);
                                break;
                            case "/measurement-sequence":
                                sampleFromMqtt.MeasurementSequence = JsonSerializer.Deserialize<ushort>(json);
                                break;
                            case "/movement-counter":
                                sampleFromMqtt.MovementCounter = JsonSerializer.Deserialize<byte>(json);
                                break;
                            case "/pressure":
                                sampleFromMqtt.Pressure = JsonSerializer.Deserialize<double>(json);
                                break;
                            case "/signal-strength":
                                sampleFromMqtt.SignalStrength = JsonSerializer.Deserialize<double>(json);
                                break;
                            case "/temperature":
                                sampleFromMqtt.Temperature = JsonSerializer.Deserialize<double>(json);
                                break;
                            case "/timestamp":
                                sampleFromMqtt.Timestamp = JsonSerializer.Deserialize<DateTimeOffset>(json);
                                break;
                            case "/tx-power":
                                sampleFromMqtt.TxPower = JsonSerializer.Deserialize<double>(json);
                                break;
                        }
                    }

                    Assert.IsNull(sampleFromMqtt.AccelerationX);
                    Assert.AreEqual(sample.AccelerationY, sampleFromMqtt.AccelerationY);
                    Assert.AreEqual(sample.AccelerationZ, sampleFromMqtt.AccelerationZ);
                    Assert.AreEqual(sample.BatteryVoltage, sampleFromMqtt.BatteryVoltage);
                    Assert.AreEqual(sample.Humidity, sampleFromMqtt.Humidity);
                    Assert.AreEqual(sample.MeasurementSequence, sampleFromMqtt.MeasurementSequence);
                    Assert.AreEqual(sample.MovementCounter, sampleFromMqtt.MovementCounter);
                    Assert.AreEqual(sample.Pressure, sampleFromMqtt.Pressure);
                    Assert.AreEqual(sample.SignalStrength, sampleFromMqtt.SignalStrength);
                    Assert.AreEqual(sample.Temperature, sampleFromMqtt.Temperature);
                    Assert.AreEqual(sample.Timestamp, sampleFromMqtt.Timestamp);
                    Assert.AreEqual(sample.TxPower, sampleFromMqtt.TxPower);
                }
            }
            finally {
                MessageReceived -= OnMessageReceived;
            }
        }

    }

}
