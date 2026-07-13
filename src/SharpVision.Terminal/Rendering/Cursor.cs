namespace SharpVision.Terminal.Rendering;

using SharpVision.Terminal.Geometry;

/// <summary>Represents the desired terminal cursor position and visibility after a frame.</summary>
public readonly record struct Cursor
{
    /// <summary>Initializes a desired terminal cursor state.</summary>
    /// <param name="position">The zero-based cell coordinate.</param>
    /// <param name="visible">Whether the terminal cursor is visible.</param>
    public Cursor(Point position, bool visible)
    {
        Position = position;
        Visible = visible;
    }

    /// <summary>Gets the zero-based cell coordinate.</summary>
    public Point Position { get; }

    /// <summary>Gets whether the terminal cursor is visible.</summary>
    public bool Visible { get; }

    /// <summary>Deconstructs the cursor state.</summary>
    public void Deconstruct(out Point position, out bool visible)
    {
        position = Position;
        visible = Visible;
    }
}
