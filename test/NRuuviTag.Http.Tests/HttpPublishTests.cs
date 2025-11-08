using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NRuuviTag.Http.Tests;

[TestClass]
public sealed class HttpPublishTests {

    public TestContext TestContext { get; set; }
    
    
    private void ConfigureApplicationBuilder(HostApplicationBuilder builder, Action<HttpPublisherOptions> configureOptions, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> httpHandlerCallback) {
        builder.Services.AddLogging(options => {
            options.AddFilter("NRuuviTag", LogLevel.Trace);
        });
        
        var http = builder.Services.AddHttpClient<HttpPublisher>();
        http.AddStandardResilienceHandler().Configure(options => {
            options.Retry.Delay = TimeSpan.FromMilliseconds(50);
        });
        http.AddHttpMessageHandler(() => new CallbackHttpHandler(httpHandlerCallback));
        
        builder.Services.AddSingleton<TestRuuviTagListener>();
        builder.Services.AddSingleton<IRuuviTagListener>(provider => provider.GetRequiredService<TestRuuviTagListener>());
        
        builder.Services.AddOptions<HttpPublisherOptions>().Configure(options => {
            options.Endpoint = new Uri($"http://{TestContext.TestName}/telemetry");
            configureOptions.Invoke(options);
        });
        builder.Services.AddSingleton(provider => {
            var options = provider.GetRequiredService<IOptions<HttpPublisherOptions>>();
            return ActivatorUtilities.CreateInstance<HttpPublisher>(provider, options.Value);
        });
        
        builder.Services.AddHostedService<HttpPublisherBackgroundService>();
    }
    
    
    [TestMethod]
    public async Task ShouldPublishSingleSample() {
        var channel = Channel.CreateUnbounded<RuuviTagSample>(
            new UnboundedChannelOptions() {
                AllowSynchronousContinuations = false,
                SingleReader = true,
                SingleWriter = true
            });
        
        var builder = Host.CreateApplicationBuilder();
        
        ConfigureApplicationBuilder(
            builder,
            options => {
                options.MaximumBatchSize = 1;
            },
            async (request, ct) => {
                await foreach (var sample in request.Content!.ReadFromJsonAsAsyncEnumerable<RuuviTagSample>(ct)) {
                    await channel.Writer.WriteAsync(sample!, ct);
                }

                return new HttpResponseMessage(HttpStatusCode.Accepted) {
                    RequestMessage = request
                };
            });
        
        using var host = builder.Build();
        await host.StartAsync(TestContext.CancellationTokenSource.Token);
        
        var listener = host.Services.GetRequiredService<TestRuuviTagListener>();
        await listener.WaitForListenStartedAsync(TestContext.CancellationTokenSource.Token);
        
        var sample = new RuuviTagSample() {
            Timestamp = DateTimeOffset.UtcNow,
            MacAddress = "AA:BB:CC:DD:EE:FF",
            Temperature = 100
        };
        
        await listener.PublishAsync(sample);
        
        TestContext.CancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(5));
        
        RuuviTagSample receivedSample = null!;
        
        if (!await channel.Reader.WaitToReadAsync(TestContext.CancellationToken) || !channel.Reader.TryRead(out receivedSample!)) {
            Assert.Fail("No sample received");
        }
        
        Assert.AreEqual(sample, receivedSample);
    }
    
    
    [TestMethod]
    public async Task ShouldPublishMultipleSamples() {
        var channel = Channel.CreateUnbounded<RuuviTagSample>(
            new UnboundedChannelOptions() {
                AllowSynchronousContinuations = false,
                SingleReader = true,
                SingleWriter = true
            });
        
        var builder = Host.CreateApplicationBuilder();
        
        ConfigureApplicationBuilder(
            builder,
            options => {
                options.MaximumBatchSize = 2;
            },
            async (request, ct) => {
                await foreach (var sample in request.Content!.ReadFromJsonAsAsyncEnumerable<RuuviTagSample>(ct)) {
                    await channel.Writer.WriteAsync(sample!, ct);
                }

                return new HttpResponseMessage(HttpStatusCode.Accepted) {
                    RequestMessage = request
                };
            });
        
        using var host = builder.Build();
        await host.StartAsync(TestContext.CancellationTokenSource.Token);
        
        var listener = host.Services.GetRequiredService<TestRuuviTagListener>();
        await listener.WaitForListenStartedAsync(TestContext.CancellationTokenSource.Token);
        
        RuuviTagSample[] samples = [
            new RuuviTagSample() {
                Timestamp = DateTimeOffset.UtcNow,
                MacAddress = "AA:BB:CC:DD:EE:FF",
                Temperature = 100
            },
            new RuuviTagSample() {
                Timestamp = DateTimeOffset.UtcNow,
                MacAddress = "11:22:33:44:55:66",
                Humidity = 50
            },
            new RuuviTagSample() {
                Timestamp = DateTimeOffset.UtcNow,
                MacAddress = "77:88:99:AA:BB:CC",
                Pressure = 1013.25
            },
            new RuuviTagSample() {
                Timestamp = DateTimeOffset.UtcNow,
                MacAddress = "DD:EE:FF:00:11:22",
                AccelerationX = 1.0
            },
            new RuuviTagSample() {
                Timestamp = DateTimeOffset.UtcNow,
                MacAddress = "33:44:55:66:77:88",
                AccelerationY = 2.0
            }
        ];
        
        listener.Publish(samples);
        
        TestContext.CancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(5));
        
        var receivedSamples = new List<RuuviTagSample>();

        while (await channel.Reader.WaitToReadAsync(TestContext.CancellationTokenSource.Token)) {
            while (channel.Reader.TryRead(out var sample)) {
                receivedSamples.Add(sample);
            }

            if (receivedSamples.Count >= samples.Length) {
                break;
            }
        }
        
        Assert.HasCount(samples.Length, receivedSamples);
        CollectionAssert.AreEquivalent(samples, receivedSamples);
    }
    
    
    [TestMethod]
    public async Task ShouldRetryPublishOnTransientError() {
        var channel = Channel.CreateUnbounded<RuuviTagSample>(
            new UnboundedChannelOptions() {
                AllowSynchronousContinuations = false,
                SingleReader = true,
                SingleWriter = true
            });
        
        var builder = Host.CreateApplicationBuilder();
        
        var requestCount = 0;
        
        ConfigureApplicationBuilder(
            builder,
            options => {
                options.MaximumBatchSize = 1;
            },
            async (request, ct) => {
                if (++requestCount == 1) {
                    throw new HttpRequestException(HttpRequestError.Unknown);
                }
                
                await foreach (var sample in request.Content!.ReadFromJsonAsAsyncEnumerable<RuuviTagSample>(ct)) {
                    await channel.Writer.WriteAsync(sample!, ct);
                }

                return new HttpResponseMessage(HttpStatusCode.Accepted) {
                    RequestMessage = request
                };
            });
        
        using var host = builder.Build();
        await host.StartAsync(TestContext.CancellationTokenSource.Token);
        
        var listener = host.Services.GetRequiredService<TestRuuviTagListener>();
        await listener.WaitForListenStartedAsync(TestContext.CancellationTokenSource.Token);
        
        var sample = new RuuviTagSample() {
            Timestamp = DateTimeOffset.UtcNow,
            MacAddress = "AA:BB:CC:DD:EE:FF",
            Temperature = 100
        };
        
        await listener.PublishAsync(sample);
        
        TestContext.CancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(5));
        
        RuuviTagSample receivedSample = null!;
        
        if (!await channel.Reader.WaitToReadAsync(TestContext.CancellationToken) || !channel.Reader.TryRead(out receivedSample!)) {
            Assert.Fail("No sample received");
        }
        
        Assert.AreEqual(sample, receivedSample);
    }


    [TestMethod]
    public async Task ShouldPublishLatestSampleOnly() {
        var channel = Channel.CreateUnbounded<RuuviTagSample>(
            new UnboundedChannelOptions() {
                AllowSynchronousContinuations = false,
                SingleReader = true,
                SingleWriter = true
            });
        
        var builder = Host.CreateApplicationBuilder();
        
        ConfigureApplicationBuilder(
            builder,
            options => {
                options.PublishInterval = TimeSpan.FromMinutes(1);
                options.PerDevicePublishBehaviour = BatchPublishDeviceBehaviour.LatestSampleOnly;
            },
            async (request, ct) => {
                await foreach (var sample in request.Content!.ReadFromJsonAsAsyncEnumerable<RuuviTagSample>(ct)) {
                    await channel.Writer.WriteAsync(sample!, ct);
                }

                return new HttpResponseMessage(HttpStatusCode.Accepted) {
                    RequestMessage = request
                };
            });
        
        using var host = builder.Build();
        await host.StartAsync(TestContext.CancellationTokenSource.Token);
        
        var listener = host.Services.GetRequiredService<TestRuuviTagListener>();
        await listener.WaitForListenStartedAsync(TestContext.CancellationTokenSource.Token);
        
        var publisher = host.Services.GetRequiredService<HttpPublisher>();
        await publisher.WaitForRunningAsync(TestContext.CancellationTokenSource.Token);

        IReadOnlyList<RuuviTagSample> samples = [
            ..Enumerable.Range(1, 50).Select(x => new RuuviTagSample() {
                Timestamp = DateTimeOffset.UtcNow.AddSeconds(x),
                MacAddress = "AA:BB:CC:DD:EE:FF",
                Temperature = x
            })
        ];
        
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var samplesPublished = 0;
        publisher.SampleReceived += _ => {
            ++samplesPublished;
            if (samplesPublished >= samples.Count) {
                tcs.TrySetResult();
            }
        };
        
        await listener.PublishAsync(samples);
        await tcs.Task;
        publisher.Flush();
        
        TestContext.CancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(5));
        
        RuuviTagSample receivedSample = null!;
        
        if (!await channel.Reader.WaitToReadAsync(TestContext.CancellationToken) || !channel.Reader.TryRead(out receivedSample!)) {
            Assert.Fail("No sample received");
        }
        
        Assert.AreEqual(samples[^1], receivedSample);
    }
    
    
    [TestMethod]
    public async Task ShouldPublishAllSamples() {
        var channel = Channel.CreateUnbounded<RuuviTagSample>(
            new UnboundedChannelOptions() {
                AllowSynchronousContinuations = false,
                SingleReader = true,
                SingleWriter = true
            });
        
        var builder = Host.CreateApplicationBuilder();
        
        ConfigureApplicationBuilder(
            builder,
            options => {
                options.PublishInterval = TimeSpan.FromMinutes(1);
                options.PerDevicePublishBehaviour = BatchPublishDeviceBehaviour.AllSamples;
                options.MaximumBatchSize = 100;
            },
            async (request, ct) => {
                await foreach (var sample in request.Content!.ReadFromJsonAsAsyncEnumerable<RuuviTagSample>(ct)) {
                    await channel.Writer.WriteAsync(sample!, ct);
                }

                return new HttpResponseMessage(HttpStatusCode.Accepted) {
                    RequestMessage = request
                };
            });
        
        using var host = builder.Build();
        await host.StartAsync(TestContext.CancellationTokenSource.Token);
        
        var listener = host.Services.GetRequiredService<TestRuuviTagListener>();
        await listener.WaitForListenStartedAsync(TestContext.CancellationTokenSource.Token);
        
        var publisher = host.Services.GetRequiredService<HttpPublisher>();
        await publisher.WaitForRunningAsync(TestContext.CancellationTokenSource.Token);

        List<RuuviTagSample> samples = [
            ..Enumerable.Range(1, 50).Select(x => new RuuviTagSample() {
                Timestamp = DateTimeOffset.UtcNow.AddSeconds(x),
                MacAddress = "AA:BB:CC:DD:EE:FF",
                Temperature = x
            })
        ];

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var samplesPublished = 0;
        publisher.SampleReceived += _ => {
            ++samplesPublished;
            if (samplesPublished >= samples.Count) {
                tcs.TrySetResult();
            }
        };
        
        await listener.PublishAsync(samples);
        await tcs.Task;
        publisher.Flush();
        
        TestContext.CancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(5));
        
        var receivedSamples = new List<RuuviTagSample>(samples.Count);

        if (await channel.Reader.WaitToReadAsync(TestContext.CancellationToken)) {
            while (channel.Reader.TryRead(out var sample)) {
                receivedSamples.Add(sample);
            }
        }
        
        CollectionAssert.AreEquivalent(samples, receivedSamples);
    }


    private sealed class CallbackHttpHandler : DelegatingHandler {
        
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;
        
        public CallbackHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => _handler.Invoke(request, cancellationToken);

    }
    
    
    private sealed class HttpPublisherBackgroundService : BackgroundService {
        
        private readonly HttpPublisher _publisher;
        
        public HttpPublisherBackgroundService(HttpPublisher publisher) {
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        }
        
        /// <inheritdoc />
        protected override Task ExecuteAsync(CancellationToken stoppingToken) => _publisher.RunAsync(stoppingToken);

        

    }
    
}
