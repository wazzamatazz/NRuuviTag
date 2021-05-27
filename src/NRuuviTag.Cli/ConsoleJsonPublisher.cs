using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NRuuviTag.Cli {
    internal class ConsoleJsonPublisher : RuuviTagPublisher {

        public ConsoleJsonPublisher(IRuuviTagListener listener, Func<string, bool>? filter) : base(listener, 0, filter) { }


        protected override Task PublishAsyncCore(RuuviTagPublisherContext context, IEnumerable<RuuviTagSample> samples, CancellationToken cancellationToken) {
            foreach (var sample in samples) {
                Console.WriteLine();
                Console.WriteLine(JsonSerializer.Serialize(sample));
            }

            return Task.CompletedTask;
        }

    }
}
