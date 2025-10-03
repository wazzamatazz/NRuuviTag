# About

NRuuviTag.OpenTelemetry allows NRuuviTag instrumentation to be registered with an OpenTelemetry builder.

More detailed documentation can be found on [GitHub](https://github.com/wazzamatazz/NRuuviTag).


# How to Use

Add NRuuviTag metrics to an OpenTelemetry builder using the `AddRuuviTagInstrumentation` extension method:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddNRuuviTagInstrumentation());
```
