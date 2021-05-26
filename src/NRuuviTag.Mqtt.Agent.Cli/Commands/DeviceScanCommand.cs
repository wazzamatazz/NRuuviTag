using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Spectre.Console;
using Spectre.Console.Cli;

namespace NRuuviTag.Mqtt.Cli.Commands {
    public class DeviceScanCommand : AsyncCommand<DeviceScanCommandSettings> {

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
        /// Creates a new <see cref="DeviceScanCommand"/> object.
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
        public DeviceScanCommand(IRuuviTagListener listener, IOptionsMonitor<DeviceCollection> devices, IHostApplicationLifetime appLifetime) {
            _listener = listener;
            _devices = devices;
            _appLifetime = appLifetime;
        }


        /// <inheritdoc/>
        public override async Task<int> ExecuteAsync(CommandContext context, DeviceScanCommandSettings settings) {
            // Wait until the host application has started if required.
            if (!_appLifetime.ApplicationStarted.IsCancellationRequested) {
                try { await Task.Delay(-1, _appLifetime.ApplicationStarted).ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }

            Dictionary<string, DeviceInfo>? devices;

            // Updates the set of known devices when _devices reports that the options have been
            // updated.
            void UpdateDevices(DeviceCollection? devicesFromConfig) {
                lock (this) {
                    devices = devicesFromConfig?.ToDictionary(x => x.Value.MacAddress, x => new DeviceInfo() {
                        DeviceId = x.Key,
                        MacAddress = x.Value.MacAddress,
                        DisplayName = x.Value.DisplayName
                    });
                }
            }

            UpdateDevices(_devices.CurrentValue);

            using (_devices.OnChange(newDevices => UpdateDevices(newDevices)))
            using (var ctSource = CancellationTokenSource.CreateLinkedTokenSource(_appLifetime.ApplicationStopped, _appLifetime.ApplicationStopping)) {
                try {
                    var detectedMacAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    Console.WriteLine();
                    Console.WriteLine(Resources.LogMessage_StartingDeviceScan);
                    Console.WriteLine();

                    await foreach (var sample in _listener.ListenAsync(ctSource.Token).ConfigureAwait(false)) {
                        if (string.IsNullOrWhiteSpace(sample.MacAddress)) {
                            continue;
                        }

                        if (detectedMacAddresses.Add(sample.MacAddress!)) {
                            // This is the first time we've observed this device during this scan.
                            lock (this) {
                                if (devices != null && devices.TryGetValue(sample.MacAddress!, out var deviceInfo)) {
                                    Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Resources.LogMessage_DeviceScanResultKnown, sample.MacAddress, deviceInfo.DisplayName, deviceInfo.DeviceId));
                                }
                                else {
                                    Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Resources.LogMessage_DeviceScanResultUnknown, sample.MacAddress));
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { }
            }

            Console.WriteLine();
            return 0;
        }

    }


    /// <summary>
    /// Settings for <see cref="DeviceScanCommand"/>.
    /// </summary>
    public class DeviceScanCommandSettings : CommandSettings { }

}
