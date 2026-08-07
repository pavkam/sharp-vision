// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Defines the complete immutable table grid-line glyph family. Each member is the
/// theme-customizable primary glyph only - the portable ASCII repair value stays permanently
/// code-owned, the same split every other glyph family uses.</summary>
[PublicAPI]
public readonly record struct TableGlyphs: IAppearanceFragment
{
    /// <summary>Initializes the complete grid-line glyph family.</summary>
    /// <param name="horizontal">The horizontal rule between rows.</param>
    /// <param name="vertical">The vertical rule between columns.</param>
    /// <param name="cross">The intersection of the two rules.</param>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    public TableGlyphs(Rune horizontal, Rune vertical, Rune cross)
    {
        // Validated by parameter name here as well as in each init accessor, since an accessor only
        // ever knows the value as "value".
        Horizontal = horizontal.ValidateSingleCell(nameof(horizontal));
        Vertical = vertical.ValidateSingleCell(nameof(vertical));
        Cross = cross.ValidateSingleCell(nameof(cross));
    }

    /// <summary>Gets the established code-owned grid-line family.</summary>
    public static TableGlyphs Default { get; } = new(
        ControlGlyphs.Separators.TableHorizontal.Value,
        ControlGlyphs.Separators.TableVertical.Value,
        ControlGlyphs.Separators.TableCross.Value);

    /// <summary>Gets the horizontal rule drawn between rows.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Horizontal
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the vertical rule drawn between columns.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Vertical
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the glyph drawn where the two rules meet.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Cross
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the code-owned horizontal rule and its repair value.</summary>
    internal ControlGlyph HorizontalGlyph => new(Horizontal, ControlGlyphs.Separators.TableHorizontal.Fallback);

    /// <summary>Gets the code-owned vertical rule and its repair value.</summary>
    internal ControlGlyph VerticalGlyph => new(Vertical, ControlGlyphs.Separators.TableVertical.Fallback);

    /// <summary>Gets the code-owned intersection glyph and its repair value.</summary>
    internal ControlGlyph CrossGlyph => new(Cross, ControlGlyphs.Separators.TableCross.Fallback);

    IAppearanceFragment IAppearanceFragment.Clone() => this with { };
}
