# NRuuviTag

A collection of libraries to simplify interacting with RuuviTag IoT sensors from [Ruuvi](https://www.ruuvi.com/).

The repository consists of a [core library](/src/NRuuviTag.Core) that defines common types, and a [listener implementation](/src/NRuuviTag.Client.Windows) that uses the Windows 10 SDK to observe the Bluetooth LE advertisements emitted by RuuviTag devices.


# Building the Solution

The repository uses [Cake](https://cakebuild.net/) for cross-platform build automation. The build script allows for metadata such as a build counter to be specified when called by a continuous integration system such as TeamCity.

A build can be run from the command line using the [build.ps1](/build.ps1) PowerShell script. For documentation about the available build script parameters, see [build.cake](/build.cake).


# Example Usage

> See the [samples](/samples) folder for more details examples of usage.

Usage is very straightforward. For example, to listen using the Windows SDK:

```csharp
var client = new WindowsSdkListener();

await foreach (var sample in client.ListenAsync(cancellationToken)) {
    // sample is a RuuviTagSample object.
}
```

To only observe specific RuuviTag devices using the Windows SDK using MAC address filtering:

```csharp
var client = new WindowsSdkListener();

bool CanProcessMessage(string macAddress) {
    return string.Equals(macAddress, "AB:CD:EF:01:23:45");
}

await foreach (var sample in client.ListenAsync(CanProcessMessage, cancellationToken)) {
    // sample is a RuuviTagSample object.
}
```
