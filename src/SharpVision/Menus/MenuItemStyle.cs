// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Menus;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable menu-entry presentation. This style declares no theme
/// section of its own: it falls back to the standard borderless interactive appearance for its
/// passive chrome, resolves its own check and radio markers from code-owned glyph constants, and
/// is themeable only through that fallback and a locally assigned <see cref="MenuItem.Style"/>.</summary>
/// <remarks>
/// Carries both marker pairs rather than one, because a menu entry's kind - check or radio - is a
/// semantic fact the entry owns, not a presentation choice a theme makes. A theme restyling menu
/// markers has to answer for both kinds or a mixed menu would render half-themed.
/// </remarks>
[PublicAPI]
public sealed record MenuItemStyle: ControlStyle
{
    /// <summary>Gets the primary menu-item style definition. Falls back through
    /// <see cref="Theme.GetInteractiveControlStyleSet"/>; all four check and radio markers are
    /// code-owned from <see cref="ControlGlyphs.Selection"/>.</summary>
    internal static StyleDefinition<MenuItemStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetInteractiveControlStyleSet(),
        Complete,
        static (previous, _, current, _) =>
            previous.AffixGap != current.AffixGap
                ? InvalidationImpact.Measure
                : previous != current
                    ? InvalidationImpact.Render
                    : InvalidationImpact.None);

    private static MenuItemStyle Complete(ControlStyle control, VisualState state, Theme theme) =>
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
    public static new MenuItemStyle Default => Complete(ControlStyle.Default, VisualState.Normal, Theme.Unthemed);

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

    /// <summary>Gets the cell gap between a present <see cref="Affix"/> and the caption it
    /// sits beside.</summary>
    /// <remarks>
    /// Mirrors <see cref="InputStyle.AffixGap"/>'s shape and code-owned default. This style
    /// derives from <see cref="ControlStyle"/> directly rather than <see cref="InputStyle"/>, so it
    /// cannot inherit that member and carries its own copy instead.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is outside 0-4.</exception>
    public int AffixGap
    {
        get;
        init => field = value is >= 0 and <= 4
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "The affix gap must be between 0 and 4 cells.");
    } = 1;
}
