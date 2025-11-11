using System;

namespace NRuuviTag.Cli;

/// <summary>
/// <see cref="IRuuviTagListenerFactory"/> allows the NRuuviTag command app to create
/// <see cref="IRuuviTagListener"/> instances in a platform-agnostic way.
/// </summary>
public interface IRuuviTagListenerFactory {

    /// <summary>
    /// Creates a new <see cref="IRuuviTagListener"/> instance with the given options.
    /// </summary>
    /// <param name="configureOptions">
    ///   An action to configure the <see cref="RuuviTagListenerOptions"/> for the listener.
    /// </param>
    /// <returns>
    ///   A new <see cref="IRuuviTagListener"/> instance.
    /// </returns>
    IRuuviTagListener CreateListener(Action<RuuviTagListenerOptions> configureOptions);

}
