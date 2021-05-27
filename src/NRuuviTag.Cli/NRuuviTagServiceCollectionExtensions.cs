using System;

using Microsoft.Extensions.Configuration;

using MQTTnet;

using NRuuviTag;
using NRuuviTag.Cli;
using NRuuviTag.Cli.Commands;

using Spectre.Console.Cli;

namespace Microsoft.Extensions.DependencyInjection {

    /// <summary>
    /// Extensions for <see cref="IServiceCollection"/>.
    /// </summary>
    public static class NRuuviTagServiceCollectionExtensions {

        /// <summary>
        /// Registers services required for a <see cref="CommandApp"/> that will run an <see cref="NRuuviTag.Mqtt.MqttAgent"/>.
        /// </summary>
        /// <typeparam name="TListener">
        ///   The <see cref="IRuuviTagListener"/> that the agent will use to listen for RuuviTag 
        ///   advertisements.
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
        public static IServiceCollection AddRuuviTagMqttAgent<TListener>(this IServiceCollection services, IConfiguration configuration) where TListener : class, IRuuviTagListener { 
            if (services == null) {
                throw new ArgumentNullException(nameof(services));
            }

            services.Configure<DeviceCollection>(configuration.GetSection("Devices"));

            var typeResolver = new TypeResolver();

            services.AddSingleton(typeResolver);
            services.AddTransient<IMqttFactory, MqttFactory>();
            services.AddTransient<IRuuviTagListener, TListener>();
            services.AddSingleton(CommandUtilities.BuildCommandApp(typeResolver));

            return services;
        }


        /// <summary>
        /// Registers services required for a <see cref="CommandApp"/> that will run an <see cref="NRuuviTag.Mqtt.MqttAgent"/>.
        /// </summary>
        /// <typeparam name="TListener">
        ///   The <see cref="IRuuviTagListener"/> that the agent will use to listen for RuuviTag 
        ///   advertisements.
        /// </typeparam>
        /// <param name="services">
        ///   The <see cref="IServiceCollection"/>.
        /// </param>
        /// <param name="configuration">
        ///   The <see cref="Configuration.IConfiguration"/> for the application.
        /// </param>
        /// <param name="factory">
        ///   The factory for creating the <typeparamref name="TListener"/>.
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
        public static IServiceCollection AddRuuviTagMqttAgent<TListener>(this IServiceCollection services, IConfiguration configuration, Func<IServiceProvider, TListener> factory) where TListener : class, IRuuviTagListener {
            if (services == null) {
                throw new ArgumentNullException(nameof(services));
            }
            if (configuration == null) {
                throw new ArgumentNullException(nameof(configuration));
            }
            if (factory == null) {
                throw new ArgumentNullException(nameof(factory));
            }

            services.Configure<DeviceCollection>(configuration.GetSection("Devices"));

            var typeResolver = new TypeResolver();

            services.AddSingleton(typeResolver);
            services.AddTransient<IMqttFactory, MqttFactory>();
            services.AddTransient<IRuuviTagListener, TListener>();
            services.AddSingleton(CommandUtilities.BuildCommandApp(typeResolver));

            return services;
        }

    }

}
