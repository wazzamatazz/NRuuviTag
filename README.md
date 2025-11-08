# NRuuviTag

A collection of .NET libraries to simplify interacting with RuuviTag IoT sensors from [Ruuvi](https://www.ruuvi.com/).

The repository contains a [core library](/src/NRuuviTag.Core) that defines common types, and listener implementations that observe the Bluetooth LE advertisements emitted by RuuviTag devices. Samples received from RuuviTags can be automatically published to an [MQTT server](#publishing-samples-to-mqtt) or to an [Azure Event Hub](#publishing-samples-to-azure-event-hubs).

The repository contains the following listener implementations:

- [Windows](/src/NRuuviTag.Listener.Windows) (using the Windows 10 SDK)
- [Linux](/src/NRuuviTag.Listener.Linux) (using [Linux.Bluetooth](https://www.nuget.org/packages/Linux.Bluetooth/) to receive advertisements from BlueZ's D-Bus APIs)

The `nruuvitag` [command-line tool](#command-line-application) can be used to as a turnkey solution to start receiving and publishing RuuviTag sensor data to an MQTT server or Azure Event Hub.


# Example Usage

> See the [samples](/samples) folder for more detailed examples of usage.

Usage is very straightforward. For example, to listen via the Windows SDK using the [NRuuviTag.Listener.Windows](https://www.nuget.org/packages/NRuuviTag.Listener.Windows) NuGet package ([source](/src/NRuuviTag.Listener.Windows)):

```csharp
IRuuviTagListener client = new WindowsSdkListener();

await foreach (var sample in client.ListenAsync(cancellationToken)) {
    // sample is a RuuviTagSample object.
}
```

To listen via BlueZ on Linux using the [NRuuviTag.Listener.Linux](https://www.nuget.org/packages/NRuuviTag.Listener.Linux) NuGet package ([source](/src/NRuuviTag.Listener.Linux)):

```csharp
IRuuviTagListener client = new BlueZListener("hci0");

await foreach (var sample in client.ListenAsync(cancellationToken)) {
    // sample is a RuuviTagSample object.
}
```

To only observe specific RuuviTag devices using MAC address filtering:

```csharp
bool CanProcessMessage(string macAddress) {
    return string.Equals(macAddress, "AB:CD:EF:01:23:45");
}

await foreach (var sample in client.ListenAsync(CanProcessMessage, cancellationToken)) {
    // sample is a RuuviTagSample object.
}
```


# Publishing Samples to MQTT

The [NRuuviTag.Mqtt.Publisher](https://www.nuget.org/packages/NRuuviTag.Mqtt.Publisher) NuGet package ([source](./src/NRuuviTag.Mqtt.Publisher)) can be used to observe RuuviTag broadcasts and forward the samples to an MQTT server:

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


# Publishing Samples to Azure Event Hubs

The [NRuuviTag.AzureEventHubs.Publisher](https://www.nuget.org/packages/NRuuviTag.AzureEventHubs.Publisher) NuGet package ([source](./src/NRuuviTag.AzureEventHubs.Publisher)) can be used to observe RuuviTag broadcasts and forward the samples to an Azure Event Hub:

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


# Publishing Samples to an HTTP Endpoint

The [NRuuviTag.Http.Publisher](https://www.nuget.org/packages/NRuuviTag.Http.Publisher) NuGet package ([source](./src/NRuuviTag.Http.Publisher)) can be used to observe RuuviTag broadcasts and forward the samples to an HTTP endpoint:

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


# Command-Line Application

`nruuvitag` is a command-line tool for [Windows](/src/NRuuviTag.Cli.Windows) and [Linux](/src/NRuuviTag.Cli.Linux) that can scan for nearby RuuviTags, and publish device readings to the console, or to an MQTT server or Azure Event Hub.

> Add `--help` to any command to view help.

Examples:

```
# Scan for nearby devices

nruuvitag devices scan
```

```
# Write sensor readings from all nearby devices to the console

nruuvitag publish console
```

```
# Add a device to the known devices list

nruuvitag devices add "AB:CD:EF:01:23:45" --id "bedroom-1" --name "Master Bedroom"
```

```
# Publish readings from known devices to an MQTT server

nruuvitag publish mqtt my-mqtt-service.local:1883 --client-id "MY_CLIENT_ID" --topic "{clientId}/my-ruuvi-tags/{deviceId}" --known-devices
```

```
# Publish readings from nearby devices to an Azure Event Hub in batches of up to 100 samples

nruuvitag publish az "MY_CONNECTION_STRING" "MY_EVENT_HUB" --batch-size-limit 100
```

```
# Publish readings from known devices to an HTTP endpoint

nruuvitag publish http "https://my-receiver.local" --header "X-API-Key: MY_API_KEY" --known-devices
```


# Linux Service

The command-line application can be run as a Linux service using systemd. See [here](/docs/LinuxSystemdService.md) for details.


# Linux Container Image

The command-line application can be run on Linux as a container image. First, create a `$HOME/.nruuvitag` directory if you have not done so already:

```sh
mkdir $HOME/.nruuvitag
```

Then, run the container image as follows:

```sh
docker run -it --rm \
    -v /var/run/dbus:/var/run/dbus \
    -v $HOME/.nruuvitag:/root/.nruuvitag \
    ghcr.io/wazzamatazz/nruuvitag:latest
```

> [!WARNING]
> Note that the container runs as the `root` user to allow BlueZ to access the Bluetooth adapter.

The container requires that the following volumes are mapped:

- `/var/run/dbus` - Enables the container to communicate with DBus to receive Bluetooth advertisements.
- `$HOME/.nruuvitag` - Allows the container to read from/write to the `devices.json` file that stores known devices.

You can append the command arguments to the end of the call to `docker run` e.g. to list known devices:

```sh
docker run -it --rm \
    -v /var/run/dbus:/var/run/dbus \
    -v $HOME/.nruuvitag:/root/.nruuvitag \
    ghcr.io/wazzamatazz/nruuvitag:latest \
    devices list
```

You can also alias the command as follows:

```sh
alias nruuvitag='docker run -it --rm -v /var/run/dbus:/var/run/dbus -v $HOME/.nruuvitag:/root/.nruuvitag ghcr.io/wazzamatazz/nruuvitag:latest'
export nruuvitag
```

To persist the alias, add the above commands to your shell's configuration file (e.g. `~/.bash_aliases`).

Once you have created the alias, you can invoke the `nruuvitag` command as if it were installed on your host machine:

```sh
nruuvitag devices list
```

See [here](/docs/Docker.md) for details about how to build the image. 


# Building the Solution

The repository uses [Cake](https://cakebuild.net/) for cross-platform build automation. The build script allows for metadata such as a build counter to be specified when called by a continuous integration system such as TeamCity.

A build can be run from the command line using the [build.ps1](/build.ps1) PowerShell script or the [build.sh](/build.sh) Bash script. For documentation about the available build script parameters, see [build.cake](/build.cake).

