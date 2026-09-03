// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Diagnostics.CodeAnalysis;

using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>Defines one complete immutable checkbox presentation. This style declares no theme
/// section of its own: it falls back to <see cref="InputStyle"/>'s "input" role section,
/// chromeless (a checkbox is a selectable control, not a framed one), resolves its own mark style
/// and glyph family from <see cref="Theme.Glyphs"/>, owns validated mark placement and gap, and is
/// themeable only through that fallback and a locally assigned <see cref="CheckBox.Style"/>.</summary>
[PublicAPI]
public sealed record CheckBoxStyle: InputStyle
{
    /// <summary>Gets the primary checkbox-style definition. Falls back to <see cref="InputStyle"/>'s
    /// "input" role section, chromeless; MarkStyle, Glyphs, MarkGap, and MarkPlacement are
    /// code-owned.</summary>
    internal static StyleDefinition<CheckBoxStyle> Definition { get; } = StyleDefinitions.Control(
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
                    : InvalidationImpact.None,
        // CheckBox is chromeless (NoBorder) with no other visible focus cue, unlike the six
        // well-known base types whose border color change signals focus on its own - see
        // Theme.ApplyBorderlessFocusFallback.
        applyBorderlessFocusFallback: true);

    // The mark style and glyph trio come from the active theme's glyph family (see
    // themes.md#glyph-families) rather than a literal hardcoded here - GlyphFamily.Default carries
    // the exact three-cell Brackets layout this style used to hardcode directly, so an unthemed
    // resolution is unchanged. Distinct from CheckBoxGlyphs.Default ('☐','☑','◩'), which is the
    // ONE-cell family belonging to the Square layout - pairing that family with Brackets renders a
    // box inside brackets ("[☐]"), which is what an earlier rewrite accidentally did here.
    private static CheckBoxStyle Complete(InputStyle input, VisualState state, Theme theme) =>
        new(input.Face, NoBorder, NoShadow, theme.Glyphs.CheckBox.MarkStyle, theme.Glyphs.CheckBox.Glyphs)
        {
            // Forwarded from the fallback rather than left at the code-owned value the base
            // constructor supplies. Without this, DropDownGlyph would stay stuck at
            // InputStyle.Default's literal regardless of how a theme customizes "input"'s own
            // dropDownGlyph - a divergence CheckBox would never surface visually, since it never
            // draws a dropdown glyph itself.
            DropDownGlyph = input.DropDownGlyph,
            AffixGap = input.AffixGap
        };

    /// <summary>Gets the non-invalidating definition used by library and external pure forwarding hosts.</summary>
    public static StyleDefinition<CheckBoxStyle> ForwardingDefinition { get; } = StyleDefinitions.Part(
        static theme => Definition.Resolve(null, theme),
        static (_, _, _, _) => InvalidationImpact.None);

    /// <summary>Initializes a complete checkbox presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="markStyle">The validated mark-layout family.</param>
    /// <param name="glyphs">The complete unchecked, checked, and indeterminate glyph family.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="markStyle"/> is undefined.</exception>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    [SetsRequiredMembers]
    public CheckBoxStyle(Face face, Border border, Shadow shadow, CheckBoxMarkStyle markStyle, CheckBoxGlyphs glyphs) : base(face, border, shadow, InputStyle.Default.DropDownGlyph)
    {
        MarkStyle = markStyle;
        Glyphs = new CheckBoxGlyphs(glyphs.Unchecked, glyphs.Checked, glyphs.Indeterminate);
    }

    /// <summary>Gets the standard three-cell bracket presentation.</summary>
    public static new CheckBoxStyle Default => Brackets;

    /// <summary>Gets the three-cell bracket presentation.</summary>
    public static CheckBoxStyle Brackets { get; } = Complete(InputStyle.Default, VisualState.Normal, Theme.Unthemed);

    /// <summary>Gets the one-cell circle, tick, and indeterminate presentation.</summary>
    public static CheckBoxStyle Tick { get; } = Brackets with
    {
        MarkStyle = CheckBoxMarkStyle.Tick,
        Glyphs = new CheckBoxGlyphs(new Rune('○'), new Rune('✓'), new Rune('−'))
    };

    /// <summary>Gets the one-cell square-state presentation.</summary>
    public static CheckBoxStyle Square { get; } = Brackets with
    {
        MarkStyle = CheckBoxMarkStyle.Square,
        Glyphs = CheckBoxGlyphs.Default
    };

    /// <summary>Gets the mark-layout family.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is undefined.</exception>
    public required CheckBoxMarkStyle MarkStyle
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value);
            field = value;
        }
    }

    /// <summary>Gets the complete state glyph family.</summary>
    public required CheckBoxGlyphs Glyphs { get; init; }

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

    /// <summary>Gets which horizontal caption edge owns the check mark.</summary>
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

    /// <summary>Gets the horizontal terminal-cell reservation for the mark.</summary>
    public int MarkWidth => MarkStyle == CheckBoxMarkStyle.Brackets ? 3 : 1;
}
