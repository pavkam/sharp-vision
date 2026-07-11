namespace SharpVision.Terminal.Geometry;

/// <summary>
/// Represents a signed zero-based cell or pixel coordinate.
/// </summary>
/// <param name="X">The horizontal coordinate.</param>
/// <param name="Y">The vertical coordinate.</param>
public readonly record struct Point(int X, int Y);
