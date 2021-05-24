using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace NRuuviTag {

    /// <summary>
    /// <see cref="RuuviTagListener"/> implementation that allows ad hoc samples to be emitted to 
    /// subscribers on demand.
    /// </summary>
    public sealed class TestRuuviTagListener : RuuviTagListener {

        /// <summary>
        /// Active subscription channels.
        /// </summary>
        private readonly HashSet<Channel<RuuviTagSample>> _subscriptions = new HashSet<Channel<RuuviTagSample>>();


        /// <inheritdoc/>
        public sealed override async IAsyncEnumerable<RuuviTagSample> ListenAsync(
            Func<string, bool>? filter, 
            [EnumeratorCancellation]
            CancellationToken cancellationToken
        ) {
            var channel = Channel.CreateUnbounded<RuuviTagSample>(new UnboundedChannelOptions() { 
                SingleReader = true,
                SingleWriter = false
            });

            lock (_subscriptions) {
                _subscriptions.Add(channel);
            }

            try {
                await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) { 
                    if (item.MacAddress == null) {
                        continue;
                    }

                    if (filter != null && !filter.Invoke(item.MacAddress)) {
                        continue;
                    }

                    yield return item;
                }
            }
            finally {
                lock (_subscriptions) {
                    _subscriptions.Remove(channel);
                }
                channel.Writer.TryComplete();
            }
        }


        /// <summary>
        /// Publishes a sample to all active subscription channels.
        /// </summary>
        /// <param name="sample">
        ///   The sample.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="sample"/> is <see langword="null"/>.
        /// </exception>
        public void Publish(RuuviTagSample sample) {
            if (sample == null) {
                throw new ArgumentNullException(nameof(sample));
            }

            lock (_subscriptions) {
                foreach (var channel in _subscriptions) {
                    channel.Writer.TryWrite(sample);
                }
            }
        }

    }
}
