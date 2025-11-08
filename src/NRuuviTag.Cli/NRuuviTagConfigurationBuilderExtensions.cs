using System;

using NRuuviTag.Cli.Commands;

namespace Microsoft.Extensions.Configuration;

/// <summary>
/// Extension for <see cref="IConfigurationBuilder"/>
/// </summary>
public static class NRuuviTagConfigurationBuilderExtensions {

    /// <summary>
    /// Adds known MQTT devices to the <see cref="IConfigurationBuilder"/>.
    /// </summary>
    /// <param name="builder">
    ///   The <see cref="IConfigurationBuilder"/>.
    /// </param>
    /// <returns>
    ///   The <see cref="IConfigurationBuilder"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    public static IConfigurationBuilder AddRuuviTagDeviceConfiguration(this IConfigurationBuilder builder) {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddJsonFile(CommandUtilities.GetDevicesJsonFile().FullName, optional: true, reloadOnChange: true);

        return builder;
    }
    
}
