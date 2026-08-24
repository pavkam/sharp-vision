// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

/// <summary>
/// Owns an immutable semantic-text projection and the visible cell geometry of its graphemes.
/// </summary>
[PublicAPI]
public sealed class SelectableTextSnapshot
{
    /// <summary>Initializes an independently owned selectable-text projection.</summary>
    /// <param name="text">
    /// The complete semantic UTF-16 text, including content outside the visible viewport.
    /// </param>
    /// <param name="glyphs">
    /// The non-null visible grapheme projections in source-control local cell coordinates.
    /// </param>
    /// <param name="isAuthoritative">
    /// Whether <paramref name="text"/> is the authoritative semantic content rather than a
    /// presentation fallback.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="text"/>, <paramref name="glyphs"/>, or a glyph entry is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A glyph range endpoint exceeds <paramref name="text"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A glyph range does not identify exactly one extended grapheme cluster.
    /// </exception>
    public SelectableTextSnapshot(
        string text,
        IReadOnlyList<SelectableTextGlyph> glyphs,
        bool isAuthoritative)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(glyphs);

        var owned = glyphs.ToArray();

        if (owned.Length == 0)
        {
            Text = text;
            Glyphs = Array.AsReadOnly(owned);
            IsAuthoritative = isAuthoritative;
            return;
        }

        var requestedRanges = new HashSet<(int Start, int End)>();

        foreach (var glyph in owned)
        {
            if (glyph is null)
            {
                throw new ArgumentNullException(
                    nameof(glyphs),
                    "Selectable-text glyph collections cannot contain null entries.");
            }

            if (glyph.Range.End > text.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(glyphs),
                    glyph.Range,
                    "Selectable-text glyph range endpoints must not exceed the semantic text length.");
            }

            _ = requestedRanges.Add((glyph.Range.Start, glyph.Range.End));
        }

        foreach (var grapheme in Graphemes.Enumerate(text))
        {
            _ = requestedRanges.Remove((grapheme.Offset, grapheme.Offset + grapheme.Length));
        }

        if (requestedRanges.Count != 0)
        {
            throw new ArgumentException(
                "Each selectable-text glyph range must identify exactly one complete grapheme.",
                nameof(glyphs));
        }

        Text = text;
        Glyphs = Array.AsReadOnly(owned);
        IsAuthoritative = isAuthoritative;
    }

    /// <summary>Gets the complete semantic UTF-16 text, including clipped content.</summary>
    public string Text { get; }

    /// <summary>Gets the immutable, owned, non-null visible grapheme geometry.</summary>
    public IReadOnlyList<SelectableTextGlyph> Glyphs { get; }

    /// <summary>Gets whether the semantic text is authoritative rather than a presentation fallback.</summary>
    public bool IsAuthoritative { get; }
}
