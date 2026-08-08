// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>Represents the desired terminal cursor position, visibility, and semantic shape after a frame.</summary>
[DebuggerDisplay("{ToString()}")]
[PublicAPI]
public readonly record struct Cursor
{
    /// <summary>Initializes a desired terminal cursor state.</summary>
    /// <param name="position">The zero-based cell coordinate.</param>
    /// <param name="visible">Whether the terminal cursor is visible.</param>
    public Cursor(Point position, bool visible) : this(position, visible, CursorShape.Block)
    {
    }

    /// <summary>Initializes a desired terminal cursor state.</summary>
    /// <param name="position">The zero-based cell coordinate.</param>
    /// <param name="visible">Whether the terminal cursor is visible.</param>
    /// <param name="shape">The validated semantic cursor shape.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="shape"/> is unknown.</exception>
    public Cursor(Point position, bool visible, CursorShape shape)
    {
        shape.ValidateDefined(nameof(shape), "The cursor shape is unknown.");

        Position = position;
        Visible = visible;
        Shape = shape;
    }

    /// <summary>Gets the zero-based cell coordinate.</summary>
    public Point Position { get; }

    /// <summary>Gets whether the terminal cursor is visible.</summary>
    public bool Visible { get; }

    /// <summary>Gets the semantic cursor shape.</summary>
    public CursorShape Shape { get; }

    /// <summary>Deconstructs the cursor state.</summary>
    public void Deconstruct(out Point position, out bool visible)
    {
        position = Position;
        visible = Visible;
    }

    /// <summary>Deconstructs the complete cursor state.</summary>
    public void Deconstruct(out Point position, out bool visible, out CursorShape shape)
    {
        position = Position;
        visible = Visible;
        shape = Shape;
    }

    /// <inheritdoc/>
    public override string ToString() => $"Cursor {{ Position={Position}, Visible={Visible}, Shape={Shape} }}";
}
