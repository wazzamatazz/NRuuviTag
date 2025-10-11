using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NRuuviTag.AzureEventHubs;

using Spectre.Console.Cli;

namespace NRuuviTag.Cli.Commands;

/// <summary>
/// <see cref="CommandApp"/> command for listening to RuuviTag broadcasts and publishing the
/// samples to an Azure Event Hub.
/// </summary>
public class PublishAzureEventHubCommand : AsyncCommand<PublishAzureEventHubCommandSettings> {

    /// <summary>
    /// The <see cref="IRuuviTagListener"/> to listen to broadcasts with.
    /// </summary>
    private readonly IRuuviTagListener _listener;

    /// <summary>
    /// The known RuuviTag devices.
    /// </summary>
    private readonly IOptionsMonitor<DeviceCollection> _devices;

    /// <summary>
    /// The <see cref="IHostApplicationLifetime"/> for the .NET host application.
    /// </summary>
    private readonly IHostApplicationLifetime _appLifetime;

    /// <summary>
    /// The <see cref="ILoggerFactory"/> for the application.
    /// </summary>
    private readonly ILoggerFactory _loggerFactory;


    /// <summary>
    /// Creates a new <see cref="PublishMqttCommand"/> object.
    /// </summary>
    /// <param name="listener">
    ///   The <see cref="IRuuviTagListener"/> to listen to broadcasts with.
    /// </param>
    /// <param name="devices">
    ///   The known RuuviTag devices.
    /// </param>
    /// <param name="appLifetime">
    ///   The <see cref="IHostApplicationLifetime"/> for the .NET host application.
    /// </param>
    /// <param name="loggerFactory">
    ///   The <see cref="ILoggerFactory"/> for the application.
    /// </param>
    public PublishAzureEventHubCommand(
        IRuuviTagListener listener,
        IOptionsMonitor<DeviceCollection> devices,
        IHostApplicationLifetime appLifetime,
        ILoggerFactory loggerFactory
    ) {
        _listener = listener;
        _devices = devices;
        _loggerFactory = loggerFactory;
        _appLifetime = appLifetime;
    }


    /// <inheritdoc/>
    public override async Task<int> ExecuteAsync(CommandContext context, PublishAzureEventHubCommandSettings settings) {
        if (!_appLifetime.ApplicationStarted.IsCancellationRequested) {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, _appLifetime.ApplicationStarted).ConfigureAwait(false); }
            catch (OperationCanceledException) when (_appLifetime.ApplicationStarted.IsCancellationRequested) { }
        }

        IEnumerable<Device> devices = null!;

        UpdateDevices(_devices.CurrentValue);

        var publisherOptions = new AzureEventHubPublisherOptions() {
            ConnectionString = settings.ConnectionString!,
            EventHubName = settings.EventHubName!,
            SampleRate = settings.SampleRate,
            MaximumBatchSize = settings.MaximumBatchSize,
            MaximumBatchAge = settings.MaximumBatchAge,
            KnownDevicesOnly = settings.KnownDevicesOnly,
            GetDeviceInfo = addr => {
                lock (this) {
                    return devices.FirstOrDefault(x => MacAddressComparer.Instance.Equals(addr, x.MacAddress));
                }
            }
        };

        await using var publisher = new AzureEventHubPublisher(_listener, publisherOptions, _loggerFactory);

        using (_devices.OnChange(UpdateDevices))
        using (var ctSource = CancellationTokenSource.CreateLinkedTokenSource(_appLifetime.ApplicationStopped, _appLifetime.ApplicationStopping)) {
            try {
                await publisher.RunAsync(ctSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ctSource.IsCancellationRequested) { }
        }

        return 0;

        void UpdateDevices(DeviceCollection? devicesFromConfig) {
            lock (this) {
                devices = devicesFromConfig?.GetDevices() ?? [];
            }
        }
    }

}


/// <summary>
/// Settings for <see cref="PublishMqttCommand"/>.
/// </summary>
public class PublishAzureEventHubCommandSettings : CommandSettings {

    [CommandArgument(0, "<CONNECTION_STRING>")]
    [Description("The Azure Event Hub connection string.")]
    public string? ConnectionString { get; set; }

    [CommandArgument(1, "<EVENT_HUB_NAME>")]
    [Description("The Event Hub name.")]
    public string? EventHubName { get; set; }

    [CommandOption("--sample-rate <INTERVAL>")]
    [Description("Limits the RuuviTag sample rate to the specified number of seconds. Only the most-recent reading for each RuuviTag device will be included in the next Event Hub batch publish. If not specified, all observed samples will be send to the Event Hub.")]
    public int SampleRate { get; set; }

    [CommandOption("--batch-size-limit <LIMIT>")]
    [DefaultValue(50)]
    [Description("Sets the maximum number of samples that can be added to an Event Hub data batch before the batch will be published to the hub.")]
    public int MaximumBatchSize { get; set; }

    [CommandOption("--batch-age-limit <LIMIT>")]
    [DefaultValue(60)]
    [Description("Sets the maximum age of an Event Hub data batch (in seconds) before the batch will be published to the hub. The age is measured from the time that the first sample is added to the batch.")]
    public int MaximumBatchAge { get; set; }

    [CommandOption("--known-devices")]
    [Description("Specifies if only samples from pre-registered devices should be published to the event hub.")]
    public bool KnownDevicesOnly { get; set; }

}
