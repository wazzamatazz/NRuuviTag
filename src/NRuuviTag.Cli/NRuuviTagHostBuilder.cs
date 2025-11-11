using System;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NRuuviTag.Cli;

/// <summary>
/// Extensions for creating <see cref="IHostBuilder"/> instances for running the NRuuviTag command app.
/// </summary>
public static class NRuuviTagHostBuilder {

    /// <summary>
    /// Creates a new <see cref="IHostBuilder"/> instance for running the NRuuviTag command app.
    /// </summary>
    /// <typeparam name="TListenerFactory">
    ///   The factory for creating the <see cref="IRuuviTagListener"/> for the app.
    /// </typeparam>
    /// <param name="args">
    ///   The command-line arguments.
    /// </param>
    /// <returns>
    ///   A new <see cref="IHostBuilder"/> instance.
    /// </returns>
    public static IHostBuilder CreateHostBuilder<TListenerFactory>(string[]? args) where TListenerFactory : class, IRuuviTagListenerFactory {
        return CreateHostBuilderCore(args, (hostContext, services) => {
            services.AddRuuviTagCommandApp<IRuuviTagListenerFactory>(hostContext.Configuration);
        });
    }


    /// <summary>
    /// Creates a new <see cref="IHostBuilder"/> instance for running the NRuuviTag command app.
    /// </summary>
    /// <typeparam name="TListenerFactory">
    ///   The factory for creating the <see cref="IRuuviTagListener"/> for the app.
    /// </typeparam>
    /// <param name="args">
    ///   The command-line arguments.
    /// </param>
    /// <param name="factory">
    ///   The listener instance to use.
    /// </param>
    /// <returns>
    ///   A new <see cref="IHostBuilder"/> instance.
    /// </returns>
    public static IHostBuilder CreateHostBuilder<TListenerFactory>(string[]? args, TListenerFactory factory) where TListenerFactory : class, IRuuviTagListenerFactory {
        ArgumentNullException.ThrowIfNull(factory);
        return CreateHostBuilder(args, _ => factory);
    }



    /// <summary>
    /// Creates a new <see cref="IHostBuilder"/> instance for running the NRuuviTag command app.
    /// </summary>
    /// <typeparam name="TListenerFactory">
    ///   The factory for creating the <see cref="IRuuviTagListener"/> for the app.
    /// </typeparam>
    /// <param name="args">
    ///   The command-line arguments.
    /// </param>
    /// <param name="factory">
    ///   The listener factory to use.
    /// </param>
    /// <returns>
    ///   A new <see cref="IHostBuilder"/> instance.
    /// </returns>
    public static IHostBuilder CreateHostBuilder<TListenerFactory>(string[]? args, Func<IServiceProvider, TListenerFactory> factory) where TListenerFactory : class, IRuuviTagListenerFactory {
        ArgumentNullException.ThrowIfNull(factory);
        return CreateHostBuilderCore(args, (hostContext, services) => {
            services.AddRuuviTagCommandApp(hostContext.Configuration, factory.Invoke);
        });
    }


    /// <summary>
    /// Configures core services for a host builder.
    /// </summary>
    private static IHostBuilder CreateHostBuilderCore(string[]? args, Action<HostBuilderContext, IServiceCollection> configureServices) {
        return Host.CreateDefaultBuilder(args)
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureAppConfiguration(config => {
                config.AddRuuviTagDeviceConfiguration();
            })
            .ConfigureServices(configureServices);
    }

}
