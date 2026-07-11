using Spectre.Console;
using Spectre.Console.Cli;

namespace NRuuviTag.Cli.Commands;

/// <summary>
/// Utility methods for use in <see cref="CommandApp"/> commands.
/// </summary>
internal static class CommandUtilities {

    /// <summary>
    /// Configures the NRuuviTag command app.
    /// </summary>
    /// <param name="options">
    ///   The <see cref="IConfigurator"/> to configure.
    /// </param>
    internal static void ConfigureCommandApp(IConfigurator options) {
#if DEBUG
        options.PropagateExceptions();
        options.ValidateExamples();
#endif

        options.SetApplicationName("nruuvitag");
        options.UseAssemblyInformationalVersion();

        options.AddBranch("publish", branchOptions => {
            branchOptions.SetDescription("Observes RuuviTag BLE broadcasts and writes them to a destination.");

            branchOptions.AddCommand<PublishConsoleCommand>("console")
                .WithAlias("stdout")
                .WithDescription("Publishes RuuviTag samples to the console using the JSON Lines text format.")
                .WithExample(["publish", "console"]);

            branchOptions.AddCommand<PublishMqttCommand>("mqtt")
                .WithDescription("Publishes RuuviTag samples to an MQTT broker.")
                .WithExample(["publish", "mqtt", "test.mosquitto.org", "--client-id", "\"MY_CLIENT_ID\"", "--publish-interval", "5", "--known-devices"]);

            branchOptions.AddCommand<PublishAzureEventHubCommand>("az")
                .WithDescription("Publishes RuuviTag samples to an Azure Event Hub.")
                .WithExample(["publish", "az", "\"MY_CONNECTION_STRING\"", "\"MY_HUB\"", "--batch-size-limit", "100"]);
            
            branchOptions.AddCommand<PublishHttpCommand>("http")
                .WithDescription("Publishes RuuviTag samples to an HTTP endpoint.")
                .WithExample(["publish", "http", "\"https://localhost:44321/telemetry\"", "--known-devices"]);
        });

        options.AddBranch("devices", branchOptions => {
            branchOptions.SetDescription("Commands related to RuuviTag device management.");

            branchOptions.AddCommand<DeviceScanCommand>("scan")
                .WithDescription("Scans for nearby RuuviTags.");

            branchOptions.AddCommand<DeviceListCommand>("list")
                .WithDescription("Lists known RuuviTags.");

            branchOptions.AddCommand<DeviceAddCommand>("add")
                .WithDescription("Adds a RuuviTag to the known devices list.")
                .WithExample(["devices", "add", "\"AB:CD:EF:01:23:45\"", "--id", "\"bedroom-1\"", "--name", "\"Master Bedroom\""]);

            branchOptions.AddCommand<DeviceRemoveCommand>("remove")
                .WithDescription("Removes a RuuviTag from the known devices list.")
                .WithExample(["devices", "remove", "\"AB:CD:EF:01:23:45\""])
                .WithExample(["devices", "remove", "bedroom-1"])
                .WithExample(["devices", "remove", "\"Master Bedroom\""]);
        });
    }
    
    /// <summary>
    /// Prints information about the specified <see cref="DeviceCollection"/> to the console.
    /// </summary>
    /// <param name="devices">
    ///   The device collection to print.
    /// </param>
    internal static void PrintDevicesToConsole(DeviceCollection? devices) {
        var table = new Table();
        table.AddColumns(Resources.TableColumn_MacAddress, Resources.TableColumn_DeviceID, Resources.TableColumn_DisplayName);

        if (devices != null) {
            foreach (var item in devices) {
                table.AddRow(item.Value.MacAddress, item.Key, item.Value.DisplayName ?? string.Empty);
            }
        }

        AnsiConsole.Write(table);
    }


    /// <summary>
    /// Prints information about the specified <see cref="Device"/> to the console.
    /// </summary>
    /// <param name="device">
    ///   The device.
    /// </param>
    internal static void PrintDeviceToConsole(Device device) {
        var table = new Table();
        table.AddColumns(Resources.TableColumn_MacAddress, Resources.TableColumn_DeviceID, Resources.TableColumn_DisplayName);
        table.AddRow(device.MacAddress ?? string.Empty, device.DeviceId ?? string.Empty, device.DisplayName ?? string.Empty);

        AnsiConsole.Write(table);
    }


    /// <summary>
    /// Prints information about the specified <see cref="Device"/> to the console.
    /// </summary>
    /// <param name="id">
    ///   The device identifier.
    /// </param>
    /// <param name="deviceFromConfig">
    ///   The device configuration.
    /// </param>
    internal static void PrintDeviceToConsole(string id, DeviceCollectionEntry deviceFromConfig) {
        var table = new Table();
        table.AddColumns(Resources.TableColumn_MacAddress, Resources.TableColumn_DeviceID, Resources.TableColumn_DisplayName);
        table.AddRow(deviceFromConfig.MacAddress ?? string.Empty, id, deviceFromConfig.DisplayName ?? string.Empty);

        AnsiConsole.Write(table);
    }

}
