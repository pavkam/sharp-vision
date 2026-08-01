// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics.Backends;

using Rendering;

/// <summary>Shares placement-overlap analysis common to the direct and non-retained graphics backends.</summary>
internal static class GraphicsBackendSupport
{
    /// <summary>Finds placements whose ordinary-cell fallback must replace an image render.</summary>
    /// <param name="frame">The source frame whose placements are being analyzed.</param>
    /// <param name="encodable">Whether each placement, by index, can be protocol-encoded.</param>
    /// <returns>Whether each placement, by index, must fall back to an ordinary-cell render.</returns>
    public static bool[] FindFallbackBlockedPlacements(Frame frame, ReadOnlySpan<bool> encodable)
    {
        var blocked = new bool[frame.PlacementCount];

        // Paint order is a finite DAG: a lower placement depends on every overlapping later
        // placement being replayable. Walking backwards propagates an ordinary-cell fallback
        // through all lower transitive overlaps without attempting unsafe image clipping.
        for (var lower = frame.PlacementCount - 1; lower >= 0; lower--)
        {
            if (!encodable[lower])
            {
                continue;
            }

            var lowerBounds = frame.GetPlacement(lower).Destination;

            for (var upper = lower + 1; upper < frame.PlacementCount; upper++)
            {
                if (Overlaps(lowerBounds, frame.GetPlacement(upper).Destination) &&
                    (!encodable[upper] || blocked[upper]))
                {
                    blocked[lower] = true;
                    break;
                }
            }
        }

        return blocked;
    }

    /// <summary>Reports whether two rectangles share any pixel area.</summary>
    /// <param name="first">The first rectangle.</param>
    /// <param name="second">The second rectangle.</param>
    /// <returns>Whether the rectangles intersect.</returns>
    public static bool Overlaps(Rect first, Rect second)
    {
        var intersection = first.Intersect(second);
        return intersection.Width != 0 && intersection.Height != 0;
    }
}
