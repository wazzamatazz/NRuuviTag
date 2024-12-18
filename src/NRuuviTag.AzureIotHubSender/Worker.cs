using System;
//using System.Text.Json;
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

        class JsonMessage
        {
                public string temp { get; set; }
                public int humidity { get; set; }
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

        private static async Task SendToAzure(DeviceClient deviceClient, RuuviTagSample data) {
            JsonMessage newMsg = new JsonMessage()
            {
                temp = "4.05",
                humidity = 750,
            };

            string ruuvipayload = JsonConvert.SerializeObject(data);
            string payload = JsonConvert.SerializeObject(newMsg);

            Console.WriteLine(ruuvipayload);


            // Send the data string as telemetry to Azure IoT Hub
            Message message = new Message(Encoding.UTF8.GetBytes(ruuvipayload))
            {
                ContentEncoding = Encoding.UTF8.ToString(),
                ContentType = "application/json"
            };
            await deviceClient.SendEventAsync(message);

            // Set the data string as the latestData property in the reported properties
            var reportedProperties = new TwinCollection();
            reportedProperties["latestData"] = data;

            await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            try {
                DeviceClient deviceClient = await ConnectToAzure();
                IRuuviTagListener client = new BlueZListener("hci0");
                //JsonSerializerOptions jsonOptions = new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

                _logger.LogInformation("Starting RuuviTag listener.");

                await foreach (var sample in client.ListenAsync(stoppingToken)) {
                    //var json = JsonSerializer.Serialize(sample, jsonOptions);

                    /*
                    var data = new TelemetryData{ 
                        Temperature = sample.Temperature,
                        Humidity = sample.Humidity,
                        MacAddress = sample.MacAddress,
                    };
                    */

                    await SendToAzure(deviceClient, sample);

                    
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) {
                _logger.LogError(e, "Error while running RuuviTag listener.");
            }
            finally {
                _logger.LogInformation("Stopped RuuviTag listener.");
            }
        }
    }
}
