using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NRuuviTag.AzureEventHubs;

using Spectre.Console.Cli;

namespace NRuuviTag.Cli.Commands;

/// <summary>
/// <see cref="CommandApp"/> command for listening to RuuviTag broadcasts and publishing the
/// samples to an Azure Event Hub.
/// </summary>
public class PublishAzureEventHubCommand : AsyncCommand<PublishAzureEventHubCommand.Settings> {

    /// <summary>
    /// The <see cref="IRuuviTagListenerFactory"/> to create listeners with.
    /// </summary>
    private readonly IRuuviTagListenerFactory _listenerFactory;

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
    /// <param name="listenerFactory">
    ///   The <see cref="IRuuviTagListener"/> to listen to broadcasts with.
    /// </param>
    /// <param name="appLifetime">
    ///   The <see cref="IHostApplicationLifetime"/> for the .NET host application.
    /// </param>
    /// <param name="loggerFactory">
    ///   The <see cref="ILoggerFactory"/> for the application.
    /// </param>
    public PublishAzureEventHubCommand(
        IRuuviTagListenerFactory listenerFactory,
        IHostApplicationLifetime appLifetime,
        ILoggerFactory loggerFactory
    ) {
        _listenerFactory = listenerFactory;
        _loggerFactory = loggerFactory;
        _appLifetime = appLifetime;
    }


    /// <inheritdoc/>
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) {
        if (!_appLifetime.ApplicationStarted.IsCancellationRequested) {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, _appLifetime.ApplicationStarted).ConfigureAwait(false); }
            catch (OperationCanceledException) when (_appLifetime.ApplicationStarted.IsCancellationRequested) { }
        }

        var listener = _listenerFactory.CreateListener(options => {
            options.KnownDevicesOnly = settings.KnownDevicesOnly;
            options.EnableDataFormat6 = !settings.EnableDataFormat6;
        });
        
        var publisherOptions = new AzureEventHubPublisherOptions() {
            ConnectionString = settings.ConnectionString!,
            EventHubName = settings.EventHubName!,
            PublishInterval = TimeSpan.FromSeconds(settings.PublishInterval),
            PerDevicePublishBehaviour = settings.PublishBehaviour,
            MaximumBatchSize = settings.MaximumBatchSize,
            MaximumBatchAge = settings.MaximumBatchAge
        };

        await using var publisher = new AzureEventHubPublisher(listener, publisherOptions, _loggerFactory);

        using var ctSource = CancellationTokenSource.CreateLinkedTokenSource(_appLifetime.ApplicationStopped, _appLifetime.ApplicationStopping);
        try {
            await publisher.RunAsync(ctSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ctSource.IsCancellationRequested) { }

        return 0;
    }

    
    /// <summary>
    /// Settings for <see cref="PublishMqttCommand"/>.
    /// </summary>
    public class Settings : PublishCommandSettings {

        [CommandArgument(0, "<CONNECTION_STRING>")]
        [Description("The Azure Event Hub connection string.")]
        public string? ConnectionString { get; set; }

        [CommandArgument(1, "<EVENT_HUB_NAME>")]
        [Description("The Event Hub name.")]
        public string? EventHubName { get; set; }

        [CommandOption("--batch-size-limit <LIMIT>")]
        [DefaultValue(50)]
        [Description("Sets the maximum number of samples that can be added to an Event Hub data batch before the batch will be published to the hub.")]
        public int MaximumBatchSize { get; set; }

        [CommandOption("--batch-age-limit <LIMIT>")]
        [DefaultValue(60)]
        [Description("Sets the maximum age of an Event Hub data batch (in seconds) before the batch will be published to the hub. The age is measured from the time that the first sample is added to the batch.")]
        public int MaximumBatchAge { get; set; }

    }
    
}

