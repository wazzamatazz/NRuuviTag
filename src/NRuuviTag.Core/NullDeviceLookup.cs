namespace NRuuviTag;

/// <summary>
/// <see cref="IDeviceLookup"/> implementation that always returns <see langword="null"/>.
/// </summary>
public sealed class NullDeviceLookup : IDeviceLookup {

    /// <inheritdoc />
    public Device? GetDeviceInformation(string macAddress) => null;

}
