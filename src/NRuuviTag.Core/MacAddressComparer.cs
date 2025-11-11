using System;
using System.Collections.Generic;

namespace NRuuviTag;

/// <summary>
/// <see cref="IEqualityComparer{T}"/> for comparing MAC address strings.
/// </summary>
public sealed class MacAddressComparer : IEqualityComparer<string> {

    /// <summary>
    /// The default <see cref="MacAddressComparer"/> instance.
    /// </summary>
    public static MacAddressComparer Default { get; } = new MacAddressComparer();


    /// <summary>
    /// Creates a new <see cref="MacAddressComparer"/> instance.
    /// </summary>
    private MacAddressComparer() { }


    /// <inheritdoc/>
    public bool Equals(string? x, string? y) {
        if (x is null && y is null) {
            return true;
        }

        if (x is null || y is null) {
            return false;
        }

        if (string.Equals(x, y, StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        if (!RuuviTagUtilities.TryConvertMacAddressToUInt64(x, out var addrX)) {
            return false;
        }

        if (!RuuviTagUtilities.TryConvertMacAddressToUInt64(y, out var addrY)) {
            return false;
        }

        return addrX == addrY;
    }


    /// <inheritdoc/>
    public int GetHashCode(string obj) {
        ArgumentNullException.ThrowIfNull(obj);
        return RuuviTagUtilities.TryConvertMacAddressToUInt64(obj, out var address)
            ? address.GetHashCode()
            : obj.GetHashCode();
    }

}
