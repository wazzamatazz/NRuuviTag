using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NRuuviTag.Listener.Linux;

namespace NRuuviTag.Cli.Windows {

    /// <summary>
    /// RuuviTag command-line application.
    /// </summary>
    public class Program {

        public static async Task<int> Main(string[] args) {
           return await CreateHostBuilder(args)
                .BuildAndRunRuuviTagMqttAgent(args)
                .ConfigureAwait(false);
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseContentRoot(AppContext.BaseDirectory)
                .ConfigureAppConfiguration(config => {
                    config.AddRuuviTagMqttDeviceConfiguration();
                })
                .ConfigureServices((hostContext, services) => {
                    services.AddRuuviTagMqttAgent(hostContext.Configuration, sp => ActivatorUtilities.CreateInstance<BlueZListener>(sp, BlueZListener.DefaultBluetoothAdapter));
                });

    }
}
