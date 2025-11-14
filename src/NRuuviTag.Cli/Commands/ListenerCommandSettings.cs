using System.ComponentModel;

using Spectre.Console.Cli;

namespace NRuuviTag.Cli.Commands;

public abstract class ListenerCommandSettings : CommandSettings {

    [CommandOption("--enable-data-format-6")]
    [Description("Specifies whether Data Format 6 advertisements should be processed. By default, Data Format 6 advertisements are ignored in preference of Data Format E1 advertisements.")]
    public bool EnableDataFormat6 { get; set; }
    
    [CommandOption("--allow-duplicate-advertisements")]
    [Description("Linux only. Specifies whether duplicate advertisements should be allowed. By default, duplicate advertisements are ignored.")]
    public bool AllowDuplicateAdvertisements { get; set; }


    internal virtual void Bind(RuuviTagListenerOptions options) {
        options.EnableDataFormat6 = EnableDataFormat6;
        options.AllowDuplicateAdvertisements = AllowDuplicateAdvertisements;
    }
    
}
