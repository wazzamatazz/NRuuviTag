using System;
using System.Text.Json.Serialization;

namespace NRuuviTag;

/// <summary>
/// Describes a sample received from a Ruuvi sensor.
/// </summary>
/// <remarks>
///   See https://docs.ruuvi.com/communication/bluetooth-advertisements for details about 
///   Bluetooth LE advertisements.
/// </remarks>
public record RuuviTagSample : RuuviDataPayload {

    /// <summary>
    /// The identifier for the device.
    /// </summary>
    /// <remarks>
    ///   To allow the <see cref="DeviceId"/> to be used in e.g. MQTT topic names, it is 
    ///   recommended that device identifiers consist only of alphanumeric characters, 
    ///   hyphens, and underscores.
    /// </remarks>
    [JsonPropertyName("deviceId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DeviceId { get; init; }
    
    /// <summary>
    /// Sample time.
    /// </summary>
    [JsonPropertyName("timestamp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>
    /// Signal strength (dBm).
    /// </summary>
    [JsonPropertyName("signalStrength")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? SignalStrength { get; init; }
    
        
    /// <summary>
    /// Creates a new <see cref="RuuviTagSample"/> instance.
    /// </summary>
    public RuuviTagSample() { }
        

    /// <summary>
    /// Creates a new <see cref="RuuviTagSample"/> instance.
    /// </summary>
    /// <param name="deviceId">
    ///   The identifier for the device.
    /// </param>
    /// <param name="timestamp">
    ///   Sample time.
    /// </param>
    /// <param name="signalStrength">
    ///   Signal strength (dBm).
    /// </param>
    /// <param name="payload">
    ///   The <see cref="RuuviDataPayload"/> received from the Ruuvi device.
    /// </param>
    public RuuviTagSample(string? deviceId, DateTimeOffset? timestamp, double? signalStrength, RuuviDataPayload payload) : base(payload) {
        DeviceId = deviceId;
        Timestamp = timestamp;
        SignalStrength = signalStrength;
    }

}
