using System.ComponentModel.DataAnnotations;

namespace NRuuviTag.AzureEventHubs;

public class AzureEventHubPublisherOptions : RuuviTagPublisherOptions {

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
    /// The maximum number of samples to add to an event hub data batch before publishing the 
    /// batch to the event hub.
    /// </summary>
    public int MaximumBatchSize { get; set; } = 50;

    /// <summary>
    /// The maximum age of an event hub data batch (in seconds) before the batch will be 
    /// published to the event hub regardless of size.
    /// </summary>
    public int MaximumBatchAge { get; set; } = 60;

}
