using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace NRuuviTag.Cli;

internal class ConsoleJsonPublisher : RuuviTagPublisher {
    
    public ConsoleJsonPublisher(IRuuviTagListener listener) 
        : base(listener, new RuuviTagPublisherOptions()) { }


    protected override async Task RunAsync(ChannelReader<RuuviTagSample> samples, CancellationToken cancellationToken) {
        while (await samples.WaitToReadAsync(cancellationToken)) {
            while (samples.TryRead(out var item)) {
                var json = JsonSerializer.Serialize(item, RuuviJsonSerializerContext.Default.RuuviTagSample);
                Console.WriteLine(json);
            }
        }
    }

}
