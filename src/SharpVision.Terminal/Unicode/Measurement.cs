namespace SharpVision.Terminal.Unicode;

/// <summary>Reports whole-span terminal cell measurement.</summary>
public readonly record struct Measurement
{
    /// <summary>Initializes validated whole-span measurement.</summary>
    /// <param name="cells">The non-negative terminal-cell count.</param>
    /// <param name="graphemes">The non-negative grapheme count.</param>
    /// <param name="controls">The non-negative control-cluster count.</param>
    /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
    public Measurement(int cells, int graphemes, int controls)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cells);
        ArgumentOutOfRangeException.ThrowIfNegative(graphemes);
        ArgumentOutOfRangeException.ThrowIfNegative(controls);

        Cells = cells;
        Graphemes = graphemes;
        Controls = controls;
    }

    /// <summary>Gets the printable terminal-cell count.</summary>
    public int Cells { get; }

    /// <summary>Gets the extended-grapheme count.</summary>
    public int Graphemes { get; }

    /// <summary>Gets the contextual control-cluster count.</summary>
    public int Controls { get; }

    /// <summary>Deconstructs the whole-span measurement.</summary>
    /// <param name="cells">Receives the terminal-cell count.</param>
    /// <param name="graphemes">Receives the grapheme count.</param>
    /// <param name="controls">Receives the control-cluster count.</param>
    public void Deconstruct(out int cells, out int graphemes, out int controls)
    {
        cells = Cells;
        graphemes = Graphemes;
        controls = Controls;
    }
}
