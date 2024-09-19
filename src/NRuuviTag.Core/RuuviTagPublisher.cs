using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace NRuuviTag {

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
        /// The publish interval to use, in seconds.
        /// </summary>
        private readonly int _publishInterval;

        /// <summary>
        /// A callback that receives the MAC address for a RuuviTag broadcast and returns a flag 
        /// that indicates if the publisher should process the broadcast.
        /// </summary>
        Func<string, bool>? _filter;

        /// <summary>
        /// Indicates if a call to <see cref="RunAsync"/> is currently ongoing.
        /// </summary>
        private int _running;


        /// <summary>
        /// Creates a new <see cref="RuuviTagPublisher"/> object.
        /// </summary>
        /// <param name="listener">
        ///   The <see cref="IRuuviTagListener"/> to observe.
        /// </param>
        /// <param name="publishInterval">
        ///   The publish interval to use, in seconds. A value of zero indicates that samples 
        ///   should be published as soon as they are observed.
        /// </param>
        /// <param name="filter">
        ///   A callback that receives the MAC address for a RuuviTag advertisement and returns a 
        ///   flag that indicates if the publisher should process the advertisement.
        /// </param>
        /// <param name="logger">
        ///   The <see cref="ILogger"/> for the agent.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="listener"/> is <see langword="null"/>.
        /// </exception>
        protected RuuviTagPublisher(
            IRuuviTagListener listener, 
            int publishInterval, 
            Func<string, bool>? filter = null, 
            ILogger<RuuviTagPublisher>? logger = null
        ) {
            _listener = listener ?? throw new ArgumentNullException(nameof(listener));
            _publishInterval = publishInterval;
            _filter = filter;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RuuviTagPublisher>.Instance;
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
                await RunAsyncCore(cancellationToken).ConfigureAwait(false);
            }
            finally {
                _running = 0;
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
                SingleWriter = true
            });

            var useBackgroundPublish = _publishInterval > 0;

            // Samples pending publish, indexed by MAC address.
            var pendingPublish = useBackgroundPublish
                ? new Dictionary<string, RuuviTagSample>(StringComparer.OrdinalIgnoreCase)
                : null;

            _ = Task.Run(async () => { 
                try {
                    await RunAsync(publishChannel.Reader.ReadAllAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
                catch (ChannelClosedException) { }
                catch (Exception e) {
                    LogPublishError(e);
                }
            }, cancellationToken);

            // Publishes all pending samples and clears the pendingPublish dictionary.
            async Task PublishPendingSamples(CancellationToken ct) {
                if (!useBackgroundPublish) {
                    return;
                }

                RuuviTagSample[] samples;
                lock (pendingPublish!) {
                    samples = pendingPublish.Values.ToArray();
                    pendingPublish.Clear();
                }

                if (samples.Length == 0) {
                    return;
                }

                await PublishAsync(publishChannel, samples, cancellationToken).ConfigureAwait(false);
            }

            if (useBackgroundPublish) {
                // We're using a scheduled publish interval - start the background task that
                // will perform this job.
                _ = Task.Run(async () => {
                    try {
                        while (!cancellationToken.IsCancellationRequested) {
                            await Task.Delay(TimeSpan.FromSeconds(_publishInterval), cancellationToken).ConfigureAwait(false);
                            await PublishPendingSamples(cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException) { }
                }, cancellationToken);
            }

            try {
                // Start the listener
                await foreach (var item in _listener.ListenAsync(_filter, cancellationToken).ConfigureAwait(false)) {
                    if (string.IsNullOrWhiteSpace(item?.MacAddress)) {
                        continue;
                    }

                    if (useBackgroundPublish) {
                        // Add messages to pending publish list.
                        lock (pendingPublish!) {
                            pendingPublish[item!.MacAddress!] = item;
                        }
                    }
                    else {
                        // Publish immediately.
                        await PublishAsync(publishChannel, item!, cancellationToken).ConfigureAwait(false);
                    }
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
                    await PublishPendingSamples(default).ConfigureAwait(false);
                }
            }
        }


        /// <summary>
        /// Publishes the specified samples.
        /// </summary>
        /// <param name="samples">
        ///   An <see cref="IAsyncEnumerable{RuuviTagSample}"/> that will emit the observed samples.
        /// </param>
        /// <param name="cancellationToken">
        ///   The cancellation token for the operation.
        /// </param>
        /// <returns>
        ///   A <see cref="Task"/> that will complete when <paramref name="samples"/> stops 
        ///   emitting new items.
        /// </returns>
        protected abstract Task RunAsync(IAsyncEnumerable<RuuviTagSample> samples, CancellationToken cancellationToken);


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
        private async ValueTask PublishAsync(ChannelWriter<RuuviTagSample> channel, IEnumerable<RuuviTagSample> samples, CancellationToken cancellationToken) {
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
        /// <param name="samples">
        ///   The samples.
        /// </param>
        /// <param name="cancellationToken">
        ///   The cancellation token for the operation.
        /// </param>
        /// <returns>
        ///   A <see cref="Task"/> that will perform the publish operation.
        /// </returns>
        private async ValueTask PublishAsync(ChannelWriter<RuuviTagSample> channel, RuuviTagSample sample, CancellationToken cancellationToken) {
            await channel.WriteAsync(sample, cancellationToken).ConfigureAwait(false);
        }


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

    }
}
