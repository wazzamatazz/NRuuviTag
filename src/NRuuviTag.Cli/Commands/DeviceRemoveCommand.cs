using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;

using Spectre.Console.Cli;

namespace NRuuviTag.Cli.Commands {

    /// <summary>
    /// <see cref="CommandApp"/> command for registering a known RuuviTag device.
    /// </summary>
    public class DeviceRemoveCommand : AsyncCommand<DeviceRemoveCommandSettings> {

        /// <inheritdoc/>
        public override async Task<int> ExecuteAsync(CommandContext context, DeviceRemoveCommandSettings settings) {
            var devicesJsonFile = CommandUtilities.GetDevicesJsonFile();

            if (!devicesJsonFile.Exists) {
                Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Resources.LogMessage_DeviceNotFound, settings.Device));
                return 1;
            }

            DeviceCollection? devices = null;

            // File already exists; we need to load the devices in, remove the device from the
            // collection, and write back to disk.
            string? json;
            using (var reader = devicesJsonFile.OpenText()) {
                json = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(json)) {
                // Invalid JSON
                Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Resources.LogMessage_DeviceNotFound, settings.Device));
                return 1;
            }

            var config = JsonSerializer.Deserialize<JsonElement>(json);
            if (config.ValueKind != JsonValueKind.Object) {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Resources.Error_InvalidDevicesJson, config.ValueKind));
            }

            if (config.TryGetProperty("Devices", out var devicesElement)) {
                devices = JsonSerializer.Deserialize<DeviceCollection>(devicesElement.GetRawText());
            }

            if (devices == null) {
                // No Devices section
                Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Resources.LogMessage_DeviceNotFound, settings.Device));
                return 1;
            }

            var deviceToRemove = devices.FirstOrDefault(x => { 
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

            devices.Remove(deviceToRemove.Key);

            var updatedDeviceConfig = new {
                Devices = devices
            };

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
