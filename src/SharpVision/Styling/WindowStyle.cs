// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines the well-known top-level window appearance - a paired all-side border and a
/// visible shadow by default, the only well-known style that defaults to one - one of the
/// sibling styles <see cref="ControlStyle"/> generalizes.</summary>
[PublicAPI]
public record WindowStyle: ControlStyle
{
    /// <summary>Compares close-button glyphs and resolved paint shared by every Window-derived style.</summary>
    internal static InvalidationImpact GetCloseChromeImpact(
        WindowStyle previous,
        Theme? previousTheme,
        WindowStyle current,
        Theme? currentTheme) =>
        previous.CloseGlyph != current.CloseGlyph ||
        previous.CloseLeftBracket != current.CloseLeftBracket ||
        previous.CloseRightBracket != current.CloseRightBracket ||
        previous.CloseMarkColor != current.CloseMarkColor ||
        previous.CloseMarkActiveColor != current.CloseMarkActiveColor ||
        previous.CloseMarkPressedColor != current.CloseMarkPressedColor ||
        previous.CloseMarkDisabledColor != current.CloseMarkDisabledColor ||
        previous.CloseMarkColor.Resolve(previousTheme) != current.CloseMarkColor.Resolve(currentTheme) ||
        previous.CloseMarkActiveColor.Resolve(previousTheme) != current.CloseMarkActiveColor.Resolve(currentTheme) ||
        previous.CloseMarkPressedColor.Resolve(previousTheme) != current.CloseMarkPressedColor.Resolve(currentTheme) ||
        previous.CloseMarkDisabledColor.Resolve(previousTheme) != current.CloseMarkDisabledColor.Resolve(currentTheme)
            ? InvalidationImpact.Render
            : InvalidationImpact.None;

    /// <summary>Initializes a complete window appearance.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="closeGlyph">The printable one-cell close-button mark.</param>
    /// <param name="closeLeftBracket">The printable one-cell glyph left of the close mark.</param>
    /// <param name="closeRightBracket">The printable one-cell glyph right of the close mark.</param>
    /// <param name="closeMarkColor">The non-transparent close-mark foreground at rest.</param>
    /// <param name="closeMarkActiveColor">The non-transparent close-mark foreground while the pointer is over it.</param>
    /// <param name="closeMarkPressedColor">The non-transparent close-mark foreground while pressed.</param>
    /// <param name="closeMarkDisabledColor">The non-transparent close-mark foreground while disabled.</param>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide, or a part foreground is transparent.</exception>
    [SetsRequiredMembers]
    public WindowStyle(
        Face face,
        Border border,
        Shadow shadow,
        Rune closeGlyph,
        Rune closeLeftBracket,
        Rune closeRightBracket,
        ControlColor closeMarkColor,
        ControlColor closeMarkActiveColor,
        ControlColor closeMarkPressedColor,
        ControlColor closeMarkDisabledColor) : base(face, border, shadow)
    {
        // Validated by parameter name here as well as in each init accessor. The accessor guards
        // the doors a constructor cannot see - a `with` expression and the theme overlay's
        // reflective write - but it only ever knows the value as "value", which for a constructor
        // taking several channels of the same type identifies nothing.
        ControlColor.ValidatePaint(closeMarkColor, nameof(closeMarkColor));
        ControlColor.ValidatePaint(closeMarkActiveColor, nameof(closeMarkActiveColor));
        ControlColor.ValidatePaint(closeMarkPressedColor, nameof(closeMarkPressedColor));
        ControlColor.ValidatePaint(closeMarkDisabledColor, nameof(closeMarkDisabledColor));

        CloseGlyph = closeGlyph;
        CloseLeftBracket = closeLeftBracket;
        CloseRightBracket = closeRightBracket;
        CloseMarkColor = closeMarkColor;
        CloseMarkActiveColor = closeMarkActiveColor;
        CloseMarkPressedColor = closeMarkPressedColor;
        CloseMarkDisabledColor = closeMarkDisabledColor;
    }

    /// <summary>Gets the default window appearance: a paired all-side border and a visible,
    /// composite shadow offset two cells right and one row down.</summary>
    public static new WindowStyle Default { get; } = new(
        DefaultFace,
        new Border(
            BorderSide.All,
            BorderGlyphStyle.Paired,
            Color.Default,
            BorderRelief.Flat,
            Color.Transparent,
            TerminalAttributes.None),
        new Shadow(true, ShadowMode.Composite, new Point(2, 1), ControlGlyphs.Chrome.Shadow.Value, Color.Default, Color.Transparent, TerminalAttributes.Dim),
        ControlGlyphs.Chrome.WindowClose.Value,
        ControlGlyphs.Chrome.WindowCloseLeft.Value,
        ControlGlyphs.Chrome.WindowCloseRight.Value,
        SemanticColor.Accent,
        SemanticColor.ActiveText,
        SemanticColor.PressedText,
        SemanticColor.DisabledText);

    /// <summary>Gets the close-button mark.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune CloseGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the glyph drawn immediately left of the close mark.</summary>
    /// <remarks>
    /// Bracketed with <see cref="CloseRightBracket"/> so a theme targeting a terminal without
    /// dependable box-drawing coverage can render ASCII window chrome. Both were code-owned with no
    /// override at all, so the close mark alone could not produce a coherent ASCII frame.
    /// </remarks>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune CloseLeftBracket
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the glyph drawn immediately right of the close mark.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune CloseRightBracket
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the close-mark foreground at rest.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor CloseMarkColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the close-mark foreground while the pointer is over it.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor CloseMarkActiveColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the close-mark foreground while pressed.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor CloseMarkPressedColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the close-mark foreground while disabled.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor CloseMarkDisabledColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }
}
