using System;

using Microsoft.Extensions.DependencyInjection;

using NRuuviTag.Listener.Windows;

namespace NRuuviTag.Cli.Windows;

/// <summary>
/// <see cref="IRuuviTagListenerFactory"/> that creates <see cref="WindowsSdkListener"/> instances.
/// </summary>
internal class WindowsSdkListenerFactory : IRuuviTagListenerFactory {
    
    private readonly IServiceProvider _serviceProvider;
    
    
    /// <summary>
    /// Creates a new <see cref="WindowsSdkListenerFactory"/> instance.
    /// </summary>
    /// <param name="serviceProvider">
    ///   The service provider to use for creating listener instances.
    /// </param>
    public WindowsSdkListenerFactory(IServiceProvider serviceProvider) {
        _serviceProvider = serviceProvider;
    }
    
    
    /// <inheritdoc />
    public IRuuviTagListener CreateListener(Action<RuuviTagListenerOptions> configureOptions) {
        var options = new WindowsSdkListenerOptions();
        configureOptions.Invoke(options);
        return ActivatorUtilities.CreateInstance<WindowsSdkListener>(_serviceProvider, options);
    }

}
