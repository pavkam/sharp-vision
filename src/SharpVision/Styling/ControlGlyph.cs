// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines one preferred control glyph and its one-cell repair value.</summary>
[PublicAPI]
public readonly record struct ControlGlyph
{
    /// <summary>Initializes a printable one-cell primary glyph and fallback.</summary>
    /// <param name="value">The preferred printable one-cell glyph.</param>
    /// <param name="fallback">The portable printable one-cell repair glyph.</param>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    public ControlGlyph(Rune value, Rune fallback)
    {
        Value = value.ValidateSingleCell(nameof(value));
        Fallback = fallback.ValidateSingleCell(nameof(fallback));
    }

    /// <summary>Gets the preferred Unicode scalar.</summary>
    public Rune Value { get; }

    /// <summary>Gets the portable repair scalar.</summary>
    public Rune Fallback { get; }

    /// <summary>Applies the single glyph-repair rule: the preferred glyph when the live cell policy
    /// can render it as one cell, otherwise the portable fallback.</summary>
    /// <param name="ambiguousWidth">The live East Asian Ambiguous width policy to resolve against.</param>
    /// <returns><see cref="Value"/> when it measures one printable cell under
    /// <paramref name="ambiguousWidth"/>; otherwise <see cref="Fallback"/>.</returns>
    [Pure]
    public Rune Resolve(Ambiguous ambiguousWidth) => Value.Resolve(Fallback, ambiguousWidth);
}
