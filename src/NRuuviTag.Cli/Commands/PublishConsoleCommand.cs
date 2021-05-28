using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NRuuviTag.Mqtt;

using Spectre.Console.Cli;

namespace NRuuviTag.Cli.Commands {

    /// <summary>
    /// <see cref="CommandApp"/> command for listening to RuuviTag broadcasts without forwarding 
    /// them to an MQTT broker.
    /// </summary>
    public class PublishConsoleCommand : AsyncCommand<PublishConsoleCommandSettings> {

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
        /// Creates a new <see cref="PublishConsoleCommand"/> object.
        /// </summary>
        /// <param name="listener">
        ///   The <see cref="IRuuviTagListener"/> to listen to broadcasts with.
        /// </param>
        /// <param name="devices">
        ///   The known RuuviTag devices.
        /// </param>
        /// <param name="appLifetime">
        ///   The <see cref="IHostApplicationLifetime"/> for the .NET host application.
        /// </param>
        public PublishConsoleCommand(IRuuviTagListener listener, IOptionsMonitor<DeviceCollection> devices, IHostApplicationLifetime appLifetime) {
            _listener = listener;
            _devices = devices;
            _appLifetime = appLifetime;
        }


        /// <inheritdoc/>
        public override async Task<int> ExecuteAsync(CommandContext context, PublishConsoleCommandSettings settings) {
            // Wait until the host application has started if required.
            if (!_appLifetime.ApplicationStarted.IsCancellationRequested) {
                try { await Task.Delay(-1, _appLifetime.ApplicationStarted).ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }

            Dictionary<string, MqttDeviceInfo>? devices;

            // Updates the set of known devices when _devices reports that the options have been
            // updated.
            void UpdateDevices(DeviceCollection? devicesFromConfig) {
                lock (this) {
                    devices = devicesFromConfig?.ToDictionary(x => x.Value.MacAddress, x => new MqttDeviceInfo() {
                        DeviceId = x.Key,
                        MacAddress = x.Value.MacAddress,
                        DisplayName = x.Value.DisplayName
                    });
                }
            }

            // Tests if a sample from the specified MAC address should be displayed.
            bool CanProcessSample(string macAddress) {
                if (!settings.KnownDevicesOnly) {
                    return true;
                }

                lock (this) {
                    return devices?.ContainsKey(macAddress) ?? false;
                }
            }

            UpdateDevices(_devices.CurrentValue);

            var publisher = new ConsoleJsonPublisher(_listener, CanProcessSample);

            using (_devices.OnChange(newDevices => UpdateDevices(newDevices)))
            using (var ctSource = CancellationTokenSource.CreateLinkedTokenSource(_appLifetime.ApplicationStopped, _appLifetime.ApplicationStopping)) {
                try {
                    await publisher.RunAsync(ctSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
            }

            Console.WriteLine();
            return 0;
        }
    }


    /// <summary>
    /// Settings for <see cref="PublishConsoleCommand"/>.
    /// </summary>
    public class PublishConsoleCommandSettings : CommandSettings {

        [CommandOption("--known-devices")]
        [Description("Specifies if only samples from pre-registered devices should be observed.")]
        public bool KnownDevicesOnly { get; set; }

    }

}
