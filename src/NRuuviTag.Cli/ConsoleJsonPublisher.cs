using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

namespace NRuuviTag.Cli {
    internal class ConsoleJsonPublisher : RuuviTagPublisher {

        private readonly Func<string, Device?>? _getDeviceInfo;

        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions() { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };


        public ConsoleJsonPublisher(
            IRuuviTagListener listener, 
            Func<string, bool>? filter,
            Func<string, Device?>? getDeviceInfo
        ) : base(listener, 0, filter) {
            _getDeviceInfo = getDeviceInfo;
        }


        protected override async Task RunAsync(IAsyncEnumerable<RuuviTagSample> samples, CancellationToken cancellationToken) {
            await foreach (var item in samples.ConfigureAwait(false)) {
                var knownDevice = _getDeviceInfo?.Invoke(item.MacAddress!);

                var json = knownDevice == null
                    ? JsonSerializer.Serialize(item, _jsonOptions)
                    : JsonSerializer.Serialize(RuuviTagSampleExtended.Create(item, knownDevice.DeviceId, knownDevice.DisplayName), _jsonOptions);

                Console.WriteLine();
                Console.WriteLine(json);
            }
        }

    }
}
