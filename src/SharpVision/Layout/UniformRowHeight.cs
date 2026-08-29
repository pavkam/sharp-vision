// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;

/// <summary>Validates and resolves one uniform virtualized-row request against a final viewport.</summary>
internal static class UniformRowHeight
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
    /// <param name="value">The validated non-automatic request.</param>
    /// <param name="viewportHeight">The final non-negative scrollbar-aware viewport height.</param>
    /// <returns>The fixed height or percentage result, clamped to one cell when the viewport is empty or rounding yields zero.</returns>
    [Pure]
    public static int Resolve(Length value, int viewportHeight)
    {
        Debug.Assert(value.Kind is LengthKind.Cells or LengthKind.Percent, "Only uniform row requests resolve to cells.");
        Debug.Assert(viewportHeight >= 0, "Viewport height is non-negative.");

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
    [Pure]
    public static int RemapOffset(int offset, int previousHeight, int currentHeight, int gap)
    {
        Debug.Assert(offset >= 0, "A scroll offset is non-negative.");
        Debug.Assert(previousHeight > 0 && currentHeight > 0, "Resolved row heights are positive.");
        Debug.Assert(gap >= 0, "A row gap is non-negative.");

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
