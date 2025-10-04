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
    /// Sample time.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>
    /// Signal strength (dBm).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? SignalStrength { get; init; }

        
    /// <summary>
    /// Creates a new <see cref="RuuviTagSample"/> instance.
    /// </summary>
    public RuuviTagSample() {}
        

    /// <summary>
    /// Creates a new <see cref="RuuviTagSample"/> instance.
    /// </summary>
    /// <param name="timestamp">
    ///   Sample time.
    /// </param>
    /// <param name="signalStrength">
    ///   Signal strength (dBm).
    /// </param>
    /// <param name="payload">
    ///   The <see cref="RuuviDataPayload"/> received from the Ruuvi device.
    /// </param>
    public RuuviTagSample(DateTimeOffset? timestamp, double? signalStrength, RuuviDataPayload payload) : base(payload) {
        Timestamp = timestamp;
        SignalStrength = signalStrength;
    }

}