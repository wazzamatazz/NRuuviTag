using System;

namespace NRuuviTag;

/// <summary>
/// Options for <see cref="RuuviTagPublisher"/>.
/// </summary>
public class RuuviTagPublisherOptions {

    /// <summary>
    /// The interval at which batches of samples are published.
    /// </summary>
    /// <remarks>
    ///   Specify less than or equal to <see cref="TimeSpan.Zero"/> to disable batching
    ///   and publish samples as they are received from the source devices.
    /// </remarks>
    public TimeSpan PublishInterval { get; set; }
    
    /// <summary>
    /// The per-device behaviour to use when publishing a batch of samples.
    /// </summary>
    /// <remarks>
    ///   Use <see cref="PerDevicePublishBehaviour"/> to control if batches should include all
    ///   samples for a device received since the previous publish operation, or only the latest
    ///   sample for each device.
    /// </remarks>
    public BatchPublishDeviceBehaviour PerDevicePublishBehaviour { get; set; } = BatchPublishDeviceBehaviour.AllSamples;
    
    /// <summary>
    /// Transforms a sample before it is published.
    /// </summary>
    /// <remarks>
    ///
    /// <para>
    ///   The <see cref="PrepareForPublish"/> callback can be used to transform samples prior to
    ///   publishing, for example to perform unit conversions or to remove measurements that are
    ///   not needed.
    /// </para>
    ///
    /// <para>
    ///   If the callback returns <see langword="null"/>, the sample will not be published.
    /// </para>
    /// 
    /// </remarks>
    public Func<RuuviTagSample, RuuviTagSample?>? PrepareForPublish { get; set; }

}


/// <summary>
/// Describes the per-device behaviour to use when publishing a batch of samples.
/// </summary>
public enum BatchPublishDeviceBehaviour {

    /// <summary>
    /// All samples received from a device since the previous publish operation are included in the batch.
    /// </summary>
    AllSamples,
    
    /// <summary>
    /// Only the latest sample received from a device since the previous publish operation is included in the batch.
    /// </summary>
    LatestSampleOnly,

}
