namespace NRuuviTag.Cli;

/// <summary>
/// <see cref="IDirectoryResolver"/> is used to resolve configuration and data directories used by the CLI tool.
/// </summary>
public interface IDirectoryResolver {

    /// <summary>
    /// Gets the directory containing user-specific app configuration files.
    /// </summary>
    /// <returns>
    ///   The directory containing user-specific app configuration files.
    /// </returns>
    string GetConfigDirectory();
    
    /// <summary>
    /// Gets the directory containing user-specific data files.
    /// </summary>
    /// <returns>
    ///   The directory containing user-specific data files.
    /// </returns>
    string GetDataDirectory();

}
