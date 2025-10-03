using System;
using System.Collections.Generic;

namespace NRuuviTag;

/// <summary>
/// <see cref="IEqualityComparer{T}"/> for comparing MAC address strings.
/// </summary>
public sealed class MacAddressComparer : IEqualityComparer<string> {

    /// <summary>
    /// The <see cref="MacAddressComparer"/> instance.
    /// </summary>
    public static MacAddressComparer Instance { get; } = new MacAddressComparer();


    /// <summary>
    /// Creates a new <see cref="MacAddressComparer"/> instance.
    /// </summary>
    private MacAddressComparer() { }


    /// <inheritdoc/>
    public bool Equals(string? x, string? y) {
        if (x == null && y == null) {
            return true;
        }

        if (x == null || y == null) {
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
        if (obj == null) {
            throw new ArgumentNullException(nameof(obj));
        }
        return RuuviTagUtilities.TryConvertMacAddressToUInt64(obj, out var address)
            ? address.GetHashCode()
            : obj.GetHashCode();
    }

}