using System.ComponentModel;

using Spectre.Console.Cli;

namespace NRuuviTag.Cli.Commands;

public abstract class ListenerCommandSettings : CommandSettings {

    [CommandOption("--enable-data-format-6")]
    [Description("Specifies whether Data Format 6 advertisements should be processed. By default, Data Format 6 advertisements are ignored in preference of Data Format E1 advertisements.")]
    public bool EnableDataFormat6 { get; set; }

}
