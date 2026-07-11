# NRuuviTag

A command-line tool and collection of .NET libraries to simplify interacting with [Ruuvi IoT sensors](https://www.ruuvi.com/).

The repository contains a [core library](/src/NRuuviTag.Core) that defines common types, and listener implementations that observe the Bluetooth LE advertisements emitted by Ruuvi devices. Samples can be automatically published to an [MQTT server](#publishing-samples-to-mqtt), to an [Azure Event Hub](#publishing-samples-to-azure-event-hubs), or to an [HTTP endpoint](#publishing-samples-to-an-http-endpoint). A [command-line tool](#command-line-application) provides a turnkey solution to start receiving and publishing RuuviTag sensor data to an MQTT server or Azure Event Hub.

The following Ruuvi data formats are supported:

- [RAWv2](https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-5-rawv2) - RuuviTag
- [Extended v1](https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-e1) - Ruuvi Air
- [Data Format 6](https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-6) - Ruuvi Air compatibility mode for Bluetooth adapters that do not support extended advertisements

The repository contains the following listener implementations:

- [Windows](/src/NRuuviTag.Listener.Windows) (using the Windows SDK)
- [Linux](/src/NRuuviTag.Listener.Linux) (using [Linux.Bluetooth](https://www.nuget.org/packages/Linux.Bluetooth/) to receive advertisements from BlueZ's D-Bus APIs)

# Command-Line Application

`nruuvitag` is a command-line tool for [Windows](/src/NRuuviTag.Cli.Windows) and [Linux](/src/NRuuviTag.Cli.Linux) that can scan for nearby RuuviTags, and publish device readings to the console, or to an MQTT server or Azure Event Hub.

Starting from v6, executables for Windows and Linux are available on the [releases](https://github.com/wazzamatazz/NRuuviTag/releases/) page. Linux container images ([see below](#linux-container-image)) are available on [ghcr.io](github.com/wazzamatazz/NRuuviTag/pkgs/container/nruuvitag).

> [!TIP]
> Add `--help` to any command to view help.

Examples:

```sh
# Scan for nearby devices
nruuvitag devices scan
```

```sh
# Write sensor readings from all nearby devices to the console
nruuvitag publish console
```

```sh
# Add a device to the known devices list
nruuvitag devices add \
  "AB:CD:EF:01:23:45" \
  --id "bedroom-1" \
  --name "Master Bedroom"
```

```sh
# Publish readings from known devices to an MQTT server
nruuvitag publish mqtt \
  my-mqtt-service.local:1883 \
  --client-id "MY_CLIENT_ID" \
  --topic "{clientId}/my-ruuvi-tags/{deviceId}" \
  --known-devices
```

```sh
# Publish readings from nearby devices to an Azure Event Hub in batches of
# up to 100 samples
nruuvitag publish az \
  "MY_CONNECTION_STRING" \
  "MY_EVENT_HUB" \
  --batch-size-limit 100
```

```sh
# Publish readings from known devices to an HTTP endpoint, including 
# devices using the extended advertising data format E1
nruuvitag publish http \
  "https://my-receiver.local" \
  --header "X-API-Key: MY_API_KEY" \
  --known-devices \
  --extended-advertisements
```


## Linux Container Image

The command-line application can be run on Linux as a container image. 

> [!WARNING]
> Note that the container runs as the `root` user to allow BlueZ to access the Bluetooth adapter.

### Happy Path

You can copy and paste the following shell script to create an executable `nruuvitag` command on your host machine that bootstraps the container image for you:

```sh
local_bin="$HOME/.local/bin"
exe_path="$local_bin/nruuvitag"

mkdir -p "$local_bin"

cat > "$exe_path" <<'EOF'
#!/usr/bin/env bash

# See GCR for available image tags
image="ghcr.io/wazzamatazz/nruuvitag:latest"

# Run `nruuvitag update` to pull the latest container image
if [[ $1 == "update" ]]; then
  docker pull $image
  exit 0
fi

# nruuvitag uses the XDG Base Directory Specification to determine where to 
# store configuration files. If XDG_DATA_HOME is not set, ~/.local/share is
# used by default.
if [[ -z "$XDG_DATA_HOME" ]]; then
    XDG_DATA_HOME="$HOME/.local/share"
fi

mkdir -p "$XDG_DATA_HOME/nruuvitag"

docker run -it --rm \
    -v /var/run/dbus:/var/run/dbus \
    -v $XDG_DATA_HOME/nruuvitag:/root/.local/share/nruuvitag \
    $image \
    "$@"
EOF

chmod +x "$exe_path"
```

You can then invoke the `nruuvitag` command as if it were installed locally on your host machine:

```sh
# List known devices
nruuvitag devices list

# Pull the latest container image (handled automatically by the script)
nruuvitag update
```

### Manual Container Configuration

The container requires that `/var/run/dbus` is mapped from host to container to enable communication with DBus in order to receive Bluetooth advertisements. The container writes configuration files to `/root/.local/share/nruuvitag`; it's a good idea to map a volume to this directory as well:

```sh
# Create ~/.local/share/nruuvitag/ if it does not already exist
mkdir -p $HOME/.local/share/nruuvitag

# Run the container
docker run -it --rm \
  -v /var/run/dbus:/var/run/dbus \
  -v $HOME/.local/share/nruuvitag:/root/.local/share/nruuvitag \
  ghcr.io/wazzamatazz/nruuvitag:latest
```

You can append the command arguments to the end of the call to `docker run` e.g. to list known devices:

```sh
docker run -it --rm \
    -v /var/run/dbus:/var/run/dbus \
    -v $HOME/.local/share/nruuvitag:/root/.local/share/nruuvitag \
    ghcr.io/wazzamatazz/nruuvitag:latest \
    devices list
```

### Available Container Images

You can find information about the available container images [here](github.com/wazzamatazz/NRuuviTag/pkgs/container/nruuvitag). See [here](/docs/Docker.md) for details about how to build the image locally.

## Linux Service

The command-line application can be run as a Linux service using systemd. See [here](/docs/LinuxSystemdService.md) for details.


## OpenTelemetry

The CLI tool automatically exports logs and metrics to an OTLP-compatible endpoint if standard OpenTelemetry environment variables are set. See [here](https://opentelemetry.io/docs/languages/sdk-configuration/otlp-exporter/) for details about configuring OpenTelemetry.


# Using the .NET Libraries

> [!TIP]
> See the [samples](/samples) folder for more detailed examples of usage.

## Listening for Samples

Using the .NET libraries is very straightforward. For example, to listen via the Windows SDK using the [NRuuviTag.Listener.Windows](https://www.nuget.org/packages/NRuuviTag.Listener.Windows) NuGet package ([source](/src/NRuuviTag.Listener.Windows)):

```csharp
IRuuviTagListener client = new WindowsSdkListener(new WindowsSdkListenerOptions());

await foreach (var sample in client.ListenAsync(cancellationToken)) {
    // sample is a RuuviTagSample object.
}
```

To listen via BlueZ on Linux using the [NRuuviTag.Listener.Linux](https://www.nuget.org/packages/NRuuviTag.Listener.Linux) NuGet package ([source](/src/NRuuviTag.Listener.Linux)):

```csharp
IRuuviTagListener client = new BlueZListener(new BlueZListenerOptions() {
    AdapterName = "hci0" // Optional, defaults to "hci0"
});

await foreach (var sample in client.ListenAsync(cancellationToken)) {
    // sample is a RuuviTagSample object.
}
```


## Publishing Samples to MQTT

The [NRuuviTag.Publisher.Mqtt](https://www.nuget.org/packages/NRuuviTag.Publisher.Mqtt) NuGet package ([source](./src/NRuuviTag.Publisher.Mqtt)) can be used to observe RuuviTag broadcasts and forward the samples to an MQTT server:

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
        new MQTTnet.MqttClientFactory(), 
        loggerFactory?.CreateLogger<MqttPublisher>());
  
    await publisher.RunAsync(cancellationToken);
}
```

## Publishing Samples to Azure Event Hubs

The [NRuuviTag.Publisher.AzureEventHubs](https://www.nuget.org/packages/NRuuviTag.Publisher.AzureEventHubs) NuGet package ([source](./src/NRuuviTag.Publisher.AzureEventHubs)) can be used to observe RuuviTag broadcasts and forward the samples to an Azure Event Hub:

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

## Publishing Samples to an HTTP Endpoint

The [NRuuviTag.Publisher.Http](https://www.nuget.org/packages/NRuuviTag.Publisher.Http) NuGet package ([source](./src/NRuuviTag.Publisher.Http)) can be used to observe RuuviTag broadcasts and forward the samples to an HTTP endpoint:

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


# Building the Solution

The repository uses [Cake](https://cakebuild.net/) for cross-platform build automation.

A build can be run from the command line using the [build.ps1](/build.ps1) PowerShell script or the [build.sh](/build.sh) Bash script. For documentation about the available build script parameters, run the script without any arguments.

