using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using NRuuviTag.Mqtt.Cli;

using Spectre.Console.Cli;

namespace Microsoft.Extensions.Hosting {

    /// <summary>
    /// Extensions for <see cref="IHostBuilder"/>.
    /// </summary>
    public static class NRuuviTagHostBuilderExtensions {

        /// <summary>
        /// Builds an <see cref="IHost"/> and runs a <see cref="CommandApp"/> for an MQTT agent using 
        /// the specified command-line arguments.
        /// </summary>
        /// <param name="builder">
        ///   The <see cref="IHostBuilder"/>.
        /// </param>
        /// <param name="args">
        ///   The command-line arguments.
        /// </param>
        /// <returns>
        ///   A <see cref="Task{Int32}"/> that will return the result of the underlying <see cref="CommandApp"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="builder"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="args"/> is <see langword="null"/>.
        /// </exception>
        public static async Task<int> BuildAndRunRuuviTagMqttAgent(this IHostBuilder builder, IEnumerable<string> args) {
            if (builder == null) {
                throw new ArgumentNullException(nameof(builder));
            }
            if (args == null) {
                throw new ArgumentNullException(nameof(args));
            }

            using (var host = builder.Build()) {
                await host.StartAsync().ConfigureAwait(false);

                using (var scope = host.Services.CreateScope()) {
                    var typeRegistrar = scope.ServiceProvider.GetRequiredService<TypeResolver>();
                    typeRegistrar.ServiceProvider = scope.ServiceProvider;

                    var app = host.Services.GetRequiredService<CommandApp>();
                    return await app.RunAsync(args).ConfigureAwait(false);
                }
            }
        }

    }

}
