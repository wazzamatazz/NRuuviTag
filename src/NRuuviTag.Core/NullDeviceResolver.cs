namespace NRuuviTag;

/// <summary>
/// <see cref="IDeviceResolver"/> implementation that always returns <see langword="null"/>.
/// </summary>
public sealed class NullDeviceResolver : IDeviceResolver {

    /// <inheritdoc />
    public Device? GetDeviceInformation(string macAddress) => null;

}
