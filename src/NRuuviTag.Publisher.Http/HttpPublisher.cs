using System.Net.Http.Json;
using System.Threading.Channels;

using Microsoft.Extensions.Logging;

namespace NRuuviTag.Http;

/// <summary>
/// Observes measurements emitted by an <see cref="IRuuviTagListener"/> and publishes them to 
/// an HTTP endpoint.
/// </summary>
public partial class HttpPublisher : RuuviTagPublisher {
    
    private readonly ILogger<HttpPublisher> _logger;
    
    private readonly IHttpClientFactory _httpClientFactory;
    
    private readonly HttpPublisherOptions _options;
    
    private readonly bool _useHttpPut;
    

    /// <summary>
    /// Creates a new <see cref="HttpPublisher"/> instance.
    /// </summary>
    /// <param name="listener">
    ///   The <see cref="IRuuviTagListener"/> to listen to observe.
    /// </param>
    /// <param name="options">
    ///   The publisher options.
    /// </param>
    /// <param name="httpClientFactory">
    ///   The <see cref="IHttpClientFactory"/> to use for creating HTTP clients.
    /// </param>
    /// <param name="logger">
    ///   The logger for the publisher.
    /// </param>
    /// <remarks></remarks>
    public HttpPublisher(IRuuviTagListener listener, HttpPublisherOptions options, IHttpClientFactory httpClientFactory, ILogger<HttpPublisher>? logger = null) : base(listener, options, logger) {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _useHttpPut = HttpMethod.Put.Method.Equals(_options.HttpMethod, StringComparison.OrdinalIgnoreCase);
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<HttpPublisher>.Instance;
    }


    /// <inheritdoc />
    protected override async Task RunAsync(ChannelReader<RuuviTagSample> samples, CancellationToken cancellationToken) {
        LogPublisherRunning(_options.Endpoint, _options.HttpMethod);
        
        var batch = new List<RuuviTagSample>(_options.MaximumBatchSize);
        
        while (await samples.WaitToReadAsync(cancellationToken).ConfigureAwait(false)) {
            while (samples.TryRead(out var item)) {
                batch.Add(item);
                if (batch.Count < _options.MaximumBatchSize) {
                    continue;
                }

                await PublishBatchAsync(batch, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }

            if (batch.Count == 0) {
                continue;
            }

            await PublishBatchAsync(batch, cancellationToken).ConfigureAwait(false);
            batch.Clear();
        }
    }


    private async Task PublishBatchAsync(IReadOnlyList<RuuviTagSample> samples, CancellationToken cancellationToken) {
        var http = _httpClientFactory.CreateClient(nameof(HttpPublisher));
            
        if (_options.Headers is not null) {
            foreach (var (key, value) in _options.Headers) {
                http.DefaultRequestHeaders.Add(key, value);
            }
        }

        LogPublishingBatch(samples.Count);

        try {
            if (_useHttpPut) {
                await http.PutAsJsonAsync(_options.Endpoint, samples, RuuviJsonSerializerContext.Default.IReadOnlyListRuuviTagSample, cancellationToken).ConfigureAwait(false);
                return;
            }

            await http.PostAsJsonAsync(_options.Endpoint, samples, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception e) {
            LogPublishBatchError(samples.Count, e);
        }
    }


    [LoggerMessage(101, LogLevel.Information, "HTTP publisher is running. Endpoint = {endpoint}, HTTP method = {httpMethod}")]
    partial void LogPublisherRunning(Uri endpoint, string httpMethod);
    

    [LoggerMessage(102, LogLevel.Trace, "Publishing {count} samples.")]
    partial void LogPublishingBatch(int count);


    [LoggerMessage(103, LogLevel.Error, "An error occurred while publishing a batch of {count} samples.")]
    partial void LogPublishBatchError(int count, Exception error);

}
