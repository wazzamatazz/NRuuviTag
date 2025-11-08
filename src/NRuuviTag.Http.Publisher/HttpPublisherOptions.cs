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
    /// A callback that is used to retrieve the device information to use for a given 
    /// MAC address.
    /// </summary>
    /// <remarks>
    ///   If <see cref="GetDeviceInfo"/> is <see langword="null"/>, a default <see cref="Device"/> 
    ///   will be generated for the sample.
    /// </remarks>
    public Func<string, Device?>? GetDeviceInfo { get; set; }

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
    /// The maximum number of samples to add to a data batch before publishing the 
    /// batch to the HTTP endpoint.
    /// </summary>
    [Range(1, 10_000)]
    public int MaximumBatchSize { get; set; } = 50;

}
