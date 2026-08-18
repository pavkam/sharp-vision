// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Menus;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable menu-divider presentation. This style's own
/// "menuSeparator" theme key falls back to <see cref="ControlStyle"/>'s "control" key for anything
/// it does not author itself.</summary>
[PublicAPI]
public sealed record MenuSeparatorStyle: ControlStyle
{
    /// <summary>Gets the primary menu-separator style definition. A theme's own "menuSeparator" key
    /// can restyle the divider glyph directly, the same way "separator" already could.</summary>
    internal static StyleDefinition<MenuSeparatorStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(ControlStyle.Default),
        Complete,
        static (previous, _, current, _) =>
            previous != current ? InvalidationImpact.Render : InvalidationImpact.None);

    private static MenuSeparatorStyle Complete(ControlStyle control, VisualState state) =>
        new(control.Face, control.Border, control.Shadow, ControlGlyphs.Separators.Menu.Value);

    /// <summary>Initializes a complete menu-divider presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="glyph">The printable one-cell divider glyph.</param>
    /// <exception cref="ArgumentException"><paramref name="glyph"/> is a control or is not one cell wide.</exception>
    [SetsRequiredMembers]
    public MenuSeparatorStyle(Face face, Border border, Shadow shadow, Rune glyph) : base(face, border, shadow) =>
        Glyph = glyph;

    /// <summary>Gets the standard menu-divider presentation.</summary>
    public static new MenuSeparatorStyle Default => Complete(ControlStyle.Default, VisualState.Normal);

    /// <summary>Gets the divider glyph.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune Glyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }
}
