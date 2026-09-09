// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Represents one pointer position update reported by an active drag, carrying the
/// gesture's starting cell alongside the cell the reporting event carried.</summary>
[PublicAPI]
public readonly record struct DragMove
{
    /// <summary>Initializes a reported drag position update.</summary>
    /// <param name="start">The cell the drag started at.</param>
    /// <param name="current">The cell the reporting pointer event carried.</param>
    public DragMove(Point start, Point current)
    {
        Start = start;
        Current = current;
    }

    /// <summary>Gets the cell the drag started at.</summary>
    public Point Start { get; }

    /// <summary>Gets the cell the reporting pointer event carried.</summary>
    public Point Current { get; }

    /// <summary>Gets the saturating horizontal offset from <see cref="Start"/> to
    /// <see cref="Current"/>.</summary>
    public int DeltaX => Current.X.SaturatingSubtract(Start.X);

    /// <summary>Gets the saturating vertical offset from <see cref="Start"/> to
    /// <see cref="Current"/>.</summary>
    public int DeltaY => Current.Y.SaturatingSubtract(Start.Y);
}
