using System;
using System.Collections.Generic;
using System.Threading;

namespace NRuuviTag {

    /// <summary>
    /// A client for listening to Bluetooth LE broadcasts from nearby RuuviTag devices.
    /// </summary>
    public interface IRuuviTagListener {

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
        IAsyncEnumerable<RuuviTagSample> ListenAsync(CancellationToken cancellationToken);


        /// <summary>
        /// Listens for advertisements broadcast by RuuviTag devices until cancelled.
        /// </summary>
        /// <param name="filter">
        ///   An optional callback that can be used to limit the listener to specific RuuviTag MAC 
        ///   addresses. The parameter passed to the callback is the MAC address of the RuuviTag 
        ///   that a broadcast was received from.
        /// </param>
        /// <param name="cancellationToken">
        ///   A cancellation token that can be cancelled when the listener should stop.
        /// </param>
        /// <returns>
        ///   An <see cref="IAsyncEnumerable{RuuviTagSample}"/> that will emit the received 
        ///   samples as they occur.
        /// </returns>
        IAsyncEnumerable<RuuviTagSample> ListenAsync(Func<string, bool>? filter, CancellationToken cancellationToken);

    }
}
