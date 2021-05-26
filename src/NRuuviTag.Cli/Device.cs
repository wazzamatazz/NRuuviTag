namespace NRuuviTag.Cli {

    /// <summary>
    /// Device definition from the application's <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.
    /// </summary>
    public class Device {

        /// <summary>
        /// The MAC address for the device.
        /// </summary>
        public string MacAddress { get; set; } = default!;

        /// <summary>
        /// The display name for the device.
        /// </summary>
        public string? DisplayName { get; set; }

    }
}
