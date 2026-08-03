// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Geometry;

/// <summary>Represents terminal cell dimensions and optional pixel dimensions.</summary>
[PublicAPI]
public readonly record struct Dimensions
{
    /// <summary>Initializes terminal dimensions and derives positive cell metrics.</summary>
    /// <param name="cells">The non-negative cell dimensions.</param>
    /// <param name="pixels">Optional non-negative pixel dimensions.</param>
    public Dimensions(Size cells, Size? pixels = null)
    {
        Cells = cells;
        Pixels = pixels;

        if (cells is { Width: > 0, Height: > 0 } &&
            pixels is { Width: > 0, Height: > 0 } pixelSize &&
            pixelSize.Width >= cells.Width &&
            pixelSize.Height >= cells.Height)
        {
            CellMetrics = new CellMetrics(cells, pixelSize);
        }
    }

    /// <summary>Gets terminal dimensions in cells.</summary>
    public Size Cells { get; }

    /// <summary>Gets optional terminal dimensions in pixels.</summary>
    public Size? Pixels { get; }

    /// <summary>Gets positive derived cell-pixel dimensions when available.</summary>
    public CellMetrics? CellMetrics { get; }

    /// <summary>Gets whether either cell axis is zero.</summary>
    public bool IsSuspended => Cells.Width == 0 || Cells.Height == 0;
}
