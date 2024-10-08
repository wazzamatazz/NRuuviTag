﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;

using Microsoft.Extensions.Logging;

namespace NRuuviTag.AzureEventHubs {

    /// <summary>
    /// Observes measurements emitted by an <see cref="IRuuviTagListener"/> and publishes them to 
    /// an Azure Event Hub.
    /// </summary>
    public partial class AzureEventHubAgent : RuuviTagPublisher {

        /// <summary>
        /// Logging.
        /// </summary>
        private readonly ILogger<AzureEventHubAgent> _logger;

        /// <summary>
        /// A delegate that retrieves the device information for a sample based on the MAC address 
        /// of the device.
        /// </summary>
        private readonly Func<string, Device?>? _getDeviceInfo;

        /// <summary>
        /// A delegate that can be used to make final modifications to a sample prior to 
        /// publishing it to the event hub.
        /// </summary>
        private readonly Action<RuuviTagSampleExtended>? _prepareForPublish;

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
        /// Creates a new <see cref="AzureEventHubAgent"/> object.
        /// </summary>
        /// <param name="listener">
        ///   The <see cref="IRuuviTagListener"/> to observe for sensor readings.
        /// </param>
        /// <param name="options">
        ///   Agent options.
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
        public AzureEventHubAgent(IRuuviTagListener listener, AzureEventHubAgentOptions options, ILoggerFactory? loggerFactory = null) 
            : base(listener, options?.SampleRate ?? 0, BuildFilterDelegate(options!), loggerFactory?.CreateLogger<RuuviTagPublisher>()) { 
            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }
            Validator.ValidateObject(options, new ValidationContext(options), true);

            _logger = loggerFactory?.CreateLogger<AzureEventHubAgent>() ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AzureEventHubAgent>.Instance;
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
        private static Func<string, bool> BuildFilterDelegate(AzureEventHubAgentOptions options) {
            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            if (!options.KnownDevicesOnly) {
                return addr => true;
            }

            var getDeviceInfo = options.GetDeviceInfo;
            return getDeviceInfo == null
                ? addr => false
                : addr => getDeviceInfo.Invoke(addr) != null;
        }


        protected override async Task RunAsync(IAsyncEnumerable<RuuviTagSample> samples, CancellationToken cancellationToken) {
            LogStartingEventHubClient();
            
            await using (var client = new EventHubProducerClient(_connectionString, _eventHubName)) {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var batch = await client.CreateBatchAsync(cancellationToken).ConfigureAwait(false);
                TimeSpan currentBatchStartedAt = TimeSpan.Zero;

                async Task PublishBatch() {
                    try {
                        await client!.SendAsync(batch).ConfigureAwait(false);
                        LogEventHubBatchPublished(batch.Count);
                        batch = await client.CreateBatchAsync().ConfigureAwait(false);
                    }
                    catch (Exception e) {
                        LogEventHubPublishError(batch.Count, e);
                    }
                }

                try {
                    await foreach (var item in samples.ConfigureAwait(false)) {
                        var device = _getDeviceInfo?.Invoke(item.MacAddress!);
                        var sample = RuuviTagSampleExtended.Create(item, device?.DeviceId, device?.DisplayName);
                        _prepareForPublish?.Invoke(sample);
                        var eventData = new EventData(JsonSerializer.SerializeToUtf8Bytes(sample, _jsonOptions));
                        eventData.Properties["Content-Type"] = "application/json";

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

                        await PublishBatch().ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) {
                    if (batch?.Count > 0) {
                        await PublishBatch().ConfigureAwait(false);
                    }
                }
                finally {
                    batch?.Dispose();
                    LogEventHubClientStopped();
                }
            }
        }


        [LoggerMessage(1, LogLevel.Information, "Starting event hub client.")]
        partial void LogStartingEventHubClient();

        [LoggerMessage(2, LogLevel.Information, "Event hub client stopped.")]
        partial void LogEventHubClientStopped();

        [LoggerMessage(3, LogLevel.Debug, "Published {count} items to the event hub.")]
        partial void LogEventHubBatchPublished(int count);

        [LoggerMessage(4, LogLevel.Error, "An error occurred while publishing {count} items to the event hub.")]
        partial void LogEventHubPublishError(int count, Exception error);

    }
}
