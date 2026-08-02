// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics.Backends;

using Rendering;

/// <summary>Shares placement-overlap analysis common to the direct and non-retained graphics backends.</summary>
internal static class GraphicsBackendSupport
{
    extension(Frame frame)
    {
        /// <summary>Finds placements whose ordinary-cell fallback must replace an image render.</summary>
        /// <param name="encodable">Whether each placement, by index, can be protocol-encoded.</param>
        /// <returns>Whether each placement, by index, must fall back to an ordinary-cell render.</returns>
        public bool[] FindFallbackBlockedPlacements(ReadOnlySpan<bool> encodable)
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
                    if (lowerBounds.Overlaps(frame.GetPlacement(upper).Destination) &&
                        (!encodable[upper] || blocked[upper]))
                    {
                        blocked[lower] = true;
                        break;
                    }
                }
            }

            return blocked;
        }
    }

    extension(Rect first)
    {
        /// <summary>Reports whether two rectangles share any pixel area.</summary>
        /// <param name="second">The second rectangle.</param>
        /// <returns>Whether the rectangles intersect.</returns>
        public bool Overlaps(Rect second)
        {
            var intersection = first.Intersect(second);
            return intersection.Width != 0 && intersection.Height != 0;
        }
    }
}
