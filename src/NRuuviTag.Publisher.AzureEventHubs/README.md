# About

NRuuviTag.AzureEventHubs.Publisher allows NRuuviTag to publish Ruuvi sensor data to an [Azure Event Hub](https://azure.microsoft.com/en-us/services/event-hubs/).

More detailed documentation can be found on [GitHub](https://github.com/wazzamatazz/NRuuviTag).


# How to Use

Use the `AzureEventHubPublisher` class to publish samples received from an `IRuuviTagListener` implementation to an Azure Event Hub:

```csharp
public async Task RunAzureEventHubPublisherAsync(
    IRuuviTagListener listener,
    ILoggerFactory? loggerFactory = null,
    CancellationToken cancellationToken = default
) {
    var options = new AzureEventHubPublisherOptions() {
        ConnectionString = "Endpoint=sb://MY_NAMESPACE.servicebus.windows.net/;SharedAccessKeyName=MY_KEY_NAME;SharedAccessKey=MY_KEY",
        EventHubName = "MY_EVENT_HUB"
    };
    
    await using var publisher = new AzureEventHubPublisher(
        listener, 
        options, 
        loggerFactory?.CreateLogger<AzureEventHubPublisher>());
    
    await publisher.RunAsync(cancellationToken);
}
```
