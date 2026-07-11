using System.IO;

namespace NRuuviTag.Cli;

public static class DirectoryResolverExtensions {
    
    extension(IDirectoryResolver resolver) {

        public FileInfo GetLocalAppSettingsFilePath() => new FileInfo(Path.Combine(resolver.GetConfigDirectory(), "appsettings.nruuvitag.json"));
        public FileInfo GetDevicesDataFilePath() => new FileInfo(Path.Combine(resolver.GetDataDirectory(), "devices.json"));

    } 

}
