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
    /// When <see langword="false"/>, Data Format 6 advertisements will be ignored.
    /// </summary>
    /// <remarks>
    ///
    /// <para>
    ///   Ruuvi devices that use the Extended v1 data format (such as Ruuvi Air) also emit
    ///   advertisements using Data Format 6 as a fallback for Bluetooth receivers that do not
    ///   support Bluetooth 5.0 extended advertisements. If the receiver supports receiving
    ///   extended advertisements, Data Format 6 advertisements should be ignored.
    /// </para>
    ///
    /// <para>
    ///   The default value of <see cref="EnableDataFormat6"/> is <see langword="false"/>. Set the
    ///   property to <see langword="true"/> on systems that do not support receiving extended
    ///   advertisements.
    /// </para>
    /// 
    /// </remarks>
    public bool EnableDataFormat6 { get; set; }
    
    /// <summary>
    /// Specifies if duplicate advertisements are allowed.
    /// </summary>
    public bool AllowDuplicateAdvertisements { get; set; }

}
