using System.Threading;
using System.Threading.Tasks;

namespace NRuuviTag;

/// <summary>
/// <see cref="IDeviceLookup"/> allows looking up Ruuvi device information based on MAC address.
/// </summary>
public interface IDeviceLookup {

    /// <summary>
    /// Gets the device information for the specified MAC address.
    /// </summary>
    /// <param name="macAddress">
    ///   The MAC address of the device.
    /// </param>
    /// <returns>
    ///   The device information, or <see langword="null"/> if the device is unknown.
    /// </returns>
    Device? GetDeviceInformation(string macAddress);

}
