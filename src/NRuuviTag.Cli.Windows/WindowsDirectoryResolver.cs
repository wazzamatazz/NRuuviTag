using System;
using System.IO;

namespace NRuuviTag.Cli.Windows;

internal sealed class WindowsDirectoryResolver : IDirectoryResolver {

    public string GetConfigDirectory() {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NRuuviTag", "config");
    }

    public string GetDataDirectory() {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NRuuviTag", "data");
        
        // If the data directory does not exist, we'll fall back to the location used by
        // nruuvitag v5 and earlier if it exists. Otherwise, we'll use the default data directory.
        
        if (Directory.Exists(path)) {
            return path;
        }

        var fallbackDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nruuvitag");
        return Directory.Exists(fallbackDataDir) 
            ? fallbackDataDir 
            : path;
    }

}
