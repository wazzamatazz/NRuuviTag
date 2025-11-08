using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NRuuviTag.Http;

using Spectre.Console.Cli;

namespace NRuuviTag.Cli.Commands;

public class PublishHttpCommand : AsyncCommand<PublishHttpCommand.Settings> {
    
    /// <summary>
    /// The <see cref="IRuuviTagListener"/> to listen to broadcasts with.
    /// </summary>
    private readonly IRuuviTagListener _listener;
    
    /// <summary>
    /// The known RuuviTag devices.
    /// </summary>
    private readonly IOptionsMonitor<DeviceCollection> _devices;
    
    /// <summary>
    /// The <see cref="IHttpClientFactory"/> to use for creating HTTP clients.
    /// </summary>
    private readonly IHttpClientFactory _httpClientFactory;

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
    /// <param name="httpClientFactory">
    ///   The <see cref="IHttpClientFactory"/> to use for creating HTTP clients.
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
    public PublishHttpCommand(
        IRuuviTagListener listener, 
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<DeviceCollection> devices, 
        IHostApplicationLifetime appLifetime, 
        ILoggerFactory loggerFactory
    ) {
        _listener = listener;
        _httpClientFactory = httpClientFactory;
        _devices = devices;
        _loggerFactory = loggerFactory;
        _appLifetime = appLifetime;
    }


    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) {
        if (!_appLifetime.ApplicationStarted.IsCancellationRequested) {
            try { await Task.Delay(-1, _appLifetime.ApplicationStarted).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        
        IEnumerable<Device> devices = null!;
        UpdateDevices(_devices.CurrentValue);
        
        var headers = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);
        if (settings.Headers is { Length: > 0 }) {
            foreach (var header in settings.Headers) {
                var separatorIndex = header.IndexOf(':');
                if (separatorIndex < 1 || separatorIndex >= header.Length - 1) {
                    // Invalid header format, skip it.
                    continue;
                }

                var name = header[..separatorIndex].Trim();
                var value = header[(separatorIndex + 1)..].Trim();
                headers[name] = value;
            }
        }
        
        var publisherOptions = new HttpPublisherOptions() {
            Endpoint = settings.Endpoint,
            HttpMethod = settings.HttpMethod,
            Headers = headers.ToImmutable(),
            PublishInterval = TimeSpan.FromSeconds(settings.PublishInterval),
            PerDevicePublishBehaviour = settings.PublishBehaviour,
            MaximumBatchSize = settings.MaximumBatchSize,
            KnownDevicesOnly = settings.KnownDevicesOnly,
            GetDeviceInfo = addr => {
                lock (this) {
                    return devices.FirstOrDefault(x => MacAddressComparer.Instance.Equals(addr, x.MacAddress));
                }
            }
        };

        await using var publisher = new HttpPublisher(_listener, publisherOptions, _httpClientFactory, _loggerFactory.CreateLogger<HttpPublisher>());

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


    public class Settings : CommandSettings {

        [CommandArgument(0, "<ENDPOINT>")]
        [Description("The HTTP endpoint to publish samples to.")]
        public Uri Endpoint { get; set; } = default!;
        
        [CommandOption("--method <METHOD>")]
        [Description("The HTTP method to use when publishing samples. Possible values are: POST (default), PUT.")]
        [DefaultValue("POST")]
        [AllowedValues("POST", "PUT")]
        public string HttpMethod { get; set; } = "POST";
        
        [CommandOption("--header <HEADER>")]
        [Description("Specifies a header to include in HTTP requests to the publish endpoint, in the format 'Name: Value'. This option can be specified multiple times to include multiple headers.")]
        public string[] Headers { get; set; } = default!;
        
        [CommandOption("--publish-interval <INTERVAL>")]
        [Description("The publish to use, in seconds. When a publish interval is specified, the '--publish-behaviour' setting controls if all observed samples for a device are included in the next publish, or if only the most-recent reading for each device are included. If a publish inteval is not specified, samples will be published to the MQTT server as soon as they are observed.")]
        public int PublishInterval { get; set; }
    
        [CommandOption("--publish-behaviour <BEHAVIOUR>")]
        [Description("The per-device publish behaviour to use when a non-zero publish interval is specified. Possible values are: " + nameof(BatchPublishDeviceBehaviour.AllSamples) + " (default), " + nameof(BatchPublishDeviceBehaviour.LatestSampleOnly))]
        [DefaultValue(BatchPublishDeviceBehaviour.AllSamples)]
        public BatchPublishDeviceBehaviour PublishBehaviour { get; set; }
        
        [CommandOption("--batch-size-limit <SIZE>")]
        [Description("The maximum number of samples to include in a single batch when publishing to the HTTP endpoint.")]
        [DefaultValue(50)]
        [Range(1, 10_000)]
        public int MaximumBatchSize { get; set; } = 50;
        
        [CommandOption("--known-devices")]
        [Description("Specifies if only samples from pre-registered devices should be published to the HTTP endpoint.")]
        public bool KnownDevicesOnly { get; set; }

    }
    
}
