using System;
using System.Collections.Generic;
using System.Text;

namespace NRuuviTag.Mqtt {

    /// <summary>
    /// Describes a device that is being observed by an <see cref="MqttAgent"/>.
    /// </summary>
    public class DeviceInfo {

        public string DeviceId { get; set; } = default!;

        public string MacAddress { get; set; } = default!;

        public string? DisplayName { get; set; }

    }
}
