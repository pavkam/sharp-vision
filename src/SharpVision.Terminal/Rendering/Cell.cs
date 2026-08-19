// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>
/// Stores one frame-owned lead, continuation, or blank semantic cell.
/// </summary>
internal struct Cell
{
    /// <summary>Gets or sets the UTF-8 arena offset for a lead cell.</summary>
    public int Offset { readonly get; set; }

    /// <summary>Gets or sets the UTF-8 byte length for a lead cell.</summary>
    public int Length { readonly get; set; }

    /// <summary>Gets or sets the semantic grapheme hash.</summary>
    public uint Hash { readonly get; set; }

    /// <summary>Gets or sets the occupied cell width for a lead cell.</summary>
    public byte Width { readonly get; set; }

    /// <summary>Gets or sets the absolute lead index, or -1 for non-continuations.</summary>
    public int LeadIndex { readonly get; set; }

    /// <summary>Gets or sets the semantic style.</summary>
    public CellStyle Style { readonly get; set; }

    /// <summary>Gets or sets the frame-local revision of the latest semantic mutation.</summary>
    /// <remarks>This provenance metadata is excluded from semantic equality and terminal damage.</remarks>
    public ulong MutationRevision { readonly get; set; }

    /// <summary>Gets whether this cell continues a preceding lead.</summary>
    public readonly bool IsContinuation => LeadIndex >= 0;

    /// <summary>Creates a blank one-cell value.</summary>
    /// <param name="style">The blank background style.</param>
    /// <param name="mutationRevision">The frame-local semantic mutation revision.</param>
    /// <returns>The blank cell.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Cell Blank(CellStyle style, ulong mutationRevision) => new()
    {
        Width = 1,
        LeadIndex = -1,
        Style = style,
        MutationRevision = mutationRevision
    };

    /// <summary>Creates a continuation referring to an absolute lead index.</summary>
    /// <param name="leadIndex">The non-negative lead index.</param>
    /// <param name="style">The lead's semantic style.</param>
    /// <param name="mutationRevision">The frame-local semantic mutation revision.</param>
    /// <returns>The continuation cell.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Cell Continuation(int leadIndex, CellStyle style, ulong mutationRevision) => new()
    {
        LeadIndex = leadIndex,
        Style = style,
        MutationRevision = mutationRevision
    };
}
