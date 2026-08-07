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
    /// <summary>Initializes a complete window appearance.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="closeGlyph">The printable one-cell close-button mark.</param>
    /// <param name="closeLeftBracket">The printable one-cell glyph left of the close mark.</param>
    /// <param name="closeRightBracket">The printable one-cell glyph right of the close mark.</param>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    [SetsRequiredMembers]
    public WindowStyle(
        Face face,
        Border border,
        Shadow shadow,
        Rune closeGlyph,
        Rune closeLeftBracket,
        Rune closeRightBracket) : base(face, border, shadow)
    {
        CloseGlyph = closeGlyph;
        CloseLeftBracket = closeLeftBracket;
        CloseRightBracket = closeRightBracket;
    }

    /// <summary>Gets the default window appearance: a paired all-side border and a visible,
    /// composite shadow offset two cells right and one row down.</summary>
    public static new WindowStyle Default { get; } = new(
        DefaultFace,
        new Border(BorderSide.All, BorderGlyphStyle.Paired, Color.Default, Color.Transparent, TerminalAttributes.None),
        new Shadow(true, ShadowMode.Composite, new Point(2, 1), ControlGlyphs.Chrome.Shadow.Value, Color.Default, Color.Transparent, TerminalAttributes.Dim),
        ControlGlyphs.Chrome.WindowClose.Value,
        ControlGlyphs.Chrome.WindowCloseLeft.Value,
        ControlGlyphs.Chrome.WindowCloseRight.Value);

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
}
