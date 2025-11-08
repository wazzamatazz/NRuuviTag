using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;

using Microsoft.Extensions.Logging;

namespace NRuuviTag.AzureEventHubs;

/// <summary>
/// Observes measurements emitted by an <see cref="IRuuviTagListener"/> and publishes them to 
/// an Azure Event Hub.
/// </summary>
public partial class AzureEventHubPublisher : RuuviTagPublisher {

    /// <summary>
    /// Logging.
    /// </summary>
    private readonly ILogger<AzureEventHubPublisher> _logger;

    /// <summary>
    /// A delegate that retrieves the device information for a sample based on the MAC address 
    /// of the device.
    /// </summary>
    private readonly Func<string, Device?>? _getDeviceInfo;

    /// <summary>
    /// A delegate that can be used to make final modifications to a sample prior to 
    /// publishing it to the event hub.
    /// </summary>
    private readonly Func<RuuviTagSampleExtended, RuuviTagSampleExtended>? _prepareForPublish;

    /// <summary>
    /// Event hub connection string.
    /// </summary>
    private readonly string _connectionString;

    /// <summary>
    /// Event hub name.
    /// </summary>
    private readonly string _eventHubName;

    /// <summary>
    /// Maximum event data batch size before the batch will be published to the event hub.
    /// </summary>
    private readonly int _maximumBatchSize;

    /// <summary>
    /// Maximum event data batch age (in seconds) before it will be published to the event hub.
    /// </summary>
    private readonly int _maximumBatchAge;

    /// <summary>
    /// JSON serializer options for serializing message payloads.
    /// </summary>
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // If and properties on a sample are set to null, we won't include them in the
        // serialized object we send to the event hub.
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };


    /// <summary>
    /// Creates a new <see cref="AzureEventHubPublisher"/> object.
    /// </summary>
    /// <param name="listener">
    ///   The <see cref="IRuuviTagListener"/> to observe for sensor readings.
    /// </param>
    /// <param name="options">
    ///   Publisher options.
    /// </param>
    /// <param name="loggerFactory">
    ///   The logger factory to use.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="listener"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ValidationException">
    ///   <paramref name="options"/> fails validation.
    /// </exception>
    public AzureEventHubPublisher(IRuuviTagListener listener, AzureEventHubPublisherOptions options, ILoggerFactory? loggerFactory = null) 
        : base(listener, options, BuildFilterDelegate(options), loggerFactory?.CreateLogger<RuuviTagPublisher>()) {
        ArgumentNullException.ThrowIfNull(options);
        Validator.ValidateObject(options, new ValidationContext(options), true);

        _logger = loggerFactory?.CreateLogger<AzureEventHubPublisher>() ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AzureEventHubPublisher>.Instance;
        _connectionString = options.ConnectionString; 
        _eventHubName = options.EventHubName;
        _maximumBatchSize = options.MaximumBatchSize;
        _maximumBatchAge = options.MaximumBatchAge;
        _getDeviceInfo = options.GetDeviceInfo;
        _prepareForPublish = options.PrepareForPublish;
    }


    /// <summary>
    /// Builds a filter delegate that can restrict listening to broadcasts from only known 
    /// devices if required.
    /// </summary>
    /// <param name="options">
    ///   The options.
    /// </param>
    /// <returns>
    ///   The filter delegate.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    private static Func<string, bool> BuildFilterDelegate(AzureEventHubPublisherOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.KnownDevicesOnly) {
            return _ => true;
        }

        return options.GetDeviceInfo is null
            ? _ => false
            : CreateKnownDevicesFilterDelegate(options.GetDeviceInfo);
    }


    protected override async Task RunAsync(ChannelReader<RuuviTagSample> samples, CancellationToken cancellationToken) {
        LogStartingEventHubClient();

        await using var client = new EventHubProducerClient(_connectionString, _eventHubName);
            
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var batch = await client.CreateBatchAsync(cancellationToken).ConfigureAwait(false);
        var currentBatchStartedAt = TimeSpan.Zero;

        try {
            while (await samples.WaitToReadAsync(cancellationToken).ConfigureAwait(false)) {
                while (samples.TryRead(out var item)) {
                    var device = _getDeviceInfo?.Invoke(item.MacAddress!);

                    var sample = device is null
                        ? new RuuviTagSampleExtended(null, null, item)
                        : new RuuviTagSampleExtended(device.DeviceId, device.DisplayName, item);
                    
                    if (_prepareForPublish is not null) {
                        sample = _prepareForPublish.Invoke(sample);
                    }
                    var eventData = new EventData(JsonSerializer.SerializeToUtf8Bytes(sample, _jsonOptions)) {
                        Properties = {
                            ["Content-Type"] = "application/json"
                        }
                    };

                    if (batch.TryAdd(eventData) && batch.Count == 1) {
                        // Start of new batch
                        currentBatchStartedAt = stopwatch.Elapsed;
                    }

                    if (batch.Count == 0) {
                        continue;
                    }

                    if (batch.Count < _maximumBatchSize && (stopwatch.Elapsed - currentBatchStartedAt).TotalSeconds < _maximumBatchAge) {
                        continue;
                    }

                    await PublishBatchAsync(client, batch, _logger, cancellationToken).ConfigureAwait(false);
                    batch = await client.CreateBatchAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) {
            if (batch?.Count > 0) {
                await PublishBatchAsync(client, batch, _logger, cancellationToken).ConfigureAwait(false);
                batch = null;
            }
        }
        finally {
            batch?.Dispose();
            LogEventHubClientStopped();
        }

        return;

        static async Task PublishBatchAsync(EventHubProducerClient client, EventDataBatch batch, ILogger<AzureEventHubPublisher> logger, CancellationToken cancellationToken) {
            try {
                await client.SendAsync(batch, cancellationToken).ConfigureAwait(false);
                LogEventHubBatchPublished(logger, batch.Count);
            }
            catch (Exception e) {
                LogEventHubPublishError(logger, batch.Count, e);
            }
        }
    }


    [LoggerMessage(1, LogLevel.Information, "Starting event hub client.")]
    partial void LogStartingEventHubClient();

    [LoggerMessage(2, LogLevel.Information, "Event hub client stopped.")]
    partial void LogEventHubClientStopped();

    [LoggerMessage(3, LogLevel.Debug, "Published {count} items to the event hub.")]
    static partial void LogEventHubBatchPublished(ILogger logger, int count);

    [LoggerMessage(4, LogLevel.Error, "An error occurred while publishing {count} items to the event hub.")]
    static partial void LogEventHubPublishError(ILogger logger, int count, Exception error);

}
