// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Resolves the two border-box tracks and exterior margins owned by a split pane.</summary>
internal static class SplitPaneLayout
{
    /// <summary>Allocates pane extents and the leading pane's jointly feasible divider range.</summary>
    /// <param name="firstLength">The leading pane's fixed or percentage border-box request.</param>
    /// <param name="firstAutomatic">The leading pane's intrinsic border-box request.</param>
    /// <param name="secondAutomatic">The trailing pane's intrinsic border-box request.</param>
    /// <param name="firstMinimum">The leading pane's resolved border-box minimum.</param>
    /// <param name="firstMaximum">The leading pane's resolved border-box maximum.</param>
    /// <param name="secondMinimum">The trailing pane's resolved border-box minimum.</param>
    /// <param name="secondMaximum">The trailing pane's resolved border-box maximum.</param>
    /// <param name="firstMargin">The leading pane's margin on the split axis.</param>
    /// <param name="secondMargin">The trailing pane's margin on the split axis.</param>
    /// <param name="available">The finite divider-excluded outer pool, or null during intrinsic allocation.</param>
    /// <param name="percentBase">The containing axis used by percentage requests and limits.</param>
    /// <param name="extents">Two caller-owned cells receiving border-box extents in source order.</param>
    /// <param name="effectiveMargins">Two caller-owned cells receiving contained margin extents.</param>
    /// <param name="minimumFirstExtent">Receives the smallest jointly feasible leading border-box extent.</param>
    /// <param name="maximumFirstExtent">Receives the largest jointly feasible leading border-box extent.</param>
    internal static void Resolve(
        Length firstLength,
        int firstAutomatic,
        int secondAutomatic,
        int firstMinimum,
        int firstMaximum,
        int secondMinimum,
        int secondMaximum,
        int firstMargin,
        int secondMargin,
        int? available,
        int? percentBase,
        Span<int> extents,
        Span<int> effectiveMargins,
        out int minimumFirstExtent,
        out int maximumFirstExtent)
    {
        Debug.Assert(firstLength.Kind is LengthKind.Cells or LengthKind.Percent);
        Debug.Assert(firstAutomatic >= 0 && secondAutomatic >= 0);
        Debug.Assert(firstMinimum >= 0 && firstMaximum >= firstMinimum);
        Debug.Assert(secondMinimum >= 0 && secondMaximum >= secondMinimum);
        Debug.Assert(firstMargin >= 0 && secondMargin >= 0);
        Debug.Assert(available is null or >= 0);
        Debug.Assert(percentBase is null or >= 0);
        Debug.Assert(extents.Length == 2);
        Debug.Assert(effectiveMargins.Length == 2);

        Span<Length> lengths = [firstLength, Length.Star(1)];
        Span<int> automatic = [firstAutomatic, secondAutomatic];
        Span<int> minimum = [firstMinimum, secondMinimum];
        Span<int> maximum = [firstMaximum, secondMaximum];

        if (available is not { } finiteAvailable)
        {
            effectiveMargins[0] = firstMargin;
            effectiveMargins[1] = secondMargin;
            Tracks.Resolve(null, lengths, automatic, minimum, maximum, extents, percentBase);
            minimumFirstExtent = extents[0];
            maximumFirstExtent = extents[0];
            return;
        }

        effectiveMargins[0] = Math.Min(firstMargin, finiteAvailable);
        var marginRemainder = finiteAvailable - effectiveMargins[0];
        effectiveMargins[1] = Math.Min(secondMargin, marginRemainder);
        var trackAvailable = marginRemainder - effectiveMargins[1];

        Tracks.Resolve(
            trackAvailable,
            lengths,
            automatic,
            minimum,
            maximum,
            extents,
            percentBase ?? finiteAvailable);

        var lower = Math.Max(firstMinimum, Math.Max(0L, (long) trackAvailable - secondMaximum));
        var upper = Math.Min(firstMaximum, Math.Max(0L, (long) trackAvailable - secondMinimum));
        lower = Math.Min(lower, trackAvailable);
        upper = Math.Min(upper, trackAvailable);

        if (lower > upper)
        {
            minimumFirstExtent = extents[0];
            maximumFirstExtent = extents[0];
            return;
        }

        minimumFirstExtent = (int) lower;
        maximumFirstExtent = (int) upper;
    }
}
