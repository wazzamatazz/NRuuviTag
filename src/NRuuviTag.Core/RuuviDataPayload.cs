using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace NRuuviTag;

/// <summary>
/// Describes a data payload received from a Ruuvi sensor such as a RuuviTag or Ruuvi Air.
/// </summary>
/// <remarks>
///
/// <para>
///   <see cref="RuuviDataPayload"/> is a composite type describing the payload for multiple Ruuvi
///   devices, including RuuviTag and Ruuvi Air. The <see cref="DataFormat"/> property determines
///   which of the remaining properties a consumer can expect to be populated.
/// </para>
/// 
/// <para>
///   See https://docs.ruuvi.com/communication/bluetooth-advertisements for details about 
///   Bluetooth LE advertisements and data formats.
/// </para>
/// 
/// </remarks>
[SuppressMessage("ReSharper", "InconsistentNaming")]
public record RuuviDataPayload {

    /// <summary>
    /// Payload data format (see https://docs.ruuvi.com/communication/bluetooth-advertisements).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? DataFormat { get; init; }
    
    /// <summary>
    /// Sensor calibration status.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Calibrated { get; init; }

    /// <summary>
    /// Temperature (deg C).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Temperature { get; init; }

    /// <summary>
    /// Humidity (%).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Humidity { get; init; }

    /// <summary>
    /// Pressure (hPa).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Pressure { get; init; }

    /// <summary>
    /// X-acceleration (g).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? AccelerationX { get; init; }

    /// <summary>
    /// Y-acceleration (g).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? AccelerationY { get; init; }

    /// <summary>
    /// Z-acceleration (g).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? AccelerationZ { get; init; }

    /// <summary>
    /// Battery voltage (V).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? BatteryVoltage { get; init; }
    
    /// <summary>
    /// TX power (dBm).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TxPower { get; init; }
    
    /// <summary>
    /// PM 1.0 (µg/m3).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? PM10 { get; init; }
    
    /// <summary>
    /// PM 2.5 (µg/m3).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? PM25 { get; init; }
    
    /// <summary>
    /// PM 4.0 (µg/m3).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? PM40 { get; init; }
    
    /// <summary>
    /// PM 10.0 (µg/m3).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? PM100 { get; init; }
    
    /// <summary>
    /// CO2 (ppm).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? CO2 { get; init; }

    /// <summary>
    /// VOC index (unitless).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? VOC { get; init; }
    
    /// <summary>
    /// NOX index (unitless).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? NOX { get; init; }
    
    /// <summary>
    /// Luminosity (lux).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Luminosity { get; init; }
    
    /// <summary>
    /// Movement counter (counts).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? MovementCounter { get; init; }
        
    /// <summary>
    /// Measurement sequence.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? MeasurementSequence { get; init; }

    /// <summary>
    /// MAC address of device.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MacAddress { get; init; }
    
}
