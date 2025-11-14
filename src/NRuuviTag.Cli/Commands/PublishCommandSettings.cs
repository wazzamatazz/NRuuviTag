using System.ComponentModel;

using Spectre.Console.Cli;

namespace NRuuviTag.Cli.Commands;

public class PublishCommandSettings : ListenerCommandSettings {

    [CommandOption("--known-devices")]
    [Description("Specifies if only samples from pre-registered devices should be published.")]
    public bool KnownDevicesOnly { get; set; }
    
    [CommandOption("--publish-interval <INTERVAL>")]
    [Description("The publish to use, in seconds. When a publish interval is specified, the '--publish-behaviour' setting controls if all observed samples for a device are included in the next publish, or if only the most-recent reading for each device are included. If a publish interval is not specified, samples will be published to the destination as soon as they are observed.")]
    public int PublishInterval { get; set; }
    
    [CommandOption("--publish-behaviour <BEHAVIOUR>")]
    [Description("The per-device publish behaviour to use when a non-zero publish interval is specified. Possible values are: " + nameof(BatchPublishDeviceBehaviour.AllSamples) + " (default), " + nameof(BatchPublishDeviceBehaviour.LatestSampleOnly))]
    [DefaultValue(BatchPublishDeviceBehaviour.AllSamples)]
    public BatchPublishDeviceBehaviour PublishBehaviour { get; set; }


    /// <inheritdoc />
    internal override void Bind(RuuviTagListenerOptions options) {
        base.Bind(options);
        options.KnownDevicesOnly = KnownDevicesOnly;
    }

}
