using System;

using NRuuviTag.Cli;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Configuration;

/// <summary>
/// Extension for <see cref="IConfigurationBuilder"/>
/// </summary>
public static class NRuuviTagConfigurationBuilderExtensions {

    /// <summary>
    /// Adds local configuration and known Ruuvi devices to the <see cref="IConfigurationBuilder"/>.
    /// </summary>
    /// <param name="builder">
    ///   The <see cref="IConfigurationBuilder"/>.
    /// </param>
    /// <param name="directoryResolver">
    ///   The <see cref="IDirectoryResolver"/> to resolve the local configuration files with.
    /// </param>
    /// <returns>
    ///   The <see cref="IConfigurationBuilder"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    public static IConfigurationBuilder AddLocalConfiguration(this IConfigurationBuilder builder, IDirectoryResolver directoryResolver) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(directoryResolver);
        builder.AddJsonFile(directoryResolver.GetLocalAppSettingsFilePath().FullName, optional: true, reloadOnChange: true);
        builder.AddJsonFile(directoryResolver.GetDevicesDataFilePath().FullName, optional: true, reloadOnChange: true);

        return builder;
    }
    
}
