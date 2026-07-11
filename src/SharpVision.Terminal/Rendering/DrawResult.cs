using SharpVision.Terminal.Geometry;

namespace SharpVision.Terminal.Rendering;

/// <summary>
/// Reports observable results of one canvas draw operation.
/// </summary>
/// <param name="Final">The next logical cell position.</param>
/// <param name="Graphemes">The number of input grapheme clusters processed.</param>
/// <param name="Cells">The number of logical printable cells advanced.</param>
/// <param name="Clipped">The number of complete clusters not drawn.</param>
/// <param name="Replaced">The number of edge clusters replaced with U+FFFD.</param>
public readonly record struct DrawResult(
    Point Final,
    int Graphemes,
    int Cells,
    int Clipped,
    int Replaced);
