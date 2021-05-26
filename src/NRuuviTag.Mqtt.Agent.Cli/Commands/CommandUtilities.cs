using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Extensions.Hosting;

using Spectre.Console;
using Spectre.Console.Cli;

namespace NRuuviTag.Mqtt.Cli.Commands {

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
                options.AddCommand<ListenCommand>("listen")
                    .WithDescription("Listens for RuuviTag BLE broadcasts and writes them to the console.");

                options.AddCommand<PublishCommand>("publish")
                    .WithDescription("Listens for RuuviTag BLE broadcasts and publishes the samples to an MQTT broker.")
                    .WithExample(new[] { "test.mosquitto.org", "--client-id \"MY_CLIENT_ID\"", "--publish-interval 5", "--known-devices" });

                options.AddBranch("device", branchOptions => {
                    branchOptions.SetDescription("Commands related to RuuviTag device management.");

                    branchOptions.AddCommand<DeviceScanCommand>("scan")
                        .WithDescription("Scans for nearby RuuviTags.");

                    branchOptions.AddCommand<DeviceListCommand>("list")
                        .WithDescription("Lists known RuuviTags.");

                    branchOptions.AddCommand<DeviceAddCommand>("add")
                        .WithDescription("Adds a RuuviTag to the known devices list.");

                    branchOptions.AddCommand<DeviceRemoveCommand>("remove")
                        .WithDescription("Removes a RuuviTag from the known devices list.");
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
