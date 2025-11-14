using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NRuuviTag.Http;

using Spectre.Console.Cli;

namespace NRuuviTag.Cli.Commands;

public class PublishHttpCommand : AsyncCommand<PublishHttpCommand.Settings> {
    
    /// <summary>
    /// The <see cref="IRuuviTagListenerFactory"/> for creating listeners with.
    /// </summary>
    private readonly IRuuviTagListenerFactory _listenerFactory;
    
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
    /// <param name="listenerFactory">
    ///   The <see cref="IRuuviTagListener"/> to listen to broadcasts with.
    /// </param>
    /// <param name="httpClientFactory">
    ///   The <see cref="IHttpClientFactory"/> to use for creating HTTP clients.
    /// </param>
    /// <param name="appLifetime">
    ///   The <see cref="IHostApplicationLifetime"/> for the .NET host application.
    /// </param>
    /// <param name="loggerFactory">
    ///   The <see cref="ILoggerFactory"/> for the application.
    /// </param>
    public PublishHttpCommand(
        IRuuviTagListenerFactory listenerFactory, 
        IHttpClientFactory httpClientFactory,
        IHostApplicationLifetime appLifetime, 
        ILoggerFactory loggerFactory
    ) {
        _listenerFactory = listenerFactory;
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
        _appLifetime = appLifetime;
    }


    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) {
        if (!_appLifetime.ApplicationStarted.IsCancellationRequested) {
            try { await Task.Delay(-1, _appLifetime.ApplicationStarted).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        
        var listener = _listenerFactory.CreateListener(settings.Bind);
        
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
            MaximumBatchSize = settings.MaximumBatchSize
        };

        await using var publisher = new HttpPublisher(listener, publisherOptions, _httpClientFactory, _loggerFactory.CreateLogger<HttpPublisher>());

        using var ctSource = CancellationTokenSource.CreateLinkedTokenSource(_appLifetime.ApplicationStopped, _appLifetime.ApplicationStopping);
        try {
            await publisher.RunAsync(ctSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ctSource.IsCancellationRequested) { }

        return 0;
    }


    public class Settings : PublishCommandSettings {

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
        
        [CommandOption("--batch-size-limit <SIZE>")]
        [Description("The maximum number of samples to include in a single batch when publishing to the HTTP endpoint.")]
        [DefaultValue(50)]
        [Range(1, 10_000)]
        public int MaximumBatchSize { get; set; } = 50;
    }
    
}
