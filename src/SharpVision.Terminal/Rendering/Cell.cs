namespace SharpVision.Terminal.Rendering;

/// <summary>
/// Stores one frame-owned lead, continuation, or blank semantic cell.
/// </summary>
internal struct Cell
{
    /// <summary>Gets or sets the UTF-8 arena offset for a lead cell.</summary>
    internal int Offset { readonly get; set; }

    /// <summary>Gets or sets the UTF-8 byte length for a lead cell.</summary>
    internal int Length { readonly get; set; }

    /// <summary>Gets or sets the semantic grapheme hash.</summary>
    internal uint Hash { readonly get; set; }

    /// <summary>Gets or sets the occupied cell width for a lead cell.</summary>
    internal byte Width { readonly get; set; }

    /// <summary>Gets or sets the absolute lead index, or -1 for non-continuations.</summary>
    internal int LeadIndex { readonly get; set; }

    /// <summary>Gets or sets the semantic style.</summary>
    internal Style Style { readonly get; set; }

    /// <summary>Gets whether this cell continues a preceding lead.</summary>
    internal readonly bool IsContinuation => LeadIndex >= 0;

    /// <summary>Creates a blank one-cell value.</summary>
    /// <param name="style">The blank background style.</param>
    /// <returns>The blank cell.</returns>
    internal static Cell Blank(Style style) => new()
    {
        Width = 1,
        LeadIndex = -1,
        Style = style,
    };

    /// <summary>Creates a continuation referring to an absolute lead index.</summary>
    /// <param name="leadIndex">The non-negative lead index.</param>
    /// <param name="style">The lead's semantic style.</param>
    /// <returns>The continuation cell.</returns>
    internal static Cell Continuation(int leadIndex, Style style) => new()
    {
        LeadIndex = leadIndex,
        Style = style,
    };
}
