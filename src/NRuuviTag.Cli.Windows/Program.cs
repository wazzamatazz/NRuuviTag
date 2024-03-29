﻿using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NRuuviTag.Listener.Windows;

namespace NRuuviTag.Cli.Windows {

    /// <summary>
    /// RuuviTag MQTT agent command-line application.
    /// </summary>
    public class Program {

        public static async Task<int> Main(string[] args) {
            return await CreateHostBuilder(args)
                .BuildAndRunRuuviTagPublisher(args)
                .ConfigureAwait(false);
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseContentRoot(AppContext.BaseDirectory)
                .ConfigureAppConfiguration(config => {
                    config.AddRuuviTagDeviceConfiguration();
                })
                .ConfigureServices((hostContext, services) => {
                    services.AddRuuviTagPublisherCommandApp<WindowsSdkListener>(hostContext.Configuration);
                });

    }
}
