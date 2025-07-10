using System;
using System.Text;
using Newtonsoft.Json;
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
            string DeviceConnectionString = Environment.GetEnvironmentVariable("ConnectionString");

            // Create an instance of the device client using the connection string
            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(DeviceConnectionString, TransportType.Mqtt);
            
            // Connect to the device client
            await deviceClient.OpenAsync();
            return deviceClient;
        }

        private static async Task SendToAzureAsync(DeviceClient deviceClient, RuuviTagSample data) {
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
            var twinPatch = new TwinCollection();
            var latest = new TwinCollection();
            latest[data.MacAddress] = data;
            twinPatch["latestData"] = latest;
            var twinTask = deviceClient.UpdateReportedPropertiesAsync(twinPatch);

            try {
                await twinTask;
                await sendTask;
            }
            catch (Exception e) {
                _logger.LogError(e, "Failed to process Azure communication.");
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

                    _logger.LogInformation("RuuviTag listener configured.");

                    // Keep track of received samples macs
                    var macs = new List<string>();
                    await foreach (var sample in client.ListenAsync(stoppingToken)) {
                        // Verify sender mac is whitelisted first
                        // If configured in twin, and mac not found, skip sample
                        if (twin.Properties.Desired.TryGetValue("endDevices", out var whiteList) &&
                            whiteList?.FirstOrDefault(i => i.Contains("mac") && i["mac"] == sample.MacAddress) == null)
                        {
                            continue;
                        }
                        
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
                            Task.Delay(60 * 1000);
                            continue;
                        }
                        excs.Clear();
                    }
                    // Otherwise, a short delay
                    else {
                        Task.Delay(10 * 1000);
                    }
                }
                finally {
                    _logger.LogInformation("Stopped RuuviTag listener.");
                    break;
                }
            }
        }
    }
}
