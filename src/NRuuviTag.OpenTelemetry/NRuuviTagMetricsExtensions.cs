
namespace OpenTelemetry.Metrics;

/// <summary>
/// Extensions for observing NRuuviTag metrics.
/// </summary>
public static class NRuuviTagMetricsExtensions {

    /// <summary>
    /// Adds NRuuviTag metrics instrumentation to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder">
    ///   The <see cref="MeterProviderBuilder"/>.
    /// </param>
    /// <returns>
    ///   The <see cref="MeterProviderBuilder"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    public static MeterProviderBuilder AddNRuuviTagInstrumentation(this MeterProviderBuilder builder) {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMeter(NRuuviTag.Telemetry.MeterName);
    }

}