# About

NRuuviTag.Http.Publisher allows NRuuviTag to publish Ruuvi sensor data to an HTTP endpoint.

More detailed documentation can be found on [GitHub](https://github.com/wazzamatazz/NRuuviTag).


# How to Use

Use the `HttpPublisher` class to publish samples received from an `IRuuviTagListener` implementation to an HTTP endpoint:

```csharp
public async Task RunHttpPublisherAsync( 
    IRuuviTagListener listener,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory? loggerFactory = null,
    CancellationToken cancellationToken = default
) { 
    var options = new HttpPublisherOptions() { 
        Endpoint = "https://my-receiver.local",
        Headers = new Dictionary<string, string>() { 
            ["X-API-Key"] = "MY_API_KEY"
        }
    };
    
    await using var publisher = new HttpPublisher(
        listener, 
        options, 
        httpClientFactory, 
        loggerFactory?.CreateLogger<HttpPublisher>());
    
    await publisher.RunAsync(cancellationToken);
}
```

In addition to specifying the endpoint URL and request headers, you can use the `HttpPublisherOptions` to control whether HTTP POST or PUT is used, and the maximum number of samples to send in a single request.


# HTTP Client Resiliency

The publisher uses `IHttpClientFactory` to create `HttpClient` instances. It is recommended to configure the factory to use a resilient HTTP client, for example by using [Microsoft.Extensions.Http.Resilience](https://www.nuget.org/packages/Microsoft.Extensions.Http.Resilience).
