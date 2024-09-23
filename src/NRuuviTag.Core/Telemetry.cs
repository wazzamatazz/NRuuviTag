using System.Diagnostics.Metrics;

namespace NRuuviTag {

    /// <summary>
    /// Telemetry for the NRuuviTag.Core library.
    /// </summary>
    internal static class Telemetry {

        /// <summary>
        /// The meter for the NRuuviTag.Core library.
        /// </summary>
        public static Meter Meter { get; } = new Meter("nruuvitag", typeof(Telemetry).Assembly.GetName().Version.ToString(3));

    }

}
