using System.ComponentModel;

using Spectre.Console.Cli;

namespace NRuuviTag.Cli.Commands;

public abstract class ListenerCommandSettings : CommandSettings {
    
    [CommandOption("--extended-advertisements")]
    [Description("Specifies whether extended advertisement formats should be enabled. When enabled, formats that use extended advertisements will be processed, and their fallback formats will be ignored.")]
    public bool EnableExtendedAdvertisementFormats { get; set; }
    
    [CommandOption("--allow-duplicate-advertisements")]
    [Description("Linux only. Specifies whether duplicate advertisements should be allowed. By default, duplicate advertisements are ignored.")]
    public bool AllowDuplicateAdvertisements { get; set; }


    internal virtual void Bind(RuuviTagListenerOptions options) {
        options.EnableExtendedAdvertisementFormats = options.EnableExtendedAdvertisementFormats;
        options.AllowDuplicateAdvertisements = AllowDuplicateAdvertisements;
    }
    
}
