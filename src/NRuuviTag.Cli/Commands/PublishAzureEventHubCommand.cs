﻿using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NRuuviTag.AzureEventHubs;

using Spectre.Console.Cli;

namespace NRuuviTag.Cli.Commands {

    /// <summary>
    /// <see cref="CommandApp"/> command for listening to RuuviTag broadcasts and publishing the
    /// samples to an Azure Event Hub.
    /// </summary>
    public class PublishAzureEventHubCommand : AsyncCommand<PublishAzureEventHubCommandSettings> {

        /// <summary>
        /// The <see cref="IRuuviTagListener"/> to listen to broadcasts with.
        /// </summary>
        private readonly IRuuviTagListener _listener;

        /// <summary>
        /// The known RuuviTag devices.
        /// </summary>
        private readonly IOptionsMonitor<DeviceCollection> _devices;

        /// <summary>
        /// The <see cref="IHostApplicationLifetime"/> for the .NET host application.
        /// </summary>
        private readonly IHostApplicationLifetime _appLifetime;

        /// <summary>
        /// The <see cref="ILoggerFactory"/> for the application.
        /// </summary>
        private readonly ILoggerFactory _loggerFactory;


        /// <summary>
        /// Creates a new <see cref="PublishMqttCommand"/> object.
        /// </summary>
        /// <param name="listener">
        ///   The <see cref="IRuuviTagListener"/> to listen to broadcasts with.
        /// </param>
        /// <param name="mqttFactory">
        ///   The <see cref="IMqttFactory"/> that is used to create an MQTT client.
        /// </param>
        /// <param name="devices">
        ///   The known RuuviTag devices.
        /// </param>
        /// <param name="appLifetime">
        ///   The <see cref="IHostApplicationLifetime"/> for the .NET host application.
        /// </param>
        /// <param name="loggerFactory">
        ///   The <see cref="ILoggerFactory"/> for the application.
        /// </param>
        public PublishAzureEventHubCommand(
            IRuuviTagListener listener,
            IOptionsMonitor<DeviceCollection> devices,
            IHostApplicationLifetime appLifetime,
            ILoggerFactory loggerFactory
        ) {
            _listener = listener;
            _devices = devices;
            _loggerFactory = loggerFactory;
            _appLifetime = appLifetime;
        }


        /// <inheritdoc/>
        public override async Task<int> ExecuteAsync(CommandContext context, PublishAzureEventHubCommandSettings settings) {
            if (!_appLifetime.ApplicationStarted.IsCancellationRequested) {
                try { await Task.Delay(-1, _appLifetime.ApplicationStarted).ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }

            DeviceCollection? devices = null;

            void UpdateDevices(DeviceCollection? devicesFromConfig) {
                lock (this) {
                    if (devicesFromConfig == null) {
                        return;
                    }
                    devices = new DeviceCollection();
                    foreach (var item in devicesFromConfig) {
                        devices[item.Key] = new Device() { 
                            DisplayName = item.Value.DisplayName,
                            MacAddress = item.Value.MacAddress
                        };
                    }
                }
            }

            UpdateDevices(_devices.CurrentValue);

            var agentOptions = new AzureEventHubAgentOptions() {
                ConnectionString = settings.ConnectionString!,
                EventHubName = settings.EventHubName!,
                SampleRate = settings.SampleRate,
                MaximumBatchSize = settings.MaximumBatchSize,
                MaximumBatchAge = settings.MaximumBatchAge,
                KnownDevicesOnly = settings.KnownDevicesOnly,
                GetDeviceInfo = addr => {
                    lock (this) {
                        if (devices != null && devices.TryGetValue(addr, out var device)) {
                            return device;
                        }

                        return null;
                    }
                }
            };

            var agent = new AzureEventHubAgent(_listener, agentOptions, _loggerFactory.CreateLogger<AzureEventHubAgent>());

            using (_devices.OnChange(newDevices => UpdateDevices(newDevices)))
            using (var ctSource = CancellationTokenSource.CreateLinkedTokenSource(_appLifetime.ApplicationStopped, _appLifetime.ApplicationStopping)) {
                try {
                    await agent.RunAsync(ctSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
            }

            return 0;
        }

    }


    /// <summary>
    /// Settings for <see cref="PublishMqttCommand"/>.
    /// </summary>
    public class PublishAzureEventHubCommandSettings : CommandSettings {

        [CommandArgument(0, "<CONNECTION_STRING>")]
        [Description("The Azure Event Hub connection string.")]
        public string? ConnectionString { get; set; }

        [CommandArgument(1, "<EVENT_HUB_NAME>")]
        [Description("The Event Hub name.")]
        public string? EventHubName { get; set; }

        [CommandOption("--sample-rate <INTERVAL>")]
        [Description("The sample rate to use, in seconds. If not specified, samples will be published as soon as they are observed.")]
        public int SampleRate { get; set; }

        [CommandOption("--batch-size-limit <LIMIT>")]
        [DefaultValue(50)]
        [Description("Sets the maximum number of samples that can be added to an event data batch before the batch will be published to the Event Hub.")]
        public int MaximumBatchSize { get; set; }

        [CommandOption("--batch-age-limit <LIMIT>")]
        [DefaultValue(60)]
        [Description("Sets the maximum age of an event data batch (in seconds) before the batch will be published to the Event Hub.")]
        public int MaximumBatchAge { get; set; }

        [CommandOption("--known-devices")]
        [Description("Specifies if only samples from pre-registered devices should be published to the event hub.")]
        public bool KnownDevicesOnly { get; set; }

    }

}