namespace SharpVision.Controls;

using SharpVision.Layout;

/// <summary>Defines one titled Table column and its fixed, automatic, percentage, or proportional width.</summary>
public readonly record struct TableColumn
{
    /// <summary>Initializes a non-empty table column definition.</summary>
    /// <param name="header">The non-empty visible header text.</param>
    /// <param name="width">The validated width request.</param>
    /// <exception cref="ArgumentException"><paramref name="header"/> is empty or whitespace.</exception>
    public TableColumn(string header, Length width)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(header);
        Header = header;
        Width = width;
    }

    /// <summary>Gets the non-empty visible header text.</summary>
    public string Header { get; }

    /// <summary>Gets the fixed, automatic, percentage, or proportional width request.</summary>
    public Length Width { get; }

    /// <summary>Creates an automatic-width column.</summary>
    /// <param name="header">The non-empty header.</param>
    /// <returns>An automatic column.</returns>
    public static TableColumn Auto(string header) => new(header, Length.Auto);

    /// <summary>Creates a fixed terminal-cell-width column.</summary>
    /// <param name="header">The non-empty header.</param>
    /// <param name="width">The non-negative fixed cell width.</param>
    /// <returns>A fixed-width column.</returns>
    public static TableColumn Fixed(string header, int width) => new(header, Length.Cells(width));

    /// <summary>Creates a percentage-width column.</summary>
    /// <param name="header">The non-empty header.</param>
    /// <param name="percent">The finite percentage from zero through one hundred.</param>
    /// <returns>A percentage-width column.</returns>
    public static TableColumn Percent(string header, double percent) => new(header, Length.Percent(percent));

    /// <summary>Creates a proportional fill column.</summary>
    /// <param name="header">The non-empty header.</param>
    /// <param name="weight">The positive finite proportional weight.</param>
    /// <returns>A fill column.</returns>
    public static TableColumn Fill(string header, double weight = 1) => new(header, Length.Star(weight));
}
