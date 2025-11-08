using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace NRuuviTag.Cli;

internal class ConsoleJsonPublisher : RuuviTagPublisher {

    private readonly Func<string, Device?>? _getDeviceInfo;

    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions() { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };


    public ConsoleJsonPublisher(
        IRuuviTagListener listener, 
        Func<string, bool>? filter,
        Func<string, Device?>? getDeviceInfo
    ) : base(listener, new RuuviTagPublisherOptions(), filter) {
        _getDeviceInfo = getDeviceInfo;
    }


    protected override async Task RunAsync(ChannelReader<RuuviTagSample> samples, CancellationToken cancellationToken) {
        while (await samples.WaitToReadAsync(cancellationToken)) {
            while (samples.TryRead(out var item)) {
                var knownDevice = _getDeviceInfo?.Invoke(item.MacAddress!);

                var json = knownDevice == null
                    ? JsonSerializer.Serialize(item, _jsonOptions)
                    : JsonSerializer.Serialize(new RuuviTagSampleExtended(knownDevice.DeviceId, knownDevice.DisplayName, item), _jsonOptions);

                Console.WriteLine(json);
            }
        }
    }

}
