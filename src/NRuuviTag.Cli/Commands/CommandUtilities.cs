using System;
using System.IO;

using Microsoft.Extensions.Hosting;

using Spectre.Console;
using Spectre.Console.Cli;

namespace NRuuviTag.Cli.Commands {

    /// <summary>
    /// Utility methods for use in <see cref="CommandApp"/> commands.
    /// </summary>
    internal static class CommandUtilities {

        /// <summary>
        /// Folder under the user's profile that the devices.json will be stored in.
        /// </summary>
        private const string LocalDataFolderName = ".nruuvitag";

        /// <summary>
        /// The file name that contains known devices.
        /// </summary>
        private const string DevicesJsonFileName = "devices.json";


        /// <summary>
        /// Builds a <see cref="CommandApp"/> instance for the process.
        /// </summary>
        /// <param name="typeRegistrar">
        ///   The <see cref="ITypeRegistrar"/> that the app should use.
        /// </param>
        /// <returns>
        ///   A new <see cref="CommandApp"/> object.
        /// </returns>
        internal static CommandApp BuildCommandApp(ITypeRegistrar typeRegistrar) {
            var app = new CommandApp(typeRegistrar);

            app.Configure(options => {
#if DEBUG
                options.PropagateExceptions();
                options.ValidateExamples();
#endif

                options.SetApplicationName("nruuvitag");

                options.AddBranch("publish", branchOptions => {
                    branchOptions.SetDescription("Observes RuuviTag BLE broadcasts and writes them to a destination.");

                    branchOptions.AddCommand<PublishConsoleCommand>("console")
                        .WithAlias("stdout")
                        .WithDescription("Publishes RuuviTag samples to the console as JSON.")
                        .WithExample(new[] { "publish", "console" });

                    branchOptions.AddCommand<PublishMqttCommand>("mqtt")
                        .WithDescription("Publishes RuuviTag samples to an MQTT broker.")
                        .WithExample(new[] { "publish", "mqtt", "test.mosquitto.org", "--client-id", "\"MY_CLIENT_ID\"", "--sample-rate", "5", "--known-devices" });

                    branchOptions.AddCommand<PublishAzureEventHubCommand>("az")
                        .WithDescription("Publishes RuuviTag samples to an Azure Event Hub.")
                        .WithExample(new[] { "publish", "az", "\"MY_CONNECTION_STRING\"", "\"MY_HUB\"", "--batch-size-limit", "100" });
                });

                options.AddBranch("devices", branchOptions => {
                    branchOptions.SetDescription("Commands related to RuuviTag device management.");

                    branchOptions.AddCommand<DeviceScanCommand>("scan")
                        .WithDescription("Scans for nearby RuuviTags.");

                    branchOptions.AddCommand<DeviceListCommand>("list")
                        .WithDescription("Lists known RuuviTags.");

                    branchOptions.AddCommand<DeviceAddCommand>("add")
                        .WithDescription("Adds a RuuviTag to the known devices list.")
                        .WithExample(new[] { "devices", "add", "\"AB:CD:EF:01:23:45\"", "--id", "\"bedroom-1\"", "--name", "\"Master Bedroom\"" });

                    branchOptions.AddCommand<DeviceRemoveCommand>("remove")
                        .WithDescription("Removes a RuuviTag from the known devices list.")
                        .WithExample(new[] { "devices", "remove", "\"AB:CD:EF:01:23:45\"" })
                        .WithExample(new[] { "devices", "remove", "bedroom-1" })
                        .WithExample(new[] { "devices", "remove", "\"Master Bedroom\"" });
                });
            });

            return app;
        }


        /// <summary>
        /// Gets a <see cref="FileInfo"/> object for the <c>devices.json</c> file containing known 
        /// device configurations.
        /// </summary>
        /// <returns>
        ///   A new <see cref="FileInfo"/> object.
        /// </returns>
        internal static FileInfo GetDevicesJsonFile() {
            return new FileInfo(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                LocalDataFolderName,
                DevicesJsonFileName
            ));
        }


        /// <summary>
        /// Prints information about the specified <see cref="DeviceCollection"/> to the console.
        /// </summary>
        /// <param name="devices">
        ///   The device collection to print.
        /// </param>
        internal static void PrintDevicesToConsole(DeviceCollection? devices) {
            var table = new Table();
            table.AddColumns(Resources.TableColumn_MacAddress, Resources.TableColumn_DisplayName, Resources.TableColumn_DeviceID);

            if (devices != null) {
                foreach (var item in devices) {
                    table.AddRow(item.Value.MacAddress, item.Value.DisplayName ?? string.Empty, item.Key);
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
            table.AddColumns(Resources.TableColumn_MacAddress, Resources.TableColumn_DisplayName, Resources.TableColumn_DeviceID);
            table.AddRow(device.MacAddress ?? string.Empty, device.DisplayName ?? string.Empty, device.DeviceId ?? string.Empty);

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
            table.AddColumns(Resources.TableColumn_MacAddress, Resources.TableColumn_DisplayName, Resources.TableColumn_DeviceID);
            table.AddRow(deviceFromConfig.MacAddress ?? string.Empty, deviceFromConfig.DisplayName ?? string.Empty, id);

            AnsiConsole.Write(table);
        }

    }
}
