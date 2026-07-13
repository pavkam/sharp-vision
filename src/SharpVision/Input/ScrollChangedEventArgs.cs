// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Scrolling;
using SharpVision.Terminal.Geometry;

/// <summary>Reports one committed two-axis ScrollView geometry or offset transition.</summary>
public sealed class ScrollChangedEventArgs: EventArgs
{
    /// <summary>Initializes immutable old/new offsets, geometry, and cause.</summary>
    /// <param name="previousOffset">The non-negative offset before the transition.</param>
    /// <param name="offset">The committed non-negative offset.</param>
    /// <param name="extent">The committed content extent.</param>
    /// <param name="viewport">The committed visible extent.</param>
    /// <param name="cause">The defined transition path.</param>
    /// <exception cref="ArgumentOutOfRangeException">A point coordinate or cause is invalid.</exception>
    public ScrollChangedEventArgs(
        Point previousOffset,
        Point offset,
        Size extent,
        Size viewport,
        Cause cause)
    {
        Validate(previousOffset, nameof(previousOffset));
        Validate(offset, nameof(offset));

        if (!Enum.IsDefined(cause))
        {
            throw new ArgumentOutOfRangeException(nameof(cause), cause, "The scroll cause is unknown.");
        }

        PreviousOffset = previousOffset;
        Offset = offset;
        Extent = extent;
        Viewport = viewport;
        Cause = cause;
    }

    /// <summary>Gets the offset before the transition.</summary>
    public Point PreviousOffset { get; }

    /// <summary>Gets the committed offset.</summary>
    public Point Offset { get; }

    /// <summary>Gets the committed content extent.</summary>
    public Size Extent { get; }

    /// <summary>Gets the committed visible extent.</summary>
    public Size Viewport { get; }

    /// <summary>Gets the transition path.</summary>
    public Cause Cause { get; }

    private static void Validate(Point value, string name)
    {
        if (value.X < 0 || value.Y < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Scroll offsets cannot be negative.");
        }
    }
}
