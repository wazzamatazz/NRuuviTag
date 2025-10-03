namespace NRuuviTag;

/// <summary>
/// Constants used by RuuviTag listeners.
/// </summary>
public static class Constants {

    /// <summary>
    /// Manufacturer ID for Ruuvi, as per https://docs.ruuvi.com/communication/bluetooth-advertisements.
    /// </summary>
    public const ushort ManufacturerId = 0x0499;

    /// <summary>
    /// RAWv2 payload format. See https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-5-rawv2
    /// for details.
    /// </summary>
    public const byte DataFormatRawV2 = 0x05;

    /// <summary>
    /// Extended v1 payload format. https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-e1
    /// for details.
    /// </summary>
    public const byte DataFormatExtendedV1 = 0xE1;

}
