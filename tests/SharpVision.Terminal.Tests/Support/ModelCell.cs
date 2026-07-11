using SharpVision.Terminal.Rendering;

namespace SharpVision.Terminal.Tests.Support;

/// <summary>Stores one independently modeled terminal cell.</summary>
/// <param name="Text">The complete grapheme text.</param>
/// <param name="Style">The semantic cell style.</param>
/// <param name="Width">The grapheme cell width.</param>
/// <param name="IsContinuation">Whether the cell continues a wide grapheme.</param>
/// <param name="LeadX">The owning lead column.</param>
internal readonly record struct ModelCell(
    string Text,
    Style Style,
    int Width,
    bool IsContinuation,
    int LeadX)
{
    /// <summary>Gets the default blank modeled cell.</summary>
    internal static ModelCell Blank { get; } = new(" ", Style.Default, 1, false, 0);
}
