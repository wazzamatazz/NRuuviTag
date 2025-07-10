using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NRuuviTag;
using NRuuviTag.Listener.Linux;

using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;

namespace LinuxSdkClient {
    public class Worker : BackgroundService {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger) {
            _logger = logger;
        }

        private static async Task<DeviceClient> ConnectToAzure() {
            // Fetch the connection string from an environment variable
            string DeviceConnectionString = Environment.GetEnvironmentVariable("ConnectionString") ?? throw new ArgumentNullException("FATAL: Could not retrieve env-var: ConnectionString");

            // Create an instance of the device client using the connection string
            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(DeviceConnectionString, TransportType.Mqtt);
            
            // Connect to the device client
            await deviceClient.OpenAsync();
            return deviceClient;
        }

        private async Task SendToAzureAsync(DeviceClient deviceClient, RuuviTagSample data) {
            var payload = JsonConvert.SerializeObject(data);
            Console.WriteLine(payload);

            // Send the data string as telemetry to Azure IoT Hub
            Message message = new Message(Encoding.UTF8.GetBytes(payload))
            {
                ContentEncoding = Encoding.UTF8.ToString(),
                ContentType = "application/json"
            };
            var sendTask = deviceClient.SendEventAsync(message);

            // Set the data string as the latestData property in the reported properties
            var patch = new TwinCollection();
            var latest = new TwinCollection();
            latest[data.MacAddress] = data;
            patch["latestData"] = latest;
            var twinTask = deviceClient.UpdateReportedPropertiesAsync(patch);

            try {
                await twinTask;
                await sendTask;
            }
            catch (Exception e) {
                _logger.LogError(e, "Failed to process Azure communication.");
            }
        }

        private async Task UpdateEndDeviceAsync(DeviceClient deviceClient, RuuviTagSample sample, CancellationToken ct) {
            var twin = await deviceClient.GetTwinAsync(ct);
            
            // Get the current endDevices array
            var devsToken = twin.Properties.Desired["endDevices"];
            var devsTokenR = twin.Properties.Reported["endDevices"];

            var endDevices = devsToken as JArray; // Cant modify so dont set => Jarray or null
            var endDevicesR = devsTokenR is JArray og ? new JArray(og) : new JArray();

            var dev = endDevices?.FirstOrDefault(i => i["mac"]?.ToString() == sample.MacAddress);
            var devR = endDevicesR.FirstOrDefault(i => i["mac"]?.ToString() == sample.MacAddress);

            // if dev null => all values null
            // if devR null => copy from dev

            var devN = devR ?? new JObject();
            
            // Get pre-set properties from desired if available
            // Do this before overriding the specific values
            if (dev != null) {
                foreach (JProperty prop in dev)
                {
                    // Skip system-reserved properties (e.g., $version)
                    if (!prop.Name.StartsWith("$")) {
                        devN[prop.Name] = prop.Value;
                    }
                }
            }
 
            // Update/Override other necessary properties here
            // Volatile values reported by the ruuvitag devices
            // TODO internal var for keeping track of tags in memory
            // Update values from the var
            devN["firmwareVersion"] = "TODO";
            devN["displayName"] = "TODO";
            devN["deviceId"] = "TODO";

            // If dev was missing, add it
            if (devR == null) {
                endDevicesR.Add(devN);
            }

            var patch = new TwinCollection();
            patch["endDevices"] = endDevicesR;

            try {
                await deviceClient.UpdateReportedPropertiesAsync(patch);
            }
            catch (Exception e) {
                _logger.LogError(e, "Failed to update Device Twin Reported Props.");
            }            
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            var excs = new List<DateTime>();
            while (true) {
                try {
                    DeviceClient deviceClient = await ConnectToAzure();
                    IRuuviTagListener client = new BlueZListener("hci0");

                    // TODO: update dynamically in bg (start callback func in bg?)
                    var twin = await deviceClient.GetTwinAsync(stoppingToken);

                    // Get device whitelist from twin
                    JToken? devsTok = twin.Properties.Desired["endDevices"];
                    JArray? whiteList = devsTok is JArray arr ? arr : null;

                    _logger.LogInformation("RuuviTag listener configured.");

                    // Keep track of received samples macs
                    var macs = new List<string>();
                    // Only catch whitelisted mac reports
                    // If whitelist not defined, allow any device
                    await foreach (var sample in client.ListenAsync(i => whiteList?.Any(j => j["mac"]?.ToString() == i) ?? true, stoppingToken)) {
                        // Verify sender mac is whitelisted first
                        // If configured in twin, and mac not found, skip sample
                        // if (whiteList?.Any(j => j["mac"]?.ToString() == sample.MacAddress))
                        // {
                        //     _logger.LogInformation($"Caught non-whitelisted sample: {sample}");
                        //     continue;
                        // }
                        
                        // Skip any samples older than 3 min
                        if (sample.Timestamp < DateTime.UtcNow.AddMinutes(-3)) {
                            continue;
                        }
    
                        // If measurement from this mac during this round not yet sent
                        // Send to Azure in the background
                        // POTENTIAL BUG: IF 2+ SAMPLES IN MAC ORDER, SAMPLES ARE SKIPPED
                        // TODO: change limiter behaviour, collect asynclist in the bg, another sync loop fetching all collected values every 5min delay and process 
                        if (sample.MacAddress != null && !macs.Contains(sample.MacAddress)) {
                            _ = SendToAzureAsync(deviceClient, sample);
                            _ = UpdateEndDeviceAsync(deviceClient, sample, stoppingToken);
                            macs.Add(sample.MacAddress);
                            continue;
                        }
    
                        // Start over after sleep
                        macs.Clear();
                        // Sleep 5m (* 60s * 1000ms)
                        await Task.Delay(5*60*1000);
                    }
                }
                catch (OperationCanceledException ec) {
                    _logger.LogError(ec, "Execution Canceled.");
                    break;
                }
                catch (Exception e) {
                    _logger.LogError(e, "Error while running RuuviTag listener.");
                    excs.Add(DateTime.Now);
                    // If too many recent errors
                    if (excs.Count >= 5) {
                        // All last 5 errors within one minute, sleep & restart the collector
                        if (excs.All(t => t > DateTime.Now.AddMinutes(-1))) {
                            await Task.Delay(60 * 1000);
                            continue;
                        }
                        excs.Clear();
                    }
                    // Otherwise, a short delay
                    else {
                        await Task.Delay(10 * 1000);
                    }
                }
            }
        }
    }
}
