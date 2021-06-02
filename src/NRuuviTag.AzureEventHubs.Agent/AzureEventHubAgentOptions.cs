using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace NRuuviTag.AzureEventHubs {
    public class AzureEventHubAgentOptions {

        /// <summary>
        /// The Event Hub connection string.
        /// </summary>
        [Required]
        public string ConnectionString { get; set; } = default!;

        /// <summary>
        /// The Event Hub name.
        /// </summary>
        [Required]
        public string EventHubName { get; set; } = default!;

        /// <summary>
        /// When <see langword="true"/>, only samples from known devices will be published. See 
        /// remarks for details.
        /// </summary>
        /// <remarks>
        ///   When <see cref="KnownDevicesOnly"/> is enabled, a sample will be discarded if 
        ///   <see cref="GetDeviceInfo"/> is <see langword="null"/>, or if it returns <see langword="null"/> 
        ///   for a given sample.
        /// </remarks>
        public bool KnownDevicesOnly { get; set; }

        /// <summary>
        /// The fastest rate (in seconds) that values will be sampled at for each observed device. 
        /// Less than zero means that all observed values are immediately passed to the <see cref="AzureEventHubAgent"/> 
        /// for processing.
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// The maximum number of samples to add to an event hub data batch before publishing the 
        /// batch to the event hub.
        /// </summary>
        public int MaximumBatchSize { get; set; } = 50;

        /// <summary>
        /// The maximum age of an event hub data batch (in seconds) before the batch will be 
        /// published to the event hub regardless of size.
        /// </summary>
        public int MaximumBatchAge { get; set; } = 60;

        /// <summary>
        /// A callback that is used to retrieve the device information to use for a given 
        /// MAC address.
        /// </summary>
        public Func<string, Device?>? GetDeviceInfo { get; set; }

        /// <summary>
        /// A callback that is used to prepare a sample prior to publishing it to the event hub.
        /// </summary>
        /// <remarks>
        /// 
        /// <para>
        ///   Use the <see cref="PrepareForPublish"/> callback to modify a <see cref="RuuviTagSample"/> 
        ///   instance prior to it being published to the event hub (e.g. to perform unit conversion). 
        ///   Set any property on a sample to <see langword="null"/> to exclude that property from the 
        ///   publish.
        /// </para>
        /// 
        /// </remarks>
        public Action<RuuviTagSampleExtended>? PrepareForPublish { get; set; }

    }
}
