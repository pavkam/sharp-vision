// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Represents one per-instance, edge-pinned decoration reserved as a fixed cell column
/// beside a control's caption.</summary>
/// <remarks>
/// An affix is application content, not theme presentation: it varies by data, is bindable, and
/// is never authored by a theme. This is the same distinction <see cref="ControlGlyph"/> draws for
/// theme-owned chrome, seen from the opposite side - see the Style glyph vs. affix table in
/// docs/concepts/styling.md.
/// </remarks>
[PublicAPI]
public readonly record struct Affix
{
    /// <summary>Initializes an affix with the default <c>"?"</c> portable fallback.</summary>
    /// <param name="content">The printable, single-line, single-grapheme-cluster content.</param>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="content"/> is empty, contains a control cluster, spans more than one
    /// grapheme cluster, or is an unattached combining mark.
    /// </exception>
    public Affix(string content) : this(content, "?")
    {
    }

    /// <summary>Initializes a complete affix.</summary>
    /// <param name="content">The printable, single-line, single-grapheme-cluster content.</param>
    /// <param name="fallback">The printable ASCII repair value drawn when the negotiated terminal
    /// capability cannot render <paramref name="content"/>.</param>
    /// <param name="color">The optional literal-or-semantic foreground; null inherits the row's
    /// already-resolved foreground.</param>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> or
    /// <paramref name="fallback"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="content"/> is empty, contains a control cluster, spans more than one
    /// grapheme cluster, or is an unattached combining mark; or <paramref name="fallback"/> is
    /// empty or contains a non-printable-ASCII character.
    /// </exception>
    public Affix(string content, string fallback, ControlColor? color = null)
    {
        Content = ValidateContent(content);
        Fallback = ValidateFallback(fallback);
        Color = color;
    }

    /// <summary>Gets the printable, single-line, single-grapheme-cluster content, at most two
    /// terminal cells wide.</summary>
    public string Content { get; }

    /// <summary>Gets the printable ASCII repair value drawn when the negotiated terminal
    /// capability cannot render <see cref="Content"/>.</summary>
    public string Fallback { get; }

    /// <summary>Gets the optional literal-or-semantic foreground; null inherits the row's
    /// already-resolved foreground.</summary>
    public ControlColor? Color { get; }

    private static string ValidateContent(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Length == 0)
        {
            throw new ArgumentException("Affix content cannot be empty.", nameof(content));
        }

        var measurement = Width.Measure(content, Ambiguous.Narrow);

        if (measurement.Graphemes != 1)
        {
            throw new ArgumentException(
                "Affix content must be exactly one grapheme cluster.",
                nameof(content));
        }

        if (measurement.Controls != 0)
        {
            throw new ArgumentException(
                "Affix content cannot contain a control cluster.",
                nameof(content));
        }

        // A single grapheme cluster measures Control (0), Narrow (1), or Wide (2) cells; having
        // ruled out Control above, only 1 or 2 cells remain reachable here. The remaining gap this
        // closes is an unattached combining mark: with no preceding base scalar to attach to, the
        // segmenter still yields one cluster and a non-zero, non-control width, so the checks above
        // let it through. CharUnicodeInfo classifies the leading scalar the same way the codebase's
        // own combining-mark handling would, without a second grapheme/width implementation.
        var leadingCategory = Rune.GetUnicodeCategory(DecodeFirstRune(content));

        return leadingCategory is
            UnicodeCategory.NonSpacingMark or
            UnicodeCategory.SpacingCombiningMark or
            UnicodeCategory.EnclosingMark
            ? throw new ArgumentException(
                "Affix content cannot start with an unattached combining mark.",
                nameof(content))
            : content;
    }

    private static string ValidateFallback(string fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);

        if (fallback.Length == 0)
        {
            throw new ArgumentException("Affix fallback cannot be empty.", nameof(fallback));
        }

        foreach (var character in fallback)
        {
            if (character is < ' ' or > '~')
            {
                throw new ArgumentException(
                    "Affix fallback must be printable ASCII.",
                    nameof(fallback));
            }
        }

        return fallback;
    }

    private static Rune DecodeFirstRune(string value)
    {
        _ = Rune.DecodeFromUtf16(value, out var rune, out _);
        return rune;
    }
}
