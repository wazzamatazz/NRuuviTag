using System.ComponentModel.DataAnnotations;

#nullable disable warnings

namespace NRuuviTag.Http;

/// <summary>
/// Options for <see cref="HttpPublisher"/>.
/// </summary>
public class HttpPublisherOptions : RuuviTagPublisherOptions {

    /// <summary>
    /// The HTTP endpoint to which samples will be published.
    /// </summary>
    [Required]
    public Uri Endpoint { get; set; }
    
    /// <summary>
    /// The HTTP method to use when publishing samples.
    /// </summary>
    [AllowedValues("POST", "PUT")]
    public string HttpMethod { get; set; } = "POST";
    
    /// <summary>
    /// The headers to include in HTTP requests.
    /// </summary>
    public IDictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// The maximum number of samples to add to a data batch before publishing the 
    /// batch to the HTTP endpoint.
    /// </summary>
    [Range(1, 10_000)]
    public int MaximumBatchSize { get; set; } = 50;

}
