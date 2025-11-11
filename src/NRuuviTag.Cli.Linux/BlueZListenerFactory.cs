using System;

using Microsoft.Extensions.DependencyInjection;

using NRuuviTag.Listener.Linux;

namespace NRuuviTag.Cli.Linux;

/// <summary>
/// <see cref="IRuuviTagListenerFactory"/> implementation for creating <see cref="BlueZListener"/> instances.
/// </summary>
internal class BlueZListenerFactory : IRuuviTagListenerFactory {

    private readonly IServiceProvider _serviceProvider;


    /// <summary>
    /// Creates a new <see cref="BlueZListenerFactory"/> instance.
    /// </summary>
    /// <param name="serviceProvider">
    ///   The service provider to use for creating listener instances.
    /// </param>
    public BlueZListenerFactory(IServiceProvider serviceProvider) {
        _serviceProvider = serviceProvider;
    }


    /// <inheritdoc />
    public IRuuviTagListener CreateListener(Action<RuuviTagListenerOptions> configureOptions) {
        var options = new BlueZListenerOptions();
        configureOptions.Invoke(options);
        return ActivatorUtilities.CreateInstance<BlueZListener>(_serviceProvider, options);
    }

}
