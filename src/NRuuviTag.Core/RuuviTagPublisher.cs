using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace NRuuviTag {

    /// <summary>
    /// Base class for an agent that can observe RuuviTag broadcasts and publish them to a 
    /// destination.
    /// </summary>
    public abstract class RuuviTagPublisher {

        /// <summary>
        /// Logging.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// The <see cref="IRuuviTagListener"/> to observe.
        /// </summary>
        private readonly IRuuviTagListener _listener;

        /// <summary>
        /// The publish interval to use, in seconds.
        /// </summary>
        private readonly uint _publishInterval;

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
        protected RuuviTagPublisher(IRuuviTagListener listener, uint publishInterval, Func<string, bool>? filter = null, ILogger? logger = null) {
            _listener = listener ?? throw new ArgumentNullException(nameof(listener));
            _publishInterval = publishInterval;
            _filter = filter;
            Logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
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

            await using (var context = new RuuviTagPublisherContext()) {
                try {
                    await RunAsyncCore(context, cancellationToken).ConfigureAwait(false);
                }
                finally {
                    _running = 0;
                }
            }
        }


        /// <summary>
        /// Runs the agent.
        /// </summary>
        /// <param name="context">
        ///   The context for the operation.
        /// </param>
        /// <param name="cancellationToken">
        ///   A cancellation token that will request cancellation when the agent should stop.
        /// </param>
        /// <returns>
        ///   A <see cref="Task"/> that will run until <paramref name="cancellationToken"/> requests 
        ///   cancellation.
        /// </returns>
        /// <remarks>
        /// 
        /// <para>
        ///   Override this method in your implementation if you need to add items to the 
        ///   <paramref name="context"/> that are required by your <see cref="PublishAsyncCore"/> 
        ///   method. Call the base <see cref="RunAsyncCore"/> at the <em>end</em> of your 
        ///   implementation.
        /// </para>
        /// 
        /// <para>
        ///   All <see cref="IDisposable"/> and <see cref="IAsyncDisposable"/> items added to the 
        ///   <paramref name="context"/> will be disposed when <see cref="RunAsync"/> exits.
        /// </para>
        /// 
        /// </remarks>
        protected virtual async Task RunAsyncCore(RuuviTagPublisherContext context, CancellationToken cancellationToken) {
            var useBackgroundPublish = _publishInterval > 0;

            // Samples pending publish, indexed by MAC address.
            var pendingPublish = useBackgroundPublish
                ? new Dictionary<string, RuuviTagSample>(StringComparer.OrdinalIgnoreCase)
                : null;

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

                await PublishAsync(context, samples, cancellationToken).ConfigureAwait(false);
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
                        await PublishAsync(context, new[] { item! }, cancellationToken).ConfigureAwait(false);
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
        /// <param name="context">
        ///   The context for the publish operation.
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
        private async Task PublishAsync(RuuviTagPublisherContext context, IEnumerable<RuuviTagSample> samples, CancellationToken cancellationToken) {
            try {
                await PublishAsyncCore(context, samples, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException e) {
                if (cancellationToken.IsCancellationRequested) {
                    // Cancellation requested; rethrow and let the caller handle it.
                    throw;
                }
                Logger.LogError(e, Resources.Error_PublishError);
            }
            catch (Exception e) {
                Logger.LogError(e, Resources.Error_PublishError);
            }
        }


        /// <summary>
        /// Publishes the specified samples.
        /// </summary>
        /// <param name="context">
        ///   The context for the publish operation.
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
        protected abstract Task PublishAsyncCore(RuuviTagPublisherContext context, IEnumerable<RuuviTagSample> samples, CancellationToken cancellationToken);

    }
}
