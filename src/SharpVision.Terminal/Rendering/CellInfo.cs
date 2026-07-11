using SharpVision.Terminal.Geometry;

namespace SharpVision.Terminal.Rendering;

/// <summary>
/// Exposes non-owning semantic cell metadata without pooled grapheme memory.
/// </summary>
/// <param name="Style">The semantic cell style.</param>
/// <param name="Width">The lead width, one for blank, or zero for continuation.</param>
/// <param name="IsContinuation">Whether the cell refers to a preceding lead.</param>
/// <param name="Lead">The lead coordinate when this is a continuation.</param>
public readonly record struct CellInfo(
    Style Style,
    int Width,
    bool IsContinuation,
    Point Lead)
{
    /// <summary>Gets the default blank semantic cell.</summary>
    public static CellInfo Blank { get; } = new(Style.Default, 1, false, default);
}
