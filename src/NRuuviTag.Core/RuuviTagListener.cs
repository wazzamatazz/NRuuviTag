using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Nito.AsyncEx;

namespace NRuuviTag;

/// <summary>
/// Base <see cref="IRuuviTagListener"/> implementation.
/// </summary>
public abstract class RuuviTagListener : IRuuviTagListener {

    /// <summary>
    /// The type of the listener.
    /// </summary>
    /// <remarks>
    ///   Used in metric tags.
    /// </remarks>
    private readonly string _listenerType;
    
    /// <summary>
    /// Indicates whether the listener is actively listening.
    /// </summary>
    private readonly AsyncManualResetEvent _listeningSignal = new AsyncManualResetEvent();

    /// <summary>
    /// The counter for the number of observed samples.
    /// </summary>
    private static readonly Counter<long> s_observedSamplesCounter = Telemetry.Meter.CreateCounter<long>(
        "listener.observed_samples",
        unit: "{samples}",
        description: "The number of observed samples from RuuviTag devices.");


    /// <summary>
    /// Creates a new <see cref="RuuviTagListener"/> instance.
    /// </summary>
    protected RuuviTagListener() {
        _listenerType = GetType().FullName!;
    }


    /// <summary>
    /// Waits until the listener has started listening for advertisements.
    /// </summary>
    /// <param name="cancellationToken">
    ///   The cancellation token for the operation.
    /// </param>
    public async ValueTask WaitForListenStartedAsync(CancellationToken cancellationToken = default) {
        await _listeningSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
    }


    /// <inheritdoc/>
    IAsyncEnumerable<RuuviTagSample> IRuuviTagListener.ListenAsync(CancellationToken cancellationToken) {
        return ((IRuuviTagListener) this).ListenAsync(null, cancellationToken);
    }


    /// <inheritdoc/>
    async IAsyncEnumerable<RuuviTagSample> IRuuviTagListener.ListenAsync(Func<string, bool>? filter, [EnumeratorCancellation] CancellationToken cancellationToken) {
        _listeningSignal.Set();

        try {
            await foreach (var item in ListenAsync(filter, cancellationToken).ConfigureAwait(false)) {
                var instanceTagList = new TagList() {
                    {
                        "listener.type", _listenerType
                    }, {
                        "hw.id", item.MacAddress
                    }, {
                        "hw.type", "ruuvitag"
                    }
                };

                if (item is RuuviTagSampleExtended extended && !string.IsNullOrWhiteSpace(extended.DeviceId)) {
                    instanceTagList.Add("hw.name", extended.DeviceId);
                }

                s_observedSamplesCounter.Add(1, instanceTagList);
                yield return item;
            }
        }
        finally {
            _listeningSignal.Reset();
        }
    }


    /// <summary>
    /// Listens for advertisements broadcast by Ruuvi devices until cancelled.
    /// </summary>
    /// <param name="filter">
    ///   An optional callback that can be used to limit the listener to specific Ruuvi MAC 
    ///   addresses. The parameter passed to the callback is the MAC address of the Ruuvi 
    ///   device that a broadcast was received from.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token that can be cancelled when the listener should stop.
    /// </param>
    /// <returns>
    ///   An <see cref="IAsyncEnumerable{T}"/> that will emit the received samples as they occur.
    /// </returns>
    protected abstract IAsyncEnumerable<RuuviTagSample> ListenAsync(Func<string, bool>? filter, CancellationToken cancellationToken);

}
