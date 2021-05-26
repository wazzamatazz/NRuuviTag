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
        /// The file name that contains known devices.
        /// </summary>
        internal const string DevicesJsonFileName = "devices.json";


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
                options.AddBranch("publish", branchOptions => {
                    branchOptions.SetDescription("Observes RuuviTag BLE broadcasts and writes them to a destination.");

                    branchOptions.AddCommand<PublishConsoleCommand>("console")
                        .WithAlias("stdout")
                        .WithDescription("Publishes RuuviTag samples to the console as JSON.")
                        .WithExample(new[] { "publish", "console" });

                    branchOptions.AddCommand<PublishMqttCommand>("mqtt")
                        .WithDescription("Publishes RuuviTag samples to an MQTT broker.")
                        .WithExample(new[] { "publish", "mqtt", "test.mosquitto.org", "--client-id", "\"MY_CLIENT_ID\"", "--publish-interval", "5", "--known-devices" });
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
        /// <param name="hostEnvironment">
        ///   The <see cref="IHostEnvironment"/> for the app.
        /// </param>
        /// <returns>
        ///   A new <see cref="FileInfo"/> object.
        /// </returns>
        internal static FileInfo GetDevicesJsonFile(IHostEnvironment hostEnvironment) {
            return new FileInfo(Path.Combine(hostEnvironment.ContentRootPath, DevicesJsonFileName));
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

            AnsiConsole.Render(table);
        }


        /// <summary>
        /// Prints information about the specified <see cref="Device"/> to the console.
        /// </summary>
        /// <param name="device">
        ///   The device.
        /// </param>
        /// <param name="id">
        ///   The device ID.
        /// </param>
        internal static void PrintDeviceToConsole(Device device, string id) {
            var table = new Table();
            table.AddColumns(Resources.TableColumn_MacAddress, Resources.TableColumn_DisplayName, Resources.TableColumn_DeviceID);
            table.AddRow(device.MacAddress, device.DisplayName ?? string.Empty, id);

            AnsiConsole.Render(table);
        }

    }
}
