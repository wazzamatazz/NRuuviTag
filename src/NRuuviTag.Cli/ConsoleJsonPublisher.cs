using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace NRuuviTag.Cli;

internal class ConsoleJsonPublisher : RuuviTagPublisher {
    
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions() { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };


    public ConsoleJsonPublisher(IRuuviTagListener listener) 
        : base(listener, new RuuviTagPublisherOptions()) { }


    protected override async Task RunAsync(ChannelReader<RuuviTagSample> samples, CancellationToken cancellationToken) {
        while (await samples.WaitToReadAsync(cancellationToken)) {
            while (samples.TryRead(out var item)) {
                var json = JsonSerializer.Serialize(item, _jsonOptions);
                Console.WriteLine(json);
            }
        }
    }

}
