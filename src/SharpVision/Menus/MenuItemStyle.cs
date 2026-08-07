// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Menus;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable menu-entry presentation. This style's own "menuItem"
/// theme key falls back to <see cref="ControlStyle"/>'s "control" key for anything it does not
/// author itself.</summary>
/// <remarks>
/// Carries both marker pairs rather than one, because a menu entry's kind - check or radio - is a
/// semantic fact the entry owns, not a presentation choice a theme makes. A theme restyling menu
/// markers has to answer for both kinds or a mixed menu would render half-themed.
/// </remarks>
[PublicAPI]
public sealed record MenuItemStyle: ControlStyle
{
    /// <summary>Gets the primary menu-item style definition. A theme's own "menuItem" key can
    /// restyle all four markers directly - the path the misleadingly-named private
    /// <c>ThemeMenuGlyph</c> resolver never actually had.</summary>
    internal static StyleDefinition<MenuItemStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(ControlStyle.Default),
        Complete,
        Compare);

    private static MenuItemStyle Complete(ControlStyle control, VisualState state) =>
        new(
            control.Face,
            control.Border,
            control.Shadow,
            ControlGlyphs.Selection.MenuCheckUnchecked.Value,
            ControlGlyphs.Selection.MenuCheckChecked.Value,
            ControlGlyphs.Selection.MenuRadioUnchecked.Value,
            ControlGlyphs.Selection.MenuRadioChecked.Value);

    /// <summary>Initializes a complete menu-entry presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="uncheckedGlyph">The printable one-cell unchecked check-kind marker.</param>
    /// <param name="checkedGlyph">The printable one-cell checked check-kind marker.</param>
    /// <param name="radioUncheckedGlyph">The printable one-cell unchecked radio-kind marker.</param>
    /// <param name="radioCheckedGlyph">The printable one-cell checked radio-kind marker.</param>
    /// <exception cref="ArgumentException">A marker is a control or is not one cell wide.</exception>
    [SetsRequiredMembers]
    public MenuItemStyle(
        Face face,
        Border border,
        Shadow shadow,
        Rune uncheckedGlyph,
        Rune checkedGlyph,
        Rune radioUncheckedGlyph,
        Rune radioCheckedGlyph) : base(face, border, shadow)
    {
        UncheckedGlyph = uncheckedGlyph;
        CheckedGlyph = checkedGlyph;
        RadioUncheckedGlyph = radioUncheckedGlyph;
        RadioCheckedGlyph = radioCheckedGlyph;
    }

    /// <summary>Gets the standard menu-entry presentation.</summary>
    public static new MenuItemStyle Default => Complete(ControlStyle.Default, VisualState.Normal);

    /// <summary>Gets the unchecked marker for a check-kind entry.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune UncheckedGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the checked marker for a check-kind entry.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune CheckedGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the unchecked marker for a radio-kind entry.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune RadioUncheckedGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the checked marker for a radio-kind entry.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune RadioCheckedGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    private static InvalidationImpact Compare(
        MenuItemStyle previous,
        Theme? previousTheme,
        MenuItemStyle current,
        Theme? currentTheme) =>
        previous != current
            ? InvalidationImpact.Render
            : InvalidationImpact.None;
}
