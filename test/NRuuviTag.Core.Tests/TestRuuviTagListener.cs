using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace NRuuviTag;

/// <summary>
/// <see cref="RuuviTagListener"/> implementation that allows ad hoc samples to be emitted to 
/// subscribers on demand.
/// </summary>
public sealed class TestRuuviTagListener : RuuviTagListener {

    /// <summary>
    /// Active subscription channels.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, Channel<RuuviTagSample>> _subscriptions = [];
    

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<RuuviTagSample> ListenAsync(
        Func<string, bool>? filter, 
        [EnumeratorCancellation]
        CancellationToken cancellationToken
    ) {
        var channelId = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<RuuviTagSample>(new UnboundedChannelOptions() { 
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = true
        });
        _subscriptions[channelId] = channel;
        
        try {
            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) { 
                if (item?.MacAddress == null) {
                    continue;
                }

                if (filter != null && !filter.Invoke(item.MacAddress)) {
                    continue;
                }

                yield return item;
            }
        }
        finally {
            _subscriptions.TryRemove(channelId, out _);
            channel.Writer.TryComplete();
        }
    }


    /// <summary>
    /// Publishes samples to all active subscription channels.
    /// </summary>
    /// <param name="samples">
    ///   The samples.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="samples"/> is <see langword="null"/>.
    /// </exception>
    public void Publish(params IReadOnlyList<RuuviTagSample> samples) {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0) {
            return;
        }

        foreach (var channel in _subscriptions.Values) {
            foreach (var sample in samples) {
                channel.Writer.TryWrite(sample);
            }
        }
    }
    
    
    /// <summary>
    /// Publishes samples to all active subscription channels.
    /// </summary>
    /// <param name="samples">
    ///   The samples.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="samples"/> is <see langword="null"/>.
    /// </exception>
    public async ValueTask PublishAsync(params IReadOnlyList<RuuviTagSample> samples) {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0) {
            return;
        }

        foreach (var channel in _subscriptions.Values) {
            foreach (var sample in samples) {
                await channel.Writer.WriteAsync(sample);
            }
        }
    }

}
