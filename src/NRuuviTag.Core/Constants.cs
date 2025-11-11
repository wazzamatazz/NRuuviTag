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
    /// Data format 6 payload format (used by Ruuvi Air for compatibility with Bluetooth 4.0). See
    /// https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-6 for details.
    /// </summary>
    public const byte DataFormat6 = 0x06;

    /// <summary>
    /// Extended v1 payload format. https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-e1
    /// for details.
    /// </summary>
    public const byte DataFormatExtendedV1 = 0xE1;

}
