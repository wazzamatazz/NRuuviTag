namespace NRuuviTag {

    /// <summary>
    /// Constants used by RuuviTag listeners.
    /// </summary>
    public static class Constants {

        /// <summary>
        /// Manufacturer ID for Ruuvi, as per https://docs.ruuvi.com/communication/bluetooth-advertisements.
        /// </summary>
        public const ushort ManufacturerId = 0x0499;

        /// <summary>
        /// RAWv1 payload format. See https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-3-rawv1
        /// for details.
        /// </summary>
        public const byte DataFormatRawV1 = 3;

        /// <summary>
        /// Eddystone URL payload format. See https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-4-url
        /// for details.
        /// </summary>
        public const byte DataFormatUrl = 4;

        /// <summary>
        /// RAWv2 payload format. See https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-5-rawv2
        /// for details.
        /// </summary>
        public const byte DataFormatRawV2 = 5;

        /// <summary>
        /// Encrypted environmental payload format. See https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-8-encrypted-environmental
        /// for details.
        /// </summary>
        public const byte DataFormatEncryptedEnvironmental = 8;

    }

}
