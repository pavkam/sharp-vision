// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Defines one titled Table column, its width, edit policy, and optional sort key.</summary>
[PublicAPI]
public readonly record struct TableColumn
{
    /// <summary>Initializes a non-empty table column definition.</summary>
    /// <param name="header">The non-empty visible header text.</param>
    /// <param name="width">The validated width request.</param>
    /// <param name="isReadOnly">Whether cells in this column reject Table editing.</param>
    /// <param name="sortKey">An optional stable comparable key selector for a cell control.</param>
    /// <exception cref="ArgumentException"><paramref name="header"/> is empty or whitespace.</exception>
    public TableColumn(
        string header,
        Length width,
        bool isReadOnly = false,
        Func<Control, IComparable?>? sortKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(header);
        Header = header;
        Width = width;
        IsReadOnly = isReadOnly;
        SortKey = sortKey;
    }

    /// <summary>Gets the non-empty visible header text.</summary>
    public string Header { get; }

    /// <summary>Gets the fixed, automatic, percentage, or proportional width request.</summary>
    public Length Width { get; }

    /// <summary>Gets whether cells in this column are read-only to Table editing.</summary>
    public bool IsReadOnly { get; }

    /// <summary>Gets the optional comparable key selector used by Table sorting.</summary>
    public Func<Control, IComparable?>? SortKey { get; }

    /// <summary>Creates an automatic-width column.</summary>
    /// <param name="header">The non-empty header.</param>
    /// <param name="isReadOnly">Whether cells in this column reject Table editing.</param>
    /// <param name="sortKey">An optional stable comparable key selector for a cell control.</param>
    /// <returns>An automatic column.</returns>
    public static TableColumn Auto(string header, bool isReadOnly = false, Func<Control, IComparable?>? sortKey = null) =>
        new(header, Length.Auto, isReadOnly, sortKey);

    /// <summary>Creates a fixed terminal-cell-width column.</summary>
    /// <param name="header">The non-empty header.</param>
    /// <param name="width">The non-negative fixed cell width.</param>
    /// <param name="isReadOnly">Whether cells in this column reject Table editing.</param>
    /// <param name="sortKey">An optional stable comparable key selector for a cell control.</param>
    /// <returns>A fixed-width column.</returns>
    public static TableColumn Fixed(string header, int width, bool isReadOnly = false, Func<Control, IComparable?>? sortKey = null) =>
        new(header, Length.Cells(width), isReadOnly, sortKey);

    /// <summary>Creates a percentage-width column.</summary>
    /// <param name="header">The non-empty header.</param>
    /// <param name="percent">The finite percentage from zero through one hundred.</param>
    /// <param name="isReadOnly">Whether cells in this column reject Table editing.</param>
    /// <param name="sortKey">An optional stable comparable key selector for a cell control.</param>
    /// <returns>A percentage-width column.</returns>
    public static TableColumn Percent(string header, double percent, bool isReadOnly = false, Func<Control, IComparable?>? sortKey = null) =>
        new(header, Length.Percent(percent), isReadOnly, sortKey);

    /// <summary>Creates a proportional fill column.</summary>
    /// <param name="header">The non-empty header.</param>
    /// <param name="weight">The positive finite proportional weight.</param>
    /// <param name="isReadOnly">Whether cells in this column reject Table editing.</param>
    /// <param name="sortKey">An optional stable comparable key selector for a cell control.</param>
    /// <returns>A fill column.</returns>
    public static TableColumn Fill(string header, double weight = 1, bool isReadOnly = false, Func<Control, IComparable?>? sortKey = null) =>
        new(header, Length.Star(weight), isReadOnly, sortKey);
}
