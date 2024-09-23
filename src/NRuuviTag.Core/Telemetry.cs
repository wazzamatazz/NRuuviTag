using System.Diagnostics.Metrics;

namespace NRuuviTag {

    /// <summary>
    /// Telemetry for the NRuuviTag.Core library.
    /// </summary>
    public static class Telemetry {

        /// <summary>
        /// The name of the meter for the NRuuviTag.Core library.
        /// </summary>
        public const string MeterName = "nruuvitag";


        /// <summary>
        /// The meter for the NRuuviTag.Core library.
        /// </summary>
        public static Meter Meter { get; } = new Meter(MeterName, typeof(Telemetry).Assembly.GetName().Version.ToString(3));

    }

}
