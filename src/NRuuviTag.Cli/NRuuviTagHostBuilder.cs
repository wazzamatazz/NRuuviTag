using System;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NRuuviTag.Cli {

    /// <summary>
    /// Extensions for creating <see cref="IHostBuilder"/> instances for running the NRuuviTag command app.
    /// </summary>
    public static class NRuuviTagHostBuilder {

        /// <summary>
        /// Creates a new <see cref="IHostBuilder"/> instance for running the NRuuviTag command app.
        /// </summary>
        /// <typeparam name="TListener">
        ///   The type of <see cref="RuuviTagListener"/> to use.
        /// </typeparam>
        /// <param name="args">
        ///   The command-line arguments.
        /// </param>
        /// <returns>
        ///   A new <see cref="IHostBuilder"/> instance.
        /// </returns>
        public static IHostBuilder CreateHostBuilder<TListener>(string[]? args) where TListener : RuuviTagListener {
            return CreateHostBuilderCore(args, (hostContext, services) => {
                services.AddRuuviTagCommandApp<TListener>(hostContext.Configuration);
            });
        }


        /// <summary>
        /// Creates a new <see cref="IHostBuilder"/> instance for running the NRuuviTag command app.
        /// </summary>
        /// <typeparam name="TListener">
        ///   The type of <see cref="RuuviTagListener"/> to use.
        /// </typeparam>
        /// <param name="args">
        ///   The command-line arguments.
        /// </param>
        /// <param name="listenerInstance">
        ///   The listener instance to use.
        /// </param>
        /// <returns>
        ///   A new <see cref="IHostBuilder"/> instance.
        /// </returns>
        public static IHostBuilder CreateHostBuilder<TListener>(string[]? args, TListener listenerInstance) where TListener : RuuviTagListener {
            ArgumentNullException.ThrowIfNull(listenerInstance);
            return CreateHostBuilder(args, sp => listenerInstance);
        }



        /// <summary>
        /// Creates a new <see cref="IHostBuilder"/> instance for running the NRuuviTag command app.
        /// </summary>
        /// <typeparam name="TListener">
        ///   The type of <see cref="RuuviTagListener"/> to use.
        /// </typeparam>
        /// <param name="args">
        ///   The command-line arguments.
        /// </param>
        /// <param name="listenerFactory">
        ///   The listener factory to use.
        /// </param>
        /// <returns>
        ///   A new <see cref="IHostBuilder"/> instance.
        /// </returns>
        public static IHostBuilder CreateHostBuilder<TListener>(string[]? args, Func<IServiceProvider, TListener> listenerFactory) where TListener : RuuviTagListener {
            ArgumentNullException.ThrowIfNull(listenerFactory);
            return CreateHostBuilderCore(args, (hostContext, services) => {
                services.AddRuuviTagCommandApp(hostContext.Configuration, sp => listenerFactory.Invoke(sp));
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
}
