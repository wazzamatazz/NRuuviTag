# About

NRuuviTag.Mqtt.Publisher allows NRuuviTag to publish Ruuvi sensor data to an MQTT broker.

More detailed documentation can be found on [GitHub](https://github.com/wazzamatazz/NRuuviTag).


# How to Use

Use the `MqttPublisher` class to publish samples received from an `IRuuviTagListener` implementation to an MQTT broker:

```csharp
public async Task RunMqttPublisherAsync( 
    IRuuviTagListener listener,
    ILoggerFactory? loggerFactory = null,
    CancellationToken cancellationToken = default
) { 
    var options = new MqttPublisherOptions() { 
        Hostname = "my-mqtt-service.local:1883",
        ClientId = "MY_CLIENT_ID"
    };
    
    await using var publisher = new MqttPublisher(
        listener, 
        options, 
        new MQTTnet.MqttFactory(), 
        loggerFactory?.CreateLogger<MqttPublisher>());
    
    await publisher.RunAsync(cancellationToken);
}
```
