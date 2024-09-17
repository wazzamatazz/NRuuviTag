using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

using Spectre.Console.Cli;

namespace NRuuviTag.Cli.Commands {

    /// <summary>
    /// <see cref="CommandApp"/> command for registering a known RuuviTag device.
    /// </summary>
    public class DeviceRemoveCommand : AsyncCommand<DeviceRemoveCommandSettings> {

        /// <summary>
        /// The known devices.
        /// </summary>
        private readonly DeviceCollection _devices;


        /// <summary>
        /// Creates a new <see cref="DeviceAddCommand"/> instance.
        /// </summary>
        /// <param name="devices">
        ///   The known devices.
        /// </param>
        public DeviceRemoveCommand(IOptions<DeviceCollection> devices) {
            _devices = devices.Value;
        }


        /// <inheritdoc/>
        public override async Task<int> ExecuteAsync(CommandContext context, DeviceRemoveCommandSettings settings) {
            if (_devices == null || _devices.Count == 0) {
                // No devices defined
                Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Resources.LogMessage_DeviceNotFound, settings.Device));
                return 1;
            }

            var deviceToRemove = _devices.FirstOrDefault(x => { 
                if (string.Equals(x.Key, settings.Device, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }

                if (string.Equals(x.Value.MacAddress, settings.Device, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }

                if (string.Equals(x.Value.DisplayName, settings.Device, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }

                return false;
            });

            if (deviceToRemove.Key == null) {
                // Device not found.
                Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Resources.LogMessage_DeviceNotFound, settings.Device));
                return 1;
            }

            _devices.Remove(deviceToRemove.Key);

            var updatedDeviceConfig = new {
                Devices = _devices
            };

            var devicesJsonFile = CommandUtilities.GetDevicesJsonFile();

            // Ensure directory exists.
            devicesJsonFile.Directory.Create();

            using (var stream = devicesJsonFile.Open(FileMode.Create, FileAccess.Write)) {
                await JsonSerializer.SerializeAsync(stream, updatedDeviceConfig, new JsonSerializerOptions() { WriteIndented = true }).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }

            Console.WriteLine();
            Console.WriteLine(Resources.LogMessage_DeviceRemoved);
            Console.WriteLine();
            CommandUtilities.PrintDeviceToConsole(deviceToRemove.Key, deviceToRemove.Value);
            Console.WriteLine();

            return 0;
        }
    }


    /// <summary>
    /// Settings for <see cref="DeviceRemoveCommand"/>.
    /// </summary>
    public class DeviceRemoveCommandSettings : CommandSettings {

        [CommandArgument(0, "<DEVICE>")]
        [Description("The identifier, display name, or MAC address of the device to remove.")]
        public string Device { get; set; } = default!;

    }
}
