// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Diagnostics.CodeAnalysis;

using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>Defines one complete immutable radio-button presentation. This style declares no
/// theme section of its own: it falls back to <see cref="InputStyle"/>'s "input" role section,
/// chromeless (a radio button is a selectable control, not a framed one), with the checked state
/// defaulting to the semantic accent foreground, resolves its own mark style and glyph pair from
/// <see cref="Theme.Glyphs"/>, owns validated mark placement and gap, and is themeable only through
/// that fallback and a locally assigned <see cref="RadioButton.Style"/>.</summary>
[PublicAPI]
public sealed record RadioButtonStyle: InputStyle
{
    /// <summary>Gets the primary radio-button-style definition. Falls back to
    /// <see cref="InputStyle"/>'s "input" role section, chromeless; MarkStyle, Glyphs, MarkGap,
    /// and MarkPlacement are code-owned.</summary>
    internal static StyleDefinition<RadioButtonStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(InputStyle.Default),
        Complete,
        static (previous, _, current, _) =>
            previous.MarkWidth != current.MarkWidth ||
            previous.MarkGap != current.MarkGap ||
            previous.AffixGap != current.AffixGap
                ? InvalidationImpact.Measure
                : previous.MarkPlacement != current.MarkPlacement
                    ? InvalidationImpact.Arrange
                : previous.MarkStyle != current.MarkStyle || previous.Glyphs != current.Glyphs
                    ? InvalidationImpact.Render
                    : InvalidationImpact.None);

    // The checked state defaults to the semantic accent foreground unless a theme's own
    // "radioButton.checked" section overrides it - the one per-state code-owned default this
    // type needs, which BuildFallbackAwareStates's VisualState-aware `complete` exists for. The
    // mark style and glyph pair come from the active theme's glyph family (see
    // themes.md#glyph-families) rather than a literal hardcoded here - GlyphFamily.Default carries
    // the exact three-cell Parentheses layout this style used to hardcode directly, so an
    // unthemed resolution is unchanged. Distinct from RadioButtonGlyphs.Default ('○','◉'), which
    // is the ONE-cell family belonging to the Circle layout - pairing that family with
    // Parentheses renders a circle inside parentheses ("(○)"), which is what an earlier rewrite
    // accidentally did here.
    private static RadioButtonStyle Complete(InputStyle input, VisualState state, Theme theme)
    {
        var face = state == VisualState.Checked ? input.Face with { Foreground = SemanticColor.Accent } : input.Face;
        return new RadioButtonStyle(face, NoBorder, NoShadow, theme.Glyphs.RadioButton.MarkStyle, theme.Glyphs.RadioButton.Glyphs)
        {
            // Forwarded from the fallback rather than left at the code-owned value the base
            // constructor supplies. Without this, DropDownGlyph would stay stuck at
            // InputStyle.Default's literal regardless of how a theme customizes "input"'s own
            // dropDownGlyph - a divergence RadioButton would never surface visually, since it
            // never draws a dropdown glyph itself.
            DropDownGlyph = input.DropDownGlyph,
            AffixGap = input.AffixGap
        };
    }

    /// <summary>Initializes a complete radio-button presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="markStyle">The validated mark-layout family.</param>
    /// <param name="glyphs">The complete unchecked and checked glyph pair.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="markStyle"/> is undefined.</exception>
    [SetsRequiredMembers]
    public RadioButtonStyle(Face face, Border border, Shadow shadow, RadioButtonMarkStyle markStyle, RadioButtonGlyphs glyphs) : base(face, border, shadow, InputStyle.Default.DropDownGlyph)
    {
        MarkStyle = markStyle;
        Glyphs = glyphs;
    }

    /// <summary>Gets the standard parenthesized presentation.</summary>
    public static new RadioButtonStyle Default => Parentheses;

    /// <summary>Gets the exact three-cell <c>( )</c> and <c>(•)</c> presentation.</summary>
    public static RadioButtonStyle Parentheses { get; } = Complete(InputStyle.Default, VisualState.Normal, Theme.Unthemed);

    /// <summary>Gets the compact one-cell circle-glyph presentation.</summary>
    public static RadioButtonStyle Glyph { get; } = Parentheses with
    {
        MarkStyle = RadioButtonMarkStyle.Circle,
        Glyphs = RadioButtonGlyphs.Default
    };

    /// <summary>Gets the mark-layout family.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is undefined.</exception>
    public required RadioButtonMarkStyle MarkStyle
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value);
            field = value;
        }
    }

    /// <summary>Gets the complete unchecked and checked glyph pair.</summary>
    public required RadioButtonGlyphs Glyphs { get; init; }

    /// <summary>Gets the horizontal terminal-cell gap between the mark and a present caption.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is outside 0-4.</exception>
    [ValueRange(0, 4)]
    public int MarkGap
    {
        get;
        init => field = value is >= 0 and <= 4
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "The mark gap must be between 0 and 4 cells.");
    } = 1;

    /// <summary>Gets which horizontal caption edge owns the radio mark.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is undefined.</exception>
    public SelectionMarkPlacement MarkPlacement
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value);
            field = value;
        }
    } = SelectionMarkPlacement.Leading;

    /// <summary>Gets the fully formatted unchecked mark.</summary>
    public string UncheckedText => Format(Glyphs.Unchecked);

    /// <summary>Gets the fully formatted checked mark.</summary>
    public string CheckedText => Format(Glyphs.Checked);

    /// <summary>Gets the horizontal terminal-cell reservation for the mark.</summary>
    public int MarkWidth => MarkStyle == RadioButtonMarkStyle.Parentheses ? 3 : 1;

    private string Format(Rune glyph) => MarkStyle == RadioButtonMarkStyle.Parentheses
        ? string.Concat("(", glyph.ToString(), ")")
        : glyph.ToString();
}
