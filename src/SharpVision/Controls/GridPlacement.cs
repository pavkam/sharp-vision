namespace SharpVision.Controls;

/// <summary>Stores weakly attached Grid origin and span values.</summary>
internal sealed class GridPlacement
{
    /// <summary>Gets or sets the zero-based row.</summary>
    internal int Row { get; set; }

    /// <summary>Gets or sets the zero-based column.</summary>
    internal int Column { get; set; }

    /// <summary>Gets or sets the positive row span.</summary>
    internal int RowSpan { get; set; } = 1;

    /// <summary>Gets or sets the positive column span.</summary>
    internal int ColumnSpan { get; set; } = 1;
}
