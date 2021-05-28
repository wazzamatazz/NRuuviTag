using System;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;

using Spectre.Console.Cli;

namespace NRuuviTag.Cli.Commands {

    /// <summary>
    /// <see cref="CommandApp"/> command for listing known RuuviTag devices.
    /// </summary>
    public class DeviceListCommand : AsyncCommand<DeviceListCommandSettings> {

        /// <inheritdoc/>
        public override async Task<int> ExecuteAsync(CommandContext context, DeviceListCommandSettings settings) {
            var devicesJsonFile = CommandUtilities.GetDevicesJsonFile();

            if (!devicesJsonFile.Exists) {
                Console.WriteLine();
                CommandUtilities.PrintDevicesToConsole(null);
                Console.WriteLine();
                return 0;
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
                Console.WriteLine();
                CommandUtilities.PrintDevicesToConsole(null);
                Console.WriteLine();
                return 0;
            }

            var config = JsonSerializer.Deserialize<JsonElement>(json);
            if (config.ValueKind != JsonValueKind.Object) {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Resources.Error_InvalidDevicesJson, config.ValueKind));
            }

            if (config.TryGetProperty("Devices", out var devicesElement)) {
                devices = JsonSerializer.Deserialize<DeviceCollection>(devicesElement.GetRawText());
            }

            Console.WriteLine();
            CommandUtilities.PrintDevicesToConsole(devices);
            Console.WriteLine();
            
            return 0;
        }

    }


    /// <summary>
    /// Settings for <see cref="DeviceListCommand"/>.
    /// </summary>
    public class DeviceListCommandSettings : CommandSettings { }

}
