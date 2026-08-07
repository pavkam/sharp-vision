// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines general, menu, table, and tab separator glyphs.</summary>
internal readonly record struct SeparatorGlyphs
{
    /// <summary>Initializes the complete separator glyph family.</summary>
    /// <param name="horizontal">The general horizontal separator.</param>
    /// <param name="vertical">The general vertical separator.</param>
    /// <param name="menu">The menu separator.</param>
    /// <param name="tableHorizontal">The table horizontal grid glyph.</param>
    /// <param name="tableVertical">The table vertical grid glyph.</param>
    /// <param name="tableCross">The table grid-intersection glyph.</param>
    /// <param name="tableSortAscending">The table ascending sort indicator.</param>
    /// <param name="tableSortDescending">The table descending sort indicator.</param>
    /// <param name="tabDivider">The tab-divider glyph.</param>
    /// <param name="tabUnderline">The selected-tab underline glyph.</param>
    public SeparatorGlyphs(
        ControlGlyph horizontal,
        ControlGlyph vertical,
        ControlGlyph menu,
        ControlGlyph tableHorizontal,
        ControlGlyph tableVertical,
        ControlGlyph tableCross,
        ControlGlyph tableSortAscending,
        ControlGlyph tableSortDescending,
        ControlGlyph tabDivider,
        ControlGlyph tabUnderline)
    {
        Horizontal = horizontal;
        Vertical = vertical;
        Menu = menu;
        TableHorizontal = tableHorizontal;
        TableVertical = tableVertical;
        TableCross = tableCross;
        TableSortAscending = tableSortAscending;
        TableSortDescending = tableSortDescending;
        TabDivider = tabDivider;
        TabUnderline = tabUnderline;
    }

    /// <summary>Gets the general horizontal separator.</summary>
    public ControlGlyph Horizontal { get; }

    /// <summary>Gets the general vertical separator.</summary>
    public ControlGlyph Vertical { get; }

    /// <summary>Gets the menu separator.</summary>
    public ControlGlyph Menu { get; }

    /// <summary>Gets the table horizontal grid glyph.</summary>
    public ControlGlyph TableHorizontal { get; }

    /// <summary>Gets the table vertical grid glyph.</summary>
    public ControlGlyph TableVertical { get; }

    /// <summary>Gets the table grid intersection glyph.</summary>
    public ControlGlyph TableCross { get; }

    /// <summary>Gets the table ascending sort indicator.</summary>
    public ControlGlyph TableSortAscending { get; }

    /// <summary>Gets the table descending sort indicator.</summary>
    public ControlGlyph TableSortDescending { get; }

    /// <summary>Gets the tab divider.</summary>
    public ControlGlyph TabDivider { get; }

    /// <summary>Gets the selected-tab underline.</summary>
    public ControlGlyph TabUnderline { get; }
}
