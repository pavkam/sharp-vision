// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

using DisplayText = Display.Text;

using MustUseReturnValue = JetBrains.Annotations.MustUseReturnValueAttribute;

/// <summary>Builds a configured Table through a fluent column-then-row sequence.</summary>
[PublicAPI]
public sealed class TableBuilder
{
    private readonly List<(string Header, Length? Width)> _columns = [];
    private readonly List<object?[]> _rows = [];

    /// <summary>Appends one column definition.</summary>
    /// <param name="header">The non-null column header text.</param>
    /// <param name="width">An optional explicit width; defaults to automatic sizing.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="header"/> is null.</exception>
    public TableBuilder Column(string header, Length? width = null)
    {
        ArgumentNullException.ThrowIfNull(header);
        _columns.Add((header, width));
        return this;
    }

    /// <summary>Appends one row whose cell count must match the defined columns.</summary>
    /// <param name="cells">The ordered cell values; each is converted to a <see cref="Display.Text"/> control.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cells"/> is null.</exception>
    /// <exception cref="ArgumentException">The cell count does not match the column count.</exception>
    public TableBuilder Row(params object?[] cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Length != _columns.Count)
        {
            throw new ArgumentException(
                $"Row has {cells.Length} cells but {_columns.Count} columns are defined.",
                nameof(cells));
        }

        _rows.Add(cells);
        return this;
    }

    /// <summary>Materializes the configured Table.</summary>
    /// <returns>A new Table populated with the defined columns and rows.</returns>
    [MustUseReturnValue]
    public Table Build()
    {
        var table = new Table();

        foreach (var (header, width) in _columns)
        {
            table.Columns.Add(new TableColumn(header, width ?? Length.Auto));
        }

        foreach (var cells in _rows)
        {
            var controls = new ControlBase[cells.Length];

            for (var index = 0; index < cells.Length; index++)
            {
                controls[index] = new DisplayText(
                    Convert.ToString(cells[index], CultureInfo.InvariantCulture) ?? string.Empty);
            }

            table.Rows.Add(new TableRow(controls));
        }

        return table;
    }
}
