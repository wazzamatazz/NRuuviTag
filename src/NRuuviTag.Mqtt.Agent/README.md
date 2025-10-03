# About

NRuuviTag.Mqtt.Agent allows NRuuviTag to publish Ruuvi sensor data to an MQTT broker.

More detailed documentation can be found on [GitHub](https://github.com/wazzamatazz/NRuuviTag).


# How to Use

Use the `AzureEventHubAgent` class to publish samples received from an `IRuuviTagListener` implementation to an MQTT broker:

```csharp
public async Task RunMqttAgent( 
    IRuuviTagListener listener,
    ILoggerFactory? loggerFactory = null,
    CancellationToken cancellationToken = default
) { 
    var agentOptions = new MqttAgentOptions() { 
        Hostname = "my-mqtt-service.local:1883",
        ClientId = "MY_CLIENT_ID"
    };
    
    await using var agent = new MqttAgent(
        listener, 
        agentOptions, 
        new MQTTnet.MqttFactory(), 
        loggerFactory?.CreateLogger<MqttAgent>());
    
    await agent.RunAsync(cancellationToken);
}
```
