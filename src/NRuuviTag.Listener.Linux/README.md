# About

NRuuviTag.Listener.Linux allows NRuuviTag to listen for Bluetooth LE advertisements from Ruuvi devices on Linux using BlueZ.

More detailed documentation can be found on [GitHub](https://github.com/wazzamatazz/NRuuviTag).


# How to Use

Use the `BlueZListener` class to listen for Ruuvi sensor data on a Linux system with BlueZ installed:

```csharp
IRuuviTagListener client = new BlueZListener(new BlueZListenerOptions() {
    AdapterName = "hci0" // Optional, defaults to "hci0"
});

await foreach (var sample in client.ListenAsync(cancellationToken)) {
    // sample is a RuuviTagSample object.
}
```
