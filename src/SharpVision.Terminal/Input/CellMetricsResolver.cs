// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Input;

using Xterm;

/// <summary>
/// Resolves the current best-known cell-pixel metrics from locally observed geometry and queried
/// terminal responses, extracted from <see cref="InputDecoder"/> as one of its four self-contained
/// protocol decoders (see #97).
/// </summary>
/// <remarks>
/// Exact locally observed cell and pixel dimensions take precedence over queried window
/// dimensions, which in turn take precedence over a uniform queried cell-pixel measurement. Local
/// dimensions are the caller's own authoritative geometry (e.g. a Kitty <c>CSI 14 t</c> reply
/// arrives asynchronously and can go stale, but resize events reset it immediately).
/// </remarks>
internal sealed class CellMetricsResolver
{
    private Size? _localCells;
    private Size? _localPixels;
    private Size? _queriedCellPixels;
    private Size? _queriedWindowCells;
    private Size? _queriedWindowPixels;

    /// <summary>Initializes the resolver from an optional caller-supplied starting metrics.</summary>
    /// <param name="initial">Exact or uniform starting metrics, or null.</param>
    public CellMetricsResolver(CellMetrics? initial)
    {
        if (initial is { Cells: { } cells, Pixels: { } pixels })
        {
            _localCells = cells;
            _localPixels = pixels;
        }
        else if (initial is { } uniform)
        {
            _queriedCellPixels = new Size(uniform.Width, uniform.Height);
        }

        Current = Recompute();
    }

    /// <summary>Gets the current best-known cell metrics, or null when none are available.</summary>
    public CellMetrics? Current { get; private set; }

    /// <summary>Updates locally observed exact geometry, superseding queried window dimensions.</summary>
    /// <param name="cells">The non-negative local text-area cell dimensions.</param>
    /// <param name="pixels">Optional non-negative local text-area pixel dimensions.</param>
    public void SetGeometry(Size cells, Size? pixels)
    {
        _localCells = HasPositive(cells) ? cells : null;
        _localPixels = pixels is { } pixelSize && HasPositive(pixelSize) ? pixelSize : null;
        _queriedWindowCells = null;
        _queriedWindowPixels = null;
        Current = Recompute();
    }

    /// <summary>Applies one query response, superseding the prior value for its metric family.</summary>
    /// <param name="response">The non-null typed metrics response.</param>
    public void Apply(in MetricsResponse response)
    {
        if (response.Kind == ResponseKind.WindowPixels)
        {
            _queriedWindowPixels = response.Size;
        }
        else if (response.Kind == ResponseKind.CellPixels)
        {
            _queriedCellPixels = response.Size;
        }
        else
        {
            Debug.Assert(
                response.Kind == ResponseKind.WindowCells,
                "CellMetrics responses validate their family at construction.");
            _queriedWindowCells = response.Size;
        }

        Current = Recompute();
    }

    private CellMetrics? Recompute()
    {
        var cells = _localCells ?? _queriedWindowCells;
        var pixels = _localPixels ?? _queriedWindowPixels;

        var hasExact = cells is { } cellSize &&
            pixels is { } pixelSize &&
            pixelSize.Width >= cellSize.Width &&
            pixelSize.Height >= cellSize.Height;

        return hasExact
            ? new CellMetrics(cells!.Value, pixels!.Value)
            : _queriedCellPixels is { } cellPixels
                ? new CellMetrics(cellPixels.Width, cellPixels.Height)
                : null;
    }

    private static bool HasPositive(Size value) => value is { Width: > 0, Height: > 0 };
}
