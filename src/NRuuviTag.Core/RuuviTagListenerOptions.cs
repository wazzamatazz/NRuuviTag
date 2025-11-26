namespace NRuuviTag;

/// <summary>
/// Options for <see cref="RuuviTagListener"/>.
/// </summary>
public class RuuviTagListenerOptions {

    /// <summary>
    /// When <see langword="true"/>, only samples from known devices will be published.
    /// </summary>
    /// <remarks>
    ///   <see cref="RuuviTagListener"/> uses the <see cref="IDeviceResolver"/> service to determine
    ///   whether a device is known.
    /// </remarks>
    public bool KnownDevicesOnly { get; set; }
    
    /// <summary>
    /// When <see langword="true"/>, formats that use extended advertisements will be enabled, and
    /// fallback formats will be ignored.
    /// </summary>
    /// <remarks>
    ///
    /// <para>
    ///   Enable this option if your Bluetooth receiver supports Bluetooth 5.0 extended
    ///   advertisements to ensure that the most complete data format is used.
    /// </para>
    ///
    /// <para>
    ///   Data format E1 (used by Ruuvi Air) is an example of an extended advertisement data
    ///   format. When <see cref="EnableExtendedAdvertisementFormats"/> is <see langword="true"/>,
    ///   Data Format E1 advertisements will be processed, and the fallback Data Format 6
    ///   advertisements will be ignored.
    /// </para>
    /// 
    /// </remarks>
    public bool EnableExtendedAdvertisementFormats { get; set; }
    
    /// <summary>
    /// Specifies if duplicate advertisements are allowed.
    /// </summary>
    public bool AllowDuplicateAdvertisements { get; set; }

}
