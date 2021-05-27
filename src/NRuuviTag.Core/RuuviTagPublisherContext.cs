using System.Collections.Generic;
using System.Collections.Concurrent;
using System;
using System.Threading.Tasks;

namespace NRuuviTag {

    /// <summary>
    /// Context object used by <see cref="RuuviTagPublisher"/>.
    /// </summary>
    public class RuuviTagPublisherContext : IAsyncDisposable {

        /// <summary>
        /// A set of custom items associated with a running <see cref="RuuviTagPublisher"/>.
        /// </summary>
        /// <remarks>
        ///   The property is thread-safe. However, individual items in the collection may not be.
        /// </remarks>
        public IDictionary<string, object> Items { get; } = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);


        /// <inheritdoc/>
        async ValueTask IAsyncDisposable.DisposeAsync() {
            var items = Items.Values;
            Items.Clear();
            foreach (var item in items) {
                if (item is IAsyncDisposable asyncDisposable) {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else if (item is IDisposable disposable) {
                    disposable.Dispose();
                }
            }
        }

    }
}
