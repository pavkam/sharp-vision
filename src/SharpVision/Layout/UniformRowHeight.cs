// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;

/// <summary>Validates and resolves one uniform virtualized-row request against a final viewport.</summary>
/// <remarks>
/// A virtualizing panel that lays out one axis in equal-height (or equal-width) strides - the way
/// <see cref="Controls.Collections.ListView"/> and <see cref="Controls.Layout.Table"/> both do
/// internally for their rows - resolves its own <see cref="Length"/> request through this type
/// instead of reimplementing the same rounding, floor, and offset-remap rules. Every member is a
/// pure function of its arguments; none reads or changes any owning control's state.
/// </remarks>
[PublicAPI]
public static class UniformRowHeight
{
    /// <summary>Validates a row request before its owner changes observable state.</summary>
    /// <param name="value">The candidate automatic, fixed, or percentage request.</param>
    /// <param name="allowAuto">Whether automatic sizing represents an eager non-virtualized mode.</param>
    /// <param name="paramName">The public argument or property-setter parameter name.</param>
    /// <exception cref="ArgumentException">The request is automatic when disallowed or uses proportional sizing.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The fixed or percentage request is zero.</exception>
    public static void Validate(Length value, bool allowAuto, string paramName)
    {
        if (value.Kind == LengthKind.Auto && !allowAuto)
        {
            throw new ArgumentException("A progressive uniform row height cannot be automatic.", paramName);
        }

        if (value.Kind == LengthKind.Star)
        {
            throw new ArgumentException("A uniform row height cannot use proportional sizing.", paramName);
        }

        if (value.Kind is LengthKind.Cells or LengthKind.Percent && value.Value == 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "A uniform row height must be positive.");
        }
    }

    /// <summary>Resolves a validated fixed or percentage request to one positive cell height.</summary>
    /// <param name="value">The validated non-automatic, non-proportional request.</param>
    /// <param name="viewportHeight">The final non-negative scrollbar-aware viewport height.</param>
    /// <returns>
    /// A fixed request's cell count, returned as-is - a zero or negative fixed request is a caller
    /// error <see cref="Validate"/> already rejects, not something this method re-clamps. A
    /// percentage request resolves against <paramref name="viewportHeight"/> and floors at one cell,
    /// so an empty or very small viewport still yields a well-defined positive stride instead of a
    /// degenerate zero-height row.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is automatic or uses proportional sizing.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="viewportHeight"/> is negative.</exception>
    [Pure]
    public static int Resolve(Length value, int viewportHeight)
    {
        if (value.Kind is not (LengthKind.Cells or LengthKind.Percent))
        {
            throw new ArgumentException("Only a fixed or percentage request resolves to cells.", nameof(value));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(viewportHeight);

        if (value.Kind == LengthKind.Cells)
        {
            return (int) value.Value;
        }

        var resolved = Math.Round(viewportHeight * value.Value / 100, MidpointRounding.AwayFromZero);
        return Math.Max(1, resolved >= int.MaxValue ? int.MaxValue : (int) resolved);
    }

    /// <summary>Maps a cell offset so the same logical row and proportional point within its
    /// gap-inclusive stride remain anchored after responsive height resolution.</summary>
    /// <param name="offset">The prior non-negative content offset.</param>
    /// <param name="previousHeight">The prior positive resolved row height.</param>
    /// <param name="currentHeight">The current positive resolved row height.</param>
    /// <param name="gap">The non-negative fixed gap after each row.</param>
    /// <returns>The saturating offset into the same logical stride.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="offset"/> or <paramref name="gap"/> is negative, or <paramref
    /// name="previousHeight"/> or <paramref name="currentHeight"/> is not positive.
    /// </exception>
    [Pure]
    public static int RemapOffset(int offset, int previousHeight, int currentHeight, int gap)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(previousHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(currentHeight);
        ArgumentOutOfRangeException.ThrowIfNegative(gap);

        var previousStride = previousHeight.Add(gap);
        var currentStride = currentHeight.Add(gap);
        var index = offset / previousStride;
        var within = offset % previousStride;
        var mappedWithin = Math.Min(
            currentStride - 1,
            (int) Math.Round((double) within * currentStride / previousStride, MidpointRounding.AwayFromZero));
        return index.Multiply(currentStride).Add(mappedWithin);
    }
}
