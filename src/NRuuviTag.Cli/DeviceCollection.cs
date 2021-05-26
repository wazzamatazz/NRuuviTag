using System.Collections.Generic;

namespace NRuuviTag.Cli {

    /// <summary>
    /// Lookup from device ID to device information from the application's 
    /// <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.
    /// </summary>
    public class DeviceCollection : Dictionary<string, Device> { }

}
