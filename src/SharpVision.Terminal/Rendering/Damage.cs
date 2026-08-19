// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>
/// Provides allocation-free semantic damage enumeration between complete frames.
/// </summary>
[PublicAPI]
public static class Damage
{
    /// <summary>Compares complete ordered semantic graphics placements.</summary>
    /// <param name="front">The committed frame, or null before the first commit.</param>
    /// <param name="back">The target frame.</param>
    /// <param name="full">Whether complete graphics reconstruction is required.</param>
    /// <returns>Whether a backend must replace its remote graphics state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="back"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">A supplied frame is disposed.</exception>
    [Pure]
    public static bool PlacementsChanged(Frame? front, Frame back, bool full = false)
    {
        ArgumentNullException.ThrowIfNull(back);
        back.ThrowIfDisposed();
        front?.ThrowIfDisposed();
        return full || front is null || !front.EffectivePlacementsEqual(back);
    }

    /// <summary>Enumerates merged grapheme-safe changed runs.</summary>
    /// <param name="front">The committed frame, or null for a full redraw.</param>
    /// <param name="back">The target frame.</param>
    /// <param name="full">Whether to force complete target damage.</param>
    /// <returns>An allocation-free enumerable.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="back"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">A supplied frame is disposed.</exception>
    public static DamageEnumerable Enumerate(Frame? front, Frame back, bool full = false)
    {
        ArgumentNullException.ThrowIfNull(back);
        back.ThrowIfDisposed();
        front?.ThrowIfDisposed();
        return new DamageEnumerable(front, back, full);
    }
}
