using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Nito.AsyncEx;

namespace NRuuviTag;

/// <summary>
/// Base class for an agent that can observe RuuviTag broadcasts and publish them to a 
/// destination.
/// </summary>
public abstract partial class RuuviTagPublisher : IAsyncDisposable {

    /// <summary>
    /// Flags if the publisher has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Logging.
    /// </summary>
    private readonly ILogger<RuuviTagPublisher> _logger;

    /// <summary>
    /// The <see cref="IRuuviTagListener"/> to observe.
    /// </summary>
    private readonly IRuuviTagListener _listener;

    /// <summary>
    /// The publisher options.
    /// </summary>
    private readonly RuuviTagPublisherOptions _options;
    
    /// <summary>
    /// Indicates if a call to <see cref="RunAsync"/> is currently ongoing.
    /// </summary>
    private int _running;

    /// <summary>
    /// Signal that notifies when the publisher is running.
    /// </summary>
    private readonly AsyncManualResetEvent _runningSignal = new AsyncManualResetEvent();
    
    /// <summary>
    /// Signal that notifies when the publisher should emit pending samples.
    /// </summary>
    private readonly AsyncAutoResetEvent _publishSignal = new AsyncAutoResetEvent();
    
    /// <summary>
    /// Raised when a sample has been received.
    /// </summary>
    public Action<RuuviTagSample>? SampleReceived { get; set; }


    /// <summary>
    /// Creates a new <see cref="RuuviTagPublisher"/> object.
    /// </summary>
    /// <param name="listener">
    ///   The <see cref="IRuuviTagListener"/> to observe.
    /// </param>
    /// <param name="options">
    ///   The publisher options.
    /// </param>
    /// <param name="logger">
    ///   The <see cref="ILogger"/> for the agent.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="listener"/> is <see langword="null"/>.
    /// </exception>
    protected RuuviTagPublisher(
        IRuuviTagListener listener, 
        RuuviTagPublisherOptions options,
        ILogger<RuuviTagPublisher>? logger = null
    ) {
        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RuuviTagPublisher>.Instance;
    }
    

    /// <summary>
    /// Waits until the publisher is running.
    /// </summary>
    /// <param name="cancellationToken">
    ///  The cancellation token for the operation.
    /// </param>
    public async ValueTask WaitForRunningAsync(CancellationToken cancellationToken = default) {
        await _runningSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
    }


    /// <summary>
    /// Runs the agent.
    /// </summary>
    /// <param name="cancellationToken">
    ///   A cancellation token that will request cancellation when the agent should stop.
    /// </param>
    /// <returns>
    ///   A <see cref="Task"/> that will run until <paramref name="cancellationToken"/> requests 
    ///   cancellation.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///   The agent is already running.
    /// </exception>
    public async Task RunAsync(CancellationToken cancellationToken) {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) {
            // Already running.
            throw new InvalidOperationException(Resources.Error_PublisherIsAlreadyRunning);
        }

        try {
            _runningSignal.Set();
            await RunAsyncCore(cancellationToken).ConfigureAwait(false);
        }
        finally {
            _runningSignal.Reset();
            Interlocked.Exchange(ref _running, 0);
        }

    }


    /// <summary>
    /// Runs the agent.
    /// </summary>
    /// <param name="cancellationToken">
    ///   A cancellation token that will request cancellation when the agent should stop.
    /// </param>
    /// <returns>
    ///   A <see cref="Task"/> that will run until <paramref name="cancellationToken"/> requests 
    ///   cancellation.
    /// </returns>
    private async Task RunAsyncCore(CancellationToken cancellationToken) {
        var publishChannel = Channel.CreateUnbounded<RuuviTagSample>(new UnboundedChannelOptions() { 
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = false
        });

        var publishInterval = _options.PublishInterval;
        var useBackgroundPublish = publishInterval > TimeSpan.Zero;

        // Samples pending publish, indexed by MAC address.
        var pendingPublish = useBackgroundPublish
            ? new BackgroundPublishQueue(_options.PerDevicePublishBehaviour)
            : null;

        _ = Task.Run(async () => { 
            try {
                await RunAsync(publishChannel, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (ChannelClosedException) { }
            catch (Exception e) {
                LogPublishError(e);
            }
        }, cancellationToken);

        if (useBackgroundPublish) {
            // We're using a scheduled publishing interval - start the background task that
            // will signal when a publish operation should occur.
            _ = Task.Run(async () => {
                try {
                    while (!cancellationToken.IsCancellationRequested) {
                        await Task.Delay(publishInterval, cancellationToken).ConfigureAwait(false);
                        _publishSignal.Set();
                    }
                }
                catch (OperationCanceledException) { }
            }, cancellationToken);
            
            // Start the background publishing task. We use a separate task here instead of just
            // publishing after the Task.Delay above to allow manual triggering of the publish signal.
            _ = Task.Run(async () => {
                try {
                    while (!cancellationToken.IsCancellationRequested) {
                        await _publishSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
                        await PublishPendingSamples(publishChannel, pendingPublish!, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) { }
            }, cancellationToken);
        }

        try {
            // Start the listener
            await foreach (var item in _listener.ListenAsync(cancellationToken).ConfigureAwait(false)) {
                var sample = _options.PrepareForPublish is not null
                    ? _options.PrepareForPublish(item)
                    : item;

                if (string.IsNullOrWhiteSpace(sample?.MacAddress)) {
                    continue;
                }

                if (useBackgroundPublish) {
                    // Add messages to pending publish list.
                    await pendingPublish!.EnqueueAsync(sample, cancellationToken).ConfigureAwait(false);
                }
                else {
                    // Publish immediately.
                    await PublishAsync(publishChannel, sample, cancellationToken).ConfigureAwait(false);
                }

                SampleReceived?.Invoke(sample);
            }
        }
        catch (OperationCanceledException) {
            if (!cancellationToken.IsCancellationRequested) {
                // This exception was not caused by cancellationToken requesting cancellation.
                throw;
            }
        }
        finally {
            if (useBackgroundPublish) {
                // Flush any pending values.
                await PublishPendingSamples(publishChannel, pendingPublish!, default).ConfigureAwait(false);
            }
        }

        return;

        // Publishes all pending samples and clears the pendingPublish dictionary.
        async Task PublishPendingSamples(ChannelWriter<RuuviTagSample> channel, BackgroundPublishQueue pendingSamples, CancellationToken ct) {
            if (!useBackgroundPublish) {
                return;
            }
            
            await PublishAsync(
                channel, 
                await pendingSamples.DequeueAllAsync(ct).ConfigureAwait(false), 
                ct).ConfigureAwait(false);
        }
    }


    /// <summary>
    /// Publishes the specified samples.
    /// </summary>
    /// <param name="samples">
    ///   A <see cref="ChannelReader{T}"/> that will emit the observed samples.
    /// </param>
    /// <param name="cancellationToken">
    ///   The cancellation token for the operation.
    /// </param>
    /// <returns>
    ///   A <see cref="Task"/> that will complete when <paramref name="samples"/> stops 
    ///   emitting new items.
    /// </returns>
    protected abstract Task RunAsync(ChannelReader<RuuviTagSample> samples, CancellationToken cancellationToken);


    /// <summary>
    /// Publishes the specified samples.
    /// </summary>
    /// <param name="channel">
    ///   The channel to publish to.
    /// </param>
    /// <param name="samples">
    ///   The samples.
    /// </param>
    /// <param name="cancellationToken">
    ///   The cancellation token for the operation.
    /// </param>
    /// <returns>
    ///   A <see cref="Task"/> that will perform the publish operation.
    /// </returns>
    private static async ValueTask PublishAsync(ChannelWriter<RuuviTagSample> channel, IReadOnlyList<RuuviTagSample> samples, CancellationToken cancellationToken) {
        if (samples.Count == 0) {
            return;
        }
        
        foreach (var sample in samples) {
            await channel.WriteAsync(sample, cancellationToken).ConfigureAwait(false);
        }
    }


    /// <summary>
    /// Publishes the specified sample.
    /// </summary>
    /// <param name="channel">
    ///   The channel to publish to.
    /// </param>
    /// <param name="sample">
    ///   The sample.
    /// </param>
    /// <param name="cancellationToken">
    ///   The cancellation token for the operation.
    /// </param>
    /// <returns>
    ///   A <see cref="Task"/> that will perform the publish operation.
    /// </returns>
    private static async ValueTask PublishAsync(ChannelWriter<RuuviTagSample> channel, RuuviTagSample sample, CancellationToken cancellationToken) {
        await channel.WriteAsync(sample, cancellationToken).ConfigureAwait(false);
    }
    
    
    /// <summary>
    /// Flushes any pending samples to be published immediately.
    /// </summary>
    /// <remarks>
    ///   This method has no effect if the publisher is not configured to use a scheduled 
    ///   publishing interval.
    /// </remarks>
    public void Flush() => _publishSignal.Set();


    /// <inheritdoc/>
    async ValueTask IAsyncDisposable.DisposeAsync() {
        if (_disposed) {
            return;
        }

        await DisposeAsyncCore().ConfigureAwait(false);
        _disposed = true;
    }


    /// <summary>
    /// Performs tasks related to freeing unmanaged resources asynchronously.
    /// </summary>
    /// <returns>
    ///   A <see cref="ValueTask"/> that will perform any required clean-up.
    /// </returns>
    protected virtual ValueTask DisposeAsyncCore() {
        return default;
    }


    [LoggerMessage(1, LogLevel.Error, "Error during publish.")]
    partial void LogPublishError(Exception error);


    /// <summary>
    /// Holds pending samples to be published in the background.
    /// </summary>
    private class BackgroundPublishQueue {
        
        private readonly BatchPublishDeviceBehaviour _publishBehaviour;
        
        private long _count;
        
        private readonly AsyncLock _lock = new AsyncLock();

        private readonly Dictionary<string, DeviceBackgroundPublishQueue> _queue = new Dictionary<string, DeviceBackgroundPublishQueue>(StringComparer.OrdinalIgnoreCase);
        
        public long Count => Interlocked.Read(ref _count);
        
        
        public BackgroundPublishQueue(BatchPublishDeviceBehaviour publishBehaviour) {
            _publishBehaviour = publishBehaviour;
        }


        public async ValueTask EnqueueAsync(RuuviTagSample sample, CancellationToken cancellationToken) {
            if (string.IsNullOrEmpty(sample.MacAddress)) {
                return;
            }
            
            using var _ = await _lock.LockAsync(cancellationToken).ConfigureAwait(false);
            
            if (!_queue.TryGetValue(sample.MacAddress, out var queue)) {
                queue = new DeviceBackgroundPublishQueue(_publishBehaviour);
                _queue[sample.MacAddress] = queue;
            }

            queue.Enqueue(sample);
            Interlocked.Increment(ref _count);
        }
        
        
        public async ValueTask<IReadOnlyList<RuuviTagSample>> DequeueAllAsync(CancellationToken cancellationToken) {
            using var _ = await _lock.LockAsync(cancellationToken).ConfigureAwait(false);
            
            var allSamples = new List<IReadOnlyList<RuuviTagSample>>(_queue.Count);
            
            foreach (var queue in _queue.Values) {
                var samples = queue.DequeueAll();
                if (samples.Count > 0) {
                    allSamples.Add(samples);
                }
            }

            _queue.Clear();
            Interlocked.Exchange(ref _count, 0);
            
            return allSamples.SelectMany(x => x).ToList();
        }

    }
    

    /// <summary>
    /// Holds pending samples for a specific device to be published in the background.
    /// </summary>
    private class DeviceBackgroundPublishQueue {

        private readonly BatchPublishDeviceBehaviour _publishBehaviour;
        
        private RuuviTagSample? _latestSample;
        
        private ImmutableArray<RuuviTagSample> _samples = [];


        public DeviceBackgroundPublishQueue(BatchPublishDeviceBehaviour publishBehaviour) {
            _publishBehaviour = publishBehaviour;
        }
        
        
        public void Enqueue(RuuviTagSample sample) {
            if (_publishBehaviour == BatchPublishDeviceBehaviour.LatestSampleOnly) {
                _latestSample = sample;
                return;
            }
            _samples = _samples.Add(sample);
        }
        
        
        public IReadOnlyList<RuuviTagSample> DequeueAll() {
            if (_publishBehaviour == BatchPublishDeviceBehaviour.LatestSampleOnly) {
                if (_latestSample is null) {
                    return [];
                }
                    
                var sample = _latestSample;
                _latestSample = null;
                return [sample];
            }
                
            var samples = _samples;
            _samples = [];
            return samples;
        }

    }

}
