using System.Collections.Generic;
using System.Linq;

namespace NRuuviTag.Cli {

    /// <summary>
    /// Lookup from device ID to device information from the application's 
    /// <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.
    /// </summary>
    public class DeviceCollection : Dictionary<string, DeviceCollectionEntry> { 
    
        /// <summary>
        /// Converts the <see cref="DeviceCollection"/> into an equivalent collection of 
        /// <see cref="Device"/> objects.
        /// </summary>
        /// <returns>
        ///   An <see cref="IEnumerable{Device}"/> describing the devices.
        /// </returns>
        public IEnumerable<Device> GetDevices() {
            return this.Select(x => new Device() {
                DeviceId = x.Key,
                DisplayName = x.Value.DisplayName,
                MacAddress = x.Value.MacAddress
            }).ToArray();
        }
    
    }

}
