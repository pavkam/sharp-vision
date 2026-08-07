// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable radio-button presentation. This style's own
/// "radioButton" theme key falls back to <see cref="InputStyle"/>'s "input" key, chromeless
/// (a radio button is a selectable control, not a framed one), with the checked state defaulting
/// to the semantic accent foreground.</summary>
[PublicAPI]
public sealed record RadioButtonStyle: InputStyle
{
    // The glyphs that go with the three-cell Parentheses layout. Declared before every static
    // property initializer below, because Parentheses is itself completed through Complete and
    // would otherwise read this while still unset. Distinct from RadioButtonGlyphs.Default
    // ('○','◉'), which is the ONE-cell family belonging to the Circle layout - pairing that family
    // with Parentheses renders a circle inside parentheses ("(○)"), which is what an earlier
    // rewrite accidentally did here.
    private static readonly RadioButtonGlyphs _parenthesesGlyphs = new(new Rune(' '), new Rune('•'));

    /// <summary>Gets the primary radio-button-style definition. A theme's own "radioButton" key
    /// can restyle MarkStyle, Glyphs, or anything else directly - the standalone registrable
    /// "radioButton" style section this used to read separately is retired; the reflective
    /// Overlay reaches every member of this type uniformly.</summary>
    internal static StyleDefinition<RadioButtonStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(InputStyle.Default),
        Complete,
        Compare);

    // The checked state defaults to the semantic accent foreground unless a theme's own
    // "radioButton.checked" section overrides it - the one per-state code-owned default this
    // type needs, which BuildFallbackAwareStates's VisualState-aware `complete` exists for.
    private static RadioButtonStyle Complete(InputStyle input, VisualState state)
    {
        var face = state == VisualState.Checked ? input.Face with { Foreground = SemanticColor.Accent } : input.Face;
        return new RadioButtonStyle(face, NoBorder, NoShadow, RadioButtonMarkStyle.Parentheses, _parenthesesGlyphs)
        {
            // Forwarded from the fallback rather than left at the code-owned value the base
            // constructor supplies. Without this the member is inherited into this style's own
            // theme key, accepted by the parser, and silently ignored - the exact
            // blessed-but-unread section themes.md says the design exists to prevent.
            DropDownGlyph = input.DropDownGlyph
        };
    }

    private static InvalidationImpact Compare(
        RadioButtonStyle previous,
        Theme? previousTheme,
        RadioButtonStyle current,
        Theme? currentTheme) =>
        previous.MarkWidth != current.MarkWidth
            ? InvalidationImpact.Measure
            : previous.MarkStyle != current.MarkStyle || previous.Glyphs != current.Glyphs
                ? InvalidationImpact.Render
                : InvalidationImpact.None;

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
    public static RadioButtonStyle Parentheses { get; } = Complete(InputStyle.Default, VisualState.Normal);

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
            value.ValidateDefined();
            field = value;
        }
    }

    /// <summary>Gets the complete unchecked and checked glyph pair.</summary>
    public required RadioButtonGlyphs Glyphs { get; init; }

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
