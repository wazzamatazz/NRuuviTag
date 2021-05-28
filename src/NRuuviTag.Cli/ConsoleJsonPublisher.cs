using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NRuuviTag.Cli {
    internal class ConsoleJsonPublisher : RuuviTagPublisher {

        public ConsoleJsonPublisher(IRuuviTagListener listener, Func<string, bool>? filter) : base(listener, 0, filter) { }


        protected override async Task RunAsync(IAsyncEnumerable<RuuviTagSample> samples, CancellationToken cancellationToken) {
            await foreach (var item in samples.ConfigureAwait(false)) {
                Console.WriteLine();
                Console.WriteLine(JsonSerializer.Serialize(item));
            }
        }

    }
}
