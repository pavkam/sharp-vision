using SharpVision.Terminal.Geometry;

namespace SharpVision.Terminal.Rendering;

/// <summary>
/// Represents the desired terminal cursor position and visibility after a frame.
/// </summary>
/// <param name="Position">The zero-based cell coordinate.</param>
/// <param name="Visible">Whether the terminal cursor is visible.</param>
public readonly record struct Cursor(Point Position, bool Visible);
