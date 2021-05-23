using System;
using System.Collections.Generic;
using System.Threading;

namespace NRuuviTag {

    /// <summary>
    /// Extensions for <see cref="IRuuviTagListener"/>.
    /// </summary>
    public static class RuuviTagListenerExtensions {

        /// <summary>
        /// Listens for advertisements broadcast by all nearby RuuviTag devices until cancelled.
        /// </summary>
        /// <param name="cancellationToken">
        ///   A cancellation token that can be cancelled when the listener should stop.
        /// </param>
        /// <returns>
        ///   An <see cref="IAsyncEnumerable{RuuviTagSample}"/> that will emit the received 
        ///   samples as they occur.
        /// </returns>
        public static IAsyncEnumerable<RuuviTagSample> ListenAsync(
            this IRuuviTagListener listener,
            CancellationToken cancellationToken
        ) {
            if (listener == null) {
                throw new ArgumentNullException(nameof(listener));
            }

            return listener.ListenAsync(null, cancellationToken);
        }

    }
}
