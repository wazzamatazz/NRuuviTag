using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

using Spectre.Console.Cli;

namespace NRuuviTag.Cli.Commands;

/// <summary>
/// <see cref="CommandApp"/> command for listing known RuuviTag devices.
/// </summary>
public class DeviceListCommand : Command<DeviceListCommandSettings> {

    /// <summary>
    /// The known devices.
    /// </summary>
    private readonly DeviceCollection _devices;


    /// <summary>
    /// Creates a new <see cref="DeviceListCommand"/> instance.
    /// </summary>
    /// <param name="devices">
    ///   The known devices.
    /// </param>
    public DeviceListCommand(IOptions<DeviceCollection> devices) {
        _devices = devices.Value;
    }


    /// <inheritdoc/>
    public override int Execute(CommandContext context, DeviceListCommandSettings settings, CancellationToken cancellationToken) {
        Console.WriteLine();
        CommandUtilities.PrintDevicesToConsole(_devices);
        Console.WriteLine();
            
        return 0;
    }

}


/// <summary>
/// Settings for <see cref="DeviceListCommand"/>.
/// </summary>
public class DeviceListCommandSettings : CommandSettings { }
