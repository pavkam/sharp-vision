// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines general, menu, table, and tab separator glyphs.</summary>
public readonly record struct SeparatorGlyphs
{
    /// <summary>Initializes the complete separator glyph family.</summary>
    /// <param name="horizontal">The general horizontal separator.</param>
    /// <param name="vertical">The general vertical separator.</param>
    /// <param name="menu">The menu separator.</param>
    /// <param name="tableHorizontal">The table horizontal grid glyph.</param>
    /// <param name="tableVertical">The table vertical grid glyph.</param>
    /// <param name="tableCross">The table grid-intersection glyph.</param>
    /// <param name="tabDivider">The tab-divider glyph.</param>
    /// <param name="tabUnderline">The selected-tab underline glyph.</param>
    public SeparatorGlyphs(
        ThemedGlyph horizontal,
        ThemedGlyph vertical,
        ThemedGlyph menu,
        ThemedGlyph tableHorizontal,
        ThemedGlyph tableVertical,
        ThemedGlyph tableCross,
        ThemedGlyph tabDivider,
        ThemedGlyph tabUnderline)
    {
        Horizontal = horizontal;
        Vertical = vertical;
        Menu = menu;
        TableHorizontal = tableHorizontal;
        TableVertical = tableVertical;
        TableCross = tableCross;
        TabDivider = tabDivider;
        TabUnderline = tabUnderline;
    }

    /// <summary>Gets the general horizontal separator.</summary>
    public ThemedGlyph Horizontal { get; }
    /// <summary>Gets the general vertical separator.</summary>
    public ThemedGlyph Vertical { get; }
    /// <summary>Gets the menu separator.</summary>
    public ThemedGlyph Menu { get; }
    /// <summary>Gets the table horizontal grid glyph.</summary>
    public ThemedGlyph TableHorizontal { get; }
    /// <summary>Gets the table vertical grid glyph.</summary>
    public ThemedGlyph TableVertical { get; }
    /// <summary>Gets the table grid intersection glyph.</summary>
    public ThemedGlyph TableCross { get; }
    /// <summary>Gets the tab divider.</summary>
    public ThemedGlyph TabDivider { get; }
    /// <summary>Gets the selected-tab underline.</summary>
    public ThemedGlyph TabUnderline { get; }
}
