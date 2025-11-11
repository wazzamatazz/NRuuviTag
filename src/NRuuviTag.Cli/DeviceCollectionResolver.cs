using Microsoft.Extensions.Options;

namespace NRuuviTag.Cli;

/// <summary>
/// <see cref="IDeviceResolver"/> implementation that looks up devices from the application's
/// configured <see cref="DeviceCollection"/> options.
/// </summary>
internal class DeviceCollectionResolver : IDeviceResolver {

    private readonly IOptionsMonitor<DeviceCollection> _devices;


    public DeviceCollectionResolver(IOptionsMonitor<DeviceCollection> devices) {
        _devices = devices;
    }
    
    
    /// <inheritdoc />
    public Device? GetDeviceInformation(string macAddress) => _devices.CurrentValue.GetDevice(macAddress);

}
