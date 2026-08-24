// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable checkbox presentation. This style's own
/// "checkBox" theme key falls back to <see cref="InputStyle"/>'s "input" key, chromeless
/// (a checkbox is a selectable control, not a framed one).</summary>
[PublicAPI]
public sealed record CheckBoxStyle: InputStyle
{
    /// <summary>Gets the primary checkbox-style definition. A theme's own "checkBox" key can
    /// restyle MarkStyle, Glyphs, or anything else directly - the standalone registrable
    /// "checkBox" style section this used to read separately is retired; the reflective
    /// Overlay reaches every member of this type uniformly.</summary>
    internal static StyleDefinition<CheckBoxStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(InputStyle.Default),
        Complete,
        static (previous, _, current, _) =>
            previous.MarkWidth != current.MarkWidth || previous.AffixGap != current.AffixGap
                ? InvalidationImpact.Measure
                : previous.MarkStyle != current.MarkStyle || previous.Glyphs != current.Glyphs
                    ? InvalidationImpact.Render
                    : InvalidationImpact.None);

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
            // constructor supplies. Without this the member is inherited into this style's own
            // theme key, accepted by the parser, and silently ignored - the exact
            // blessed-but-unread section themes.md says the design exists to prevent.
            DropDownGlyph = input.DropDownGlyph,
            AffixGap = input.AffixGap
        };

    /// <summary>Gets the non-invalidating definition used by pure forwarding hosts.</summary>
    internal static StyleDefinition<CheckBoxStyle> ForwardingDefinition { get; } = StyleDefinitions.Part(
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

    /// <summary>Gets the horizontal terminal-cell reservation for the mark.</summary>
    public int MarkWidth => MarkStyle == CheckBoxMarkStyle.Brackets ? 3 : 1;
}
