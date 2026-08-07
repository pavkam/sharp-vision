// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Geometry;

/// <summary>
/// Describes positive pixel dimensions for one terminal cell.
/// </summary>
[PublicAPI]
public readonly record struct CellMetrics
{
    /// <summary>Initializes validated cell pixel dimensions.</summary>
    /// <param name="width">The positive cell width in pixels.</param>
    /// <param name="height">The positive cell height in pixels.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is not positive.</exception>
    public CellMetrics(int width, int height)
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
    public CellMetrics(Size cells, Size pixels)
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

        var x = checked((long) pixels.X * cellGrid.Width) / pixelGrid.Width;
        var y = checked((long) pixels.Y * cellGrid.Height) / pixelGrid.Height;

        cells = new Point(checked((int) x), checked((int) y));

        return true;
    }

    /// <summary>Maps a positive pixel extent to the smallest covering terminal-cell extent.</summary>
    /// <param name="pixels">The positive image extent in pixels.</param>
    /// <param name="cells">Receives the positive covering cell extent on success.</param>
    /// <returns>Whether the pixel extent is positive and its mapped cell extent is representable.</returns>
    /// <remarks>
    /// Exact uneven grids invert the same cumulative ceiling boundaries used by
    /// <see cref="TryMapCells"/>. Extents beyond the measured viewport continue at the measured
    /// rational density so intrinsic image measure is deterministic before arrange clipping.
    /// </remarks>
    public bool TryMeasureCells(Size pixels, out Size cells)
    {
        cells = default;

        if (pixels.Width <= 0 || pixels.Height <= 0 || Width <= 0 || Height <= 0)
        {
            return false;
        }

        long width;
        long height;

        if (Cells is { } cellGrid && Pixels is { } pixelGrid)
        {
            width = (((long) pixels.Width - 1) * cellGrid.Width / pixelGrid.Width) + 1;
            height = (((long) pixels.Height - 1) * cellGrid.Height / pixelGrid.Height) + 1;
        }
        else
        {
            width = (((long) pixels.Width - 1) / Width) + 1;
            height = (((long) pixels.Height - 1) / Height) + 1;
        }

        if (width > int.MaxValue || height > int.MaxValue)
        {
            return false;
        }

        cells = new Size((int) width, (int) height);
        return true;
    }

    /// <summary>Maps one positive cell rectangle to its exact pixel boundaries.</summary>
    /// <param name="cells">The non-negative half-open cell rectangle.</param>
    /// <param name="pixels">Receives the exact half-open pixel rectangle on success.</param>
    /// <returns>
    /// Whether the rectangle is positive and inside an exact grid, or can be represented without
    /// overflow by uniform compatibility metrics.
    /// </returns>
    public bool TryMapCells(Rect cells, out Rect pixels)
    {
        pixels = default;

        if (cells.X < 0 ||
            cells.Y < 0 ||
            cells.Width <= 0 ||
            cells.Height <= 0 ||
            Width <= 0 ||
            Height <= 0)
        {
            return false;
        }

        var cellRight = (long) cells.X + cells.Width;
        var cellBottom = (long) cells.Y + cells.Height;

        if (cellRight > int.MaxValue || cellBottom > int.MaxValue)
        {
            return false;
        }

        var right = (int) cellRight;
        var bottom = (int) cellBottom;

        if (Cells is { } cellGrid && Pixels is { } pixelGrid)
        {
            if (right > cellGrid.Width || bottom > cellGrid.Height)
            {
                return false;
            }

            var left = CeilingBoundary(cells.X, pixelGrid.Width, cellGrid.Width);
            var top = CeilingBoundary(cells.Y, pixelGrid.Height, cellGrid.Height);
            var pixelRight = CeilingBoundary(right, pixelGrid.Width, cellGrid.Width);
            var pixelBottom = CeilingBoundary(bottom, pixelGrid.Height, cellGrid.Height);

            pixels = new Rect(left, top, pixelRight - left, pixelBottom - top);

            return true;
        }

        var uniformRight = cellRight * Width;
        var uniformBottom = cellBottom * Height;

        if (uniformRight > int.MaxValue || uniformBottom > int.MaxValue)
        {
            return false;
        }

        var uniformLeft = checked(cells.X * Width);
        var uniformTop = checked(cells.Y * Height);

        pixels = new Rect(
            uniformLeft,
            uniformTop,
            (int) uniformRight - uniformLeft,
            (int) uniformBottom - uniformTop);

        return true;
    }

    private static int CeilingBoundary(int value, int pixels, int cells) =>
        checked((int) ((((long) value * pixels) + cells - 1) / cells));
}
