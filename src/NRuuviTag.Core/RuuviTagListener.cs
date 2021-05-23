using System;
using System.Collections.Generic;
using System.Threading;

namespace NRuuviTag {

    /// <summary>
    /// Base <see cref="IRuuviTagListener"/> implementation.
    /// </summary>
    public abstract class RuuviTagListener : IRuuviTagListener {

        /// <inheritdoc/>
        public IAsyncEnumerable<RuuviTagSample> ListenAsync(CancellationToken cancellationToken) {
            return ListenAsync(null, cancellationToken);
        }


        /// <inheritdoc/>
        public abstract IAsyncEnumerable<RuuviTagSample> ListenAsync(Func<string, bool>? filter, CancellationToken cancellationToken);

    }
}
