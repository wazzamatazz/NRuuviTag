# About

NRuuviTag.Listener.Windows allows NRuuviTag to listen for Bluetooth LE advertisements from Ruuvi devices on Windows.

More detailed documentation can be found on [GitHub](https://github.com/wazzamatazz/NRuuviTag).


# How to Use

Use the `WindowsSdkListener` class to listen for Ruuvi sensor data on a Windows system using the Windows SDK:

```csharp
IRuuviTagListener client = new WindowsSdkListener();

await foreach (var sample in client.ListenAsync(cancellationToken)) {
    // sample is a RuuviTagSample object.
}
```
