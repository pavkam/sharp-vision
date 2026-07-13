// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Geometry;

/// <summary>
/// Describes positive pixel dimensions for one terminal cell.
/// </summary>
public readonly record struct Metrics
{
    /// <summary>Initializes validated cell pixel dimensions.</summary>
    /// <param name="width">The positive cell width in pixels.</param>
    /// <param name="height">The positive cell height in pixels.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is not positive.</exception>
    public Metrics(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
    }

    /// <summary>Initializes one exact positive cell and pixel grid.</summary>
    /// <param name="cells">The positive terminal dimensions in cells.</param>
    /// <param name="pixels">The positive terminal dimensions in pixels.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is not positive.</exception>
    /// <exception cref="ArgumentException">A pixel axis is smaller than its cell axis.</exception>
    public Metrics(Size cells, Size pixels)
    {
        if (cells.Width == 0 || cells.Height == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cells),
                cells,
                "Exact cell dimensions must be positive.");
        }

        if (pixels.Width == 0 || pixels.Height == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pixels),
                pixels,
                "Exact pixel dimensions must be positive.");
        }

        if (pixels.Width < cells.Width || pixels.Height < cells.Height)
        {
            throw new ArgumentException(
                "Every terminal cell must own at least one pixel on each axis.",
                nameof(pixels));
        }

        Cells = cells;
        Pixels = pixels;
        Width = pixels.Width / cells.Width;
        Height = pixels.Height / cells.Height;
    }

    /// <summary>Gets one cell's width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets one cell's height in pixels.</summary>
    public int Height { get; }

    /// <summary>Gets exact total cell dimensions, or null for uniform metrics.</summary>
    public Size? Cells { get; }

    /// <summary>Gets exact total pixel dimensions, or null for uniform metrics.</summary>
    public Size? Pixels { get; }

    /// <summary>Maps one non-negative pixel coordinate when its geometry is available.</summary>
    /// <param name="pixels">The zero-based pixel coordinate.</param>
    /// <param name="cells">Receives the exact zero-based cell coordinate on success.</param>
    /// <returns>
    /// Whether the coordinate is non-negative and inside an exact grid, or is
    /// representable by uniform compatibility metrics.
    /// </returns>
    public bool TryMap(Point pixels, out Point cells)
    {
        cells = default;

        if (pixels.X < 0 || pixels.Y < 0 || Width <= 0 || Height <= 0)
        {
            return false;
        }

        if (Cells is not { } cellGrid || Pixels is not { } pixelGrid)
        {
            cells = new Point(pixels.X / Width, pixels.Y / Height);
            return true;
        }

        if (pixels.X >= pixelGrid.Width || pixels.Y >= pixelGrid.Height)
        {
            return false;
        }

        long x = checked((long) pixels.X * cellGrid.Width) / pixelGrid.Width;
        long y = checked((long) pixels.Y * cellGrid.Height) / pixelGrid.Height;
        cells = new Point(checked((int) x), checked((int) y));
        return true;
    }
}
