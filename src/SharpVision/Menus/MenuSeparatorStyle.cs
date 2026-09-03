// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Menus;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable menu-divider presentation. This style declares no
/// theme section of its own: it falls back to <see cref="ControlStyle"/>'s "control" role section
/// for its passive chrome, resolves its own divider glyph from a code-owned default, and is
/// themeable only through that fallback and a locally assigned
/// <see cref="MenuSeparator.Style"/>.</summary>
[PublicAPI]
public sealed record MenuSeparatorStyle: ControlStyle
{
    /// <summary>Gets the primary menu-separator style definition. Falls back to
    /// <see cref="ControlStyle"/>'s "control" role section; the divider glyph is
    /// code-owned.</summary>
    internal static StyleDefinition<MenuSeparatorStyle> Definition { get; } = StyleDefinitions.BarControlWithThemeOwnedStateDefaults(
        static theme => theme.GetStyleSet(ControlStyle.Default),
        Complete,
        static (previous, _, current, _) =>
            previous != current ? InvalidationImpact.Render : InvalidationImpact.None);

    private static MenuSeparatorStyle Complete(ControlStyle control, VisualState state, Theme theme)
    {
        var states = theme.GetStyleSet(ControlStyle.Default);
        return new MenuSeparatorStyle(
            BarAppearance.CompleteFace(control, state, states),
            control.Border,
            control.Shadow,
            ControlGlyphs.Separators.Menu.Value,
            ControlGlyphs.Separators.Vertical.Value);
    }

    /// <summary>Initializes a complete menu-divider presentation with the standard vertical-divider glyph.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="glyph">The printable one-cell horizontal-rule glyph.</param>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    [SetsRequiredMembers]
    public MenuSeparatorStyle(
        Face face,
        Border border,
        Shadow shadow,
        Rune glyph) : this(face, border, shadow, glyph, ControlGlyphs.Separators.Vertical.Value)
    {
    }

    /// <summary>Initializes a complete menu-divider presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="glyph">The printable one-cell horizontal-rule glyph.</param>
    /// <param name="verticalGlyph">The printable one-cell vertical-divider glyph.</param>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    [SetsRequiredMembers]
    public MenuSeparatorStyle(
        Face face,
        Border border,
        Shadow shadow,
        Rune glyph,
        Rune verticalGlyph) : base(face, border, shadow)
    {
        Glyph = glyph;
        VerticalGlyph = verticalGlyph;
    }

    /// <summary>Gets the standard menu-divider presentation.</summary>
    public static new MenuSeparatorStyle Default => Complete(ControlStyle.Default, VisualState.Normal, Theme.Unthemed);

    /// <summary>Gets the horizontal-rule glyph used inside a vertical menu.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune Glyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the vertical-divider glyph used inside a horizontal menu.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune VerticalGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }
}
