// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;


/// <summary>Reports observable results of one canvas draw operation.</summary>
public readonly record struct DrawResult
{
    /// <summary>Initializes validated draw metrics.</summary>
    /// <param name="final">The next logical cell position.</param>
    /// <param name="graphemes">The non-negative processed grapheme count.</param>
    /// <param name="cells">The non-negative printable-cell advance.</param>
    /// <param name="clipped">The non-negative clipped-cluster count.</param>
    /// <param name="replaced">The non-negative replacement count.</param>
    /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
    public DrawResult(Point final, int graphemes, int cells, int clipped, int replaced)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(graphemes);
        ArgumentOutOfRangeException.ThrowIfNegative(cells);
        ArgumentOutOfRangeException.ThrowIfNegative(clipped);
        ArgumentOutOfRangeException.ThrowIfNegative(replaced);

        Final = final;
        Graphemes = graphemes;
        Cells = cells;
        Clipped = clipped;
        Replaced = replaced;
    }

    /// <summary>Gets the next logical cell position.</summary>
    public Point Final { get; }

    /// <summary>Gets the number of input grapheme clusters processed.</summary>
    public int Graphemes { get; }

    /// <summary>Gets the number of logical printable cells advanced.</summary>
    public int Cells { get; }

    /// <summary>Gets the number of complete clusters not drawn.</summary>
    public int Clipped { get; }

    /// <summary>Gets the number of edge clusters replaced with U+FFFD.</summary>
    public int Replaced { get; }

    /// <summary>Deconstructs the draw metrics.</summary>
    /// <param name="final">Receives the next logical position.</param>
    /// <param name="graphemes">Receives the processed grapheme count.</param>
    /// <param name="cells">Receives the printable-cell advance.</param>
    /// <param name="clipped">Receives the clipped-cluster count.</param>
    /// <param name="replaced">Receives the replacement count.</param>
    public void Deconstruct(out Point final, out int graphemes, out int cells, out int clipped, out int replaced)
    {
        final = Final;
        graphemes = Graphemes;
        cells = Cells;
        clipped = Clipped;
        replaced = Replaced;
    }
}
