namespace NRuuviTag.Cli;

/// <summary>
/// An entry in a <see cref="DeviceCollection"/>.
/// </summary>
public class DeviceCollectionEntry {

    /// <summary>
    /// The MAC address for the device.
    /// </summary>
    public string MacAddress { get; set; } = default!;

    /// <summary>
    /// The display name for the device.
    /// </summary>
    public string? DisplayName { get; set; }

}