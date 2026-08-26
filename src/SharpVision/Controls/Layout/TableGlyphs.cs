// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Defines the complete immutable table glyph family: the grid-line runes and the sorted
/// column's direction indicators. Each member is the theme-customizable primary glyph only - the
/// portable ASCII repair value stays permanently code-owned, the same split every other glyph
/// family uses.</summary>
[PublicAPI]
public readonly record struct TableGlyphs: IAppearanceFragment
{
    /// <summary>Initializes the complete glyph family.</summary>
    /// <param name="horizontal">The horizontal rule between rows.</param>
    /// <param name="vertical">The vertical rule between columns.</param>
    /// <param name="cross">The intersection of the two rules.</param>
    /// <param name="sortAscending">The ascending sort indicator drawn in the sorted column's header.</param>
    /// <param name="sortDescending">The descending sort indicator drawn in the sorted column's header.</param>
    /// <param name="placeholder">The skeleton glyph drawn in an unloaded progressive row.</param>
    /// <param name="placeholderError">The skeleton glyph drawn in a permanently failed progressive row.</param>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    public TableGlyphs(
        Rune horizontal,
        Rune vertical,
        Rune cross,
        Rune sortAscending,
        Rune sortDescending,
        Rune placeholder,
        Rune placeholderError)
    {
        // Validated by parameter name here as well as in each init accessor, since an accessor only
        // ever knows the value as "value".
        Horizontal = horizontal.ValidateSingleCell(nameof(horizontal));
        Vertical = vertical.ValidateSingleCell(nameof(vertical));
        Cross = cross.ValidateSingleCell(nameof(cross));
        SortAscending = sortAscending.ValidateSingleCell(nameof(sortAscending));
        SortDescending = sortDescending.ValidateSingleCell(nameof(sortDescending));
        Placeholder = placeholder.ValidateSingleCell(nameof(placeholder));
        PlaceholderError = placeholderError.ValidateSingleCell(nameof(placeholderError));
    }

    /// <summary>Initializes the grid-line and sort-indicator glyphs, keeping the code-owned
    /// progressive placeholder glyphs.</summary>
    /// <param name="horizontal">The horizontal rule between rows.</param>
    /// <param name="vertical">The vertical rule between columns.</param>
    /// <param name="cross">The intersection of the two rules.</param>
    /// <param name="sortAscending">The ascending sort indicator drawn in the sorted column's header.</param>
    /// <param name="sortDescending">The descending sort indicator drawn in the sorted column's header.</param>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    public TableGlyphs(Rune horizontal, Rune vertical, Rune cross, Rune sortAscending, Rune sortDescending)
        : this(
            horizontal,
            vertical,
            cross,
            sortAscending,
            sortDescending,
            ControlGlyphs.Separators.TablePlaceholder.Value,
            ControlGlyphs.Separators.TablePlaceholderError.Value)
    {
    }

    /// <summary>Initializes the grid-line glyphs, keeping the code-owned sort indicators and
    /// progressive placeholder glyphs.</summary>
    /// <param name="horizontal">The horizontal rule between rows.</param>
    /// <param name="vertical">The vertical rule between columns.</param>
    /// <param name="cross">The intersection of the two rules.</param>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    public TableGlyphs(Rune horizontal, Rune vertical, Rune cross)
        : this(
            horizontal,
            vertical,
            cross,
            ControlGlyphs.Separators.TableSortAscending.Value,
            ControlGlyphs.Separators.TableSortDescending.Value,
            ControlGlyphs.Separators.TablePlaceholder.Value,
            ControlGlyphs.Separators.TablePlaceholderError.Value)
    {
    }

    /// <summary>Gets the established code-owned glyph family.</summary>
    public static TableGlyphs Default { get; } = new(
        ControlGlyphs.Separators.TableHorizontal.Value,
        ControlGlyphs.Separators.TableVertical.Value,
        ControlGlyphs.Separators.TableCross.Value,
        ControlGlyphs.Separators.TableSortAscending.Value,
        ControlGlyphs.Separators.TableSortDescending.Value,
        ControlGlyphs.Separators.TablePlaceholder.Value,
        ControlGlyphs.Separators.TablePlaceholderError.Value);

    /// <summary>Gets the horizontal rule drawn between rows.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Horizontal
    {
        get => field.Value == 0 ? Default.Horizontal : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the vertical rule drawn between columns.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Vertical
    {
        get => field.Value == 0 ? Default.Vertical : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the glyph drawn where the two rules meet.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Cross
    {
        get => field.Value == 0 ? Default.Cross : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the indicator drawn in the sorted column's header when it is sorted ascending.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune SortAscending
    {
        get => field.Value == 0 ? Default.SortAscending : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the indicator drawn in the sorted column's header when it is sorted descending.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune SortDescending
    {
        get => field.Value == 0 ? Default.SortDescending : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the skeleton glyph drawn across an unloaded progressive row.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Placeholder
    {
        get => field.Value == 0 ? Default.Placeholder : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the skeleton glyph drawn across a progressive row whose range permanently
    /// failed to load.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune PlaceholderError
    {
        get => field.Value == 0 ? Default.PlaceholderError : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the code-owned horizontal rule and its repair value.</summary>
    internal ControlGlyph HorizontalGlyph => new(Horizontal, ControlGlyphs.Separators.TableHorizontal.Fallback);

    /// <summary>Gets the code-owned vertical rule and its repair value.</summary>
    internal ControlGlyph VerticalGlyph => new(Vertical, ControlGlyphs.Separators.TableVertical.Fallback);

    /// <summary>Gets the code-owned intersection glyph and its repair value.</summary>
    internal ControlGlyph CrossGlyph => new(Cross, ControlGlyphs.Separators.TableCross.Fallback);

    /// <summary>Gets the code-owned ascending sort indicator and its repair value.</summary>
    internal ControlGlyph SortAscendingGlyph => new(SortAscending, ControlGlyphs.Separators.TableSortAscending.Fallback);

    /// <summary>Gets the code-owned descending sort indicator and its repair value.</summary>
    internal ControlGlyph SortDescendingGlyph => new(SortDescending, ControlGlyphs.Separators.TableSortDescending.Fallback);

    /// <summary>Gets the code-owned progressive-loading placeholder glyph and its repair value.</summary>
    internal ControlGlyph PlaceholderGlyph => new(Placeholder, ControlGlyphs.Separators.TablePlaceholder.Fallback);

    /// <summary>Gets the code-owned progressive load-failure placeholder glyph and its repair value.</summary>
    internal ControlGlyph PlaceholderErrorGlyph => new(PlaceholderError, ControlGlyphs.Separators.TablePlaceholderError.Fallback);

    IAppearanceFragment IAppearanceFragment.Clone() => this with { };
}
