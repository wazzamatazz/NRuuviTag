using System;

using Microsoft.Extensions.Configuration;

using MQTTnet;

using NRuuviTag;
using NRuuviTag.Cli;
using NRuuviTag.Cli.Commands;
using NRuuviTag.Mqtt;

using Spectre.Console.Cli;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions for <see cref="IServiceCollection"/>.
/// </summary>
public static class NRuuviTagServiceCollectionExtensions {

    /// <summary>
    /// Registers services required for a <see cref="CommandApp"/> that will run a <see cref="NRuuviTag.RuuviTagPublisher"/>.
    /// </summary>
    /// <typeparam name="TListenerFactory">
    ///   The <see cref="IRuuviTagListenerFactory"/> for creating a listener.
    /// </typeparam>
    /// <param name="services">
    ///   The <see cref="IServiceCollection"/>.
    /// </param>
    /// <param name="configuration">
    ///   The <see cref="Configuration.IConfiguration"/> for the application.
    /// </param>
    /// <returns>
    ///   The <see cref="IServiceCollection"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="configuration"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddRuuviTagCommandApp<TListenerFactory>(this IServiceCollection services, IConfiguration configuration) where TListenerFactory : class, IRuuviTagListenerFactory {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCoreRuuviTagServices(configuration);
        services.AddScoped<IRuuviTagListenerFactory, TListenerFactory>();

        return services;
    }


    /// <summary>
    /// Registers services required for a <see cref="CommandApp"/> that will run an <see cref="MqttPublisher"/>.
    /// </summary>
    /// <typeparam name="TListenerFactory">
    ///   The <see cref="IRuuviTagListenerFactory"/> for creating a listener.
    /// </typeparam>
    /// <param name="services">
    ///   The <see cref="IServiceCollection"/>.
    /// </param>
    /// <param name="configuration">
    ///   The <see cref="Configuration.IConfiguration"/> for the application.
    /// </param>
    /// <param name="factory">
    ///   The factory for creating the <typeparamref name="TListenerFactory"/>.
    /// </param>
    /// <returns>
    ///   The <see cref="IServiceCollection"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="configuration"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="factory"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddRuuviTagCommandApp<TListenerFactory>(this IServiceCollection services, IConfiguration configuration, Func<IServiceProvider, TListenerFactory> factory) where TListenerFactory : class, IRuuviTagListenerFactory {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(factory);

        services.AddCoreRuuviTagServices(configuration);
        services.AddScoped<IRuuviTagListenerFactory, TListenerFactory>(factory);

        return services;
    }


    /// <summary>
    /// Registers services required for the <see cref="CommandApp"/>.
    /// </summary>
    /// <param name="services">
    ///   The <see cref="IServiceCollection"/>.
    /// </param>
    /// <param name="configuration">
    ///   The <see cref="Configuration.IConfiguration"/> for the application.
    /// </param>
    /// <returns>
    ///   The <see cref="IServiceCollection"/>.
    /// </returns>
    private static IServiceCollection AddCoreRuuviTagServices(this IServiceCollection services, IConfiguration configuration) {
        services.Configure<DeviceCollection>(configuration.GetSection("Devices"));
        services.AddScoped<IDeviceResolver, DeviceCollectionResolver>();

        services.AddTransient<MqttFactory>();
        services.AddHttpClient<NRuuviTag.Http.HttpPublisher>().AddStandardResilienceHandler();

        services.AddSpectreCommandApp(CommandUtilities.ConfigureCommandApp);

        return services;
    }

}
