using System;
using System.Collections.Generic;
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
    /// Buffers published samples until the listener starts running.
    /// </summary>
    /// <remarks>
    ///   The channel is created eagerly so that samples published before <see cref="RunAsync"/>
    ///   has started are buffered instead of being dropped. <see cref="RunAsync"/> is started in
    ///   a background task when a subscriber starts listening, so waiting for the listener to
    ///   start before publishing samples is inherently racy.
    /// </remarks>
    private readonly Channel<RuuviTagSample> _channel = Channel.CreateUnbounded<RuuviTagSample>(new UnboundedChannelOptions() {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });


    /// <inheritdoc />
    public TestRuuviTagListener(RuuviTagListenerOptions options, IDeviceResolver deviceResolver)
        : base(options, deviceResolver) { }


    /// <inheritdoc/>
    protected override async Task RunAsync(CancellationToken cancellationToken) {
        await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) {
            if (item?.MacAddress == null) {
                continue;
            }

            var device = DeviceResolver.GetDeviceInformation(item.MacAddress);
            if (device is null && KnownDevicesOnly) {
                continue;
            }

            EmitSample(item);
        }
    }


    /// <summary>
    /// Publishes samples to the listener.
    /// </summary>
    /// <param name="samples">
    ///   The samples.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="samples"/> is <see langword="null"/>.
    /// </exception>
    public void Publish(params IReadOnlyList<RuuviTagSample> samples) {
        ArgumentNullException.ThrowIfNull(samples);

        foreach (var sample in samples) {
            _channel.Writer.TryWrite(sample);
        }
    }


    /// <summary>
    /// Publishes samples to the listener.
    /// </summary>
    /// <param name="samples">
    ///   The samples.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="samples"/> is <see langword="null"/>.
    /// </exception>
    public async ValueTask PublishAsync(params IReadOnlyList<RuuviTagSample> samples) {
        ArgumentNullException.ThrowIfNull(samples);

        foreach (var sample in samples) {
            await _channel.Writer.WriteAsync(sample);
        }
    }

}
