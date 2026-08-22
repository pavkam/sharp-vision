// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Defines the complete immutable document bullet, quote-bar, and rule glyph family. Each
/// member is the theme-customizable primary glyph only - the portable ASCII repair value is
/// permanently code-owned.</summary>
/// <remarks>
/// Several defaults here - the top-level bullet, the quote bar, and the rule - are East Asian
/// Ambiguous characters, which a terminal configured for wide ambiguous width renders as two cells.
/// The document resolves every glyph against the live cell policy before drawing and substitutes the
/// code-owned repair value whenever the primary would not fit one cell, so a CJK-configured terminal
/// degrades to ASCII rather than corrupting the layout. A glyph that already measures one cell under
/// the active policy is drawn as authored; the repair is a fallback, not a downgrade.
/// </remarks>
[PublicAPI]
public readonly record struct DocumentGlyphs: IAppearanceFragment
{
    private Rune FirstBulletValue { get; init; }
    private Rune QuoteBarValue { get; init; }
    private Rune RuleValue { get; init; }
    private Rune SecondBulletValue { get; init; }
    private Rune ThirdBulletValue { get; init; }

    /// <summary>Initializes the complete document glyph family.</summary>
    /// <param name="firstBullet">The bullet marking a top-level list item.</param>
    /// <param name="secondBullet">The bullet marking a once-nested list item.</param>
    /// <param name="thirdBullet">The bullet marking a twice-nested list item.</param>
    /// <param name="quoteBar">The vertical bar drawn down a block quote's left edge.</param>
    /// <param name="rule">The horizontal rule drawn for a thematic break.</param>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    public DocumentGlyphs(Rune firstBullet, Rune secondBullet, Rune thirdBullet, Rune quoteBar, Rune rule)
    {
        // Validated here as well as in each init accessor, and deliberately so: an accessor cannot
        // know which constructor argument it came from, so its ArgumentException names "value",
        // which for a family of five same-typed glyphs identifies nothing.
        FirstBullet = firstBullet.ValidateSingleCell(nameof(firstBullet));
        SecondBullet = secondBullet.ValidateSingleCell(nameof(secondBullet));
        ThirdBullet = thirdBullet.ValidateSingleCell(nameof(thirdBullet));
        QuoteBar = quoteBar.ValidateSingleCell(nameof(quoteBar));
        Rule = rule.ValidateSingleCell(nameof(rule));
    }

    /// <summary>Gets the established code-owned document glyph family.</summary>
    public static DocumentGlyphs Default { get; } = new(
        new Rune('\u2022'),
        new Rune('\u25E6'),
        new Rune('\u25AA'),
        new Rune('\u2502'),
        new Rune('\u2500'));

    /// <summary>Gets the bullet marking a top-level list item.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune FirstBullet
    {
        get => FirstBulletValue.Value == 0 ? new Rune('\u2022') : FirstBulletValue;
        init => FirstBulletValue = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the bullet marking a once-nested list item.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune SecondBullet
    {
        get => SecondBulletValue.Value == 0 ? new Rune('\u25E6') : SecondBulletValue;
        init => SecondBulletValue = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the bullet marking a twice-nested list item. Deeper nesting rotates back to
    /// <see cref="FirstBullet"/>.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune ThirdBullet
    {
        get => ThirdBulletValue.Value == 0 ? new Rune('\u25AA') : ThirdBulletValue;
        init => ThirdBulletValue = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the vertical bar drawn down a block quote's left edge.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune QuoteBar
    {
        get => QuoteBarValue.Value == 0 ? new Rune('\u2502') : QuoteBarValue;
        init => QuoteBarValue = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the horizontal rule drawn for a thematic break.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Rule
    {
        get => RuleValue.Value == 0 ? new Rune('\u2500') : RuleValue;
        init => RuleValue = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the code-owned top-level bullet glyph and repair value.</summary>
    internal ControlGlyph FirstBulletGlyph => new(FirstBullet, new Rune('*'));

    /// <summary>Gets the code-owned once-nested bullet glyph and repair value.</summary>
    internal ControlGlyph SecondBulletGlyph => new(SecondBullet, new Rune('o'));

    /// <summary>Gets the code-owned twice-nested bullet glyph and repair value.</summary>
    internal ControlGlyph ThirdBulletGlyph => new(ThirdBullet, new Rune('+'));

    /// <summary>Gets the code-owned quote-bar glyph and repair value.</summary>
    internal ControlGlyph QuoteBarGlyph => new(QuoteBar, new Rune('|'));

    /// <summary>Gets the code-owned thematic-break glyph and repair value.</summary>
    internal ControlGlyph RuleGlyph => new(Rule, new Rune('-'));

    IAppearanceFragment IAppearanceFragment.Clone() => this with { };
}
