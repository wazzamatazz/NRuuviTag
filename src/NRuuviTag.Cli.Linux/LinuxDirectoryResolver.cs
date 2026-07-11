using System;
using System.IO;

namespace NRuuviTag.Cli.Linux;

internal sealed class LinuxDirectoryResolver : IDirectoryResolver {

    /// <inheritdoc />
    public string GetConfigDirectory() {
        var xdgConfigHome = Environment.ExpandEnvironmentVariables("%XDG_CONFIG_HOME%");
        return string.IsNullOrWhiteSpace(xdgConfigHome) || xdgConfigHome.Equals("%XDG_CONFIG_HOME%", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "nruuvitag") 
            : Path.Combine(xdgConfigHome, "nruuvitag");
    }


    /// <inheritdoc />
    public string GetDataDirectory() {
        var xdgDataHome = Environment.ExpandEnvironmentVariables("%XDG_DATA_HOME%");
        if (string.IsNullOrWhiteSpace(xdgDataHome) || xdgDataHome.Equals("%XDG_DATA_HOME%", StringComparison.OrdinalIgnoreCase)) {
            xdgDataHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }
        
        // If the XDG data directory does not exist, we'll fall back to the location used by
        // nruuvitag v5 and earlier if it exists. Otherwise, we'll use the XDG data directory.
        
        var path = Path.Combine(xdgDataHome, "nruuvitag");
        if (Directory.Exists(path)) {
            return path;
        }

        var fallbackDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nruuvitag");
        return Directory.Exists(fallbackDataDir) 
            ? fallbackDataDir 
            : path;
    }

}
