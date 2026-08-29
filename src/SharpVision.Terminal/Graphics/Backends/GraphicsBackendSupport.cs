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
        [Pure]
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

        /// <summary>
        /// Appends a diagnostic for every placement that was encodable on its own but was forced to
        /// fall back solely because a later, overlapping placement could not be encoded.
        /// </summary>
        /// <param name="encodable">Whether each placement, by index, can be protocol-encoded.</param>
        /// <param name="blocked">Whether each placement, by index, must fall back to an ordinary-cell render.</param>
        /// <param name="skippedPlacements">The skipped-placement diagnostics collected so far, lazily allocated.</param>
        /// <returns>The skipped-placement diagnostics, including any newly appended entries.</returns>
        public List<GraphicsPlacementDiagnostic>? AppendOverlapBlockedDiagnostics(
            ReadOnlySpan<bool> encodable,
            ReadOnlySpan<bool> blocked,
            List<GraphicsPlacementDiagnostic>? skippedPlacements)
        {
            for (var index = 0; index < frame.PlacementCount; index++)
            {
                if (encodable[index] && blocked[index])
                {
                    (skippedPlacements ??= []).Add(new GraphicsPlacementDiagnostic(
                        frame.GetPlacement(index).ImageIdentity,
                        GraphicsPlacementSkipReason.OverlapBlocked));
                }
            }

            return skippedPlacements;
        }
    }

    extension(Rect first)
    {
        /// <summary>Reports whether two rectangles share any pixel area.</summary>
        /// <param name="second">The second rectangle.</param>
        /// <returns>Whether the rectangles intersect.</returns>
        [Pure]
        public bool Overlaps(Rect second)
        {
            var intersection = first.Intersect(second);
            return intersection.Width != 0 && intersection.Height != 0;
        }
    }

    /// <summary>Throws when a backend field marks the owning instance disposed.</summary>
    /// <param name="disposed">The owning backend's disposed field.</param>
    /// <param name="backend">The owning backend, reported as the disposed type on failure.</param>
    /// <exception cref="ObjectDisposedException"><paramref name="disposed"/> is true.</exception>
    public static void ThrowIfDisposed(bool disposed, object backend) =>
        ObjectDisposedException.ThrowIf(disposed, backend);

    /// <summary>Throws when a backend is disposed or has no prepared transaction.</summary>
    /// <param name="disposed">The owning backend's disposed field.</param>
    /// <param name="backend">The owning backend, reported as the disposed type on failure.</param>
    /// <param name="prepared">The owning backend's prepared field.</param>
    /// <param name="message">The backend-specific message for a missing prepared transaction.</param>
    /// <exception cref="ObjectDisposedException"><paramref name="disposed"/> is true.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="prepared"/> is false.</exception>
    public static void EnsurePrepared(bool disposed, object backend, bool prepared, string message)
    {
        ThrowIfDisposed(disposed, backend);

        if (!prepared)
        {
            throw new InvalidOperationException(message);
        }
    }
}
