using System.Collections.Generic;
using System.Threading;

namespace NRuuviTag;

/// <summary>
/// A client for listening to Bluetooth LE broadcasts from nearby Ruuvi devices.
/// </summary>
public interface IRuuviTagListener {

    /// <summary>
    /// Listens for advertisements broadcast by nearby Ruuvi devices until cancelled.
    /// </summary>
    /// <param name="cancellationToken">
    ///   A cancellation token that can be cancelled when the listener should stop.
    /// </param>
    /// <returns>
    ///   An <see cref="IAsyncEnumerable{T}"/> that will emit the received samples as they occur.
    /// </returns>
    IAsyncEnumerable<RuuviTagSample> ListenAsync(CancellationToken cancellationToken);

}
