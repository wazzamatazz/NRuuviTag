﻿using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NRuuviTag;
using NRuuviTag.Listener.Windows;

namespace ExampleWindowsSdkClient {
    public class Worker : BackgroundService {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger) {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            try {
                IRuuviTagListener client = new WindowsSdkListener();
                var jsonOptions = new JsonSerializerOptions() {
                    WriteIndented = true
                };

                _logger.LogInformation("Starting RuuviTag listener.");
                await foreach (var sample in client.ListenAsync(stoppingToken)) {
                    Console.WriteLine();
                    Console.WriteLine(JsonSerializer.Serialize(sample, jsonOptions));
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
