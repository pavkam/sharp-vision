using SharpVision.Terminal.Geometry;

namespace SharpVision.Terminal.Runtime;

/// <summary>Represents terminal cell dimensions and optional pixel dimensions.</summary>
public readonly record struct Dimensions
{
    /// <summary>Initializes terminal dimensions and derives positive cell metrics.</summary>
    /// <param name="cells">The non-negative cell dimensions.</param>
    /// <param name="pixels">Optional non-negative pixel dimensions.</param>
    public Dimensions(Size cells, Size? pixels = null)
    {
        Cells = cells;
        Pixels = pixels;

        if (cells.Width > 0 && cells.Height > 0 &&
            pixels is { Width: > 0, Height: > 0 } pixelSize)
        {
            var width = pixelSize.Width / cells.Width;
            var height = pixelSize.Height / cells.Height;
            CellMetrics = width > 0 && height > 0
                ? new Metrics(width, height)
                : null;
        }
    }

    /// <summary>Gets terminal dimensions in cells.</summary>
    public Size Cells { get; }

    /// <summary>Gets optional terminal dimensions in pixels.</summary>
    public Size? Pixels { get; }

    /// <summary>Gets positive derived cell-pixel dimensions when available.</summary>
    public Metrics? CellMetrics { get; }

    /// <summary>Gets whether either cell axis is zero.</summary>
    public bool IsSuspended => Cells.Width == 0 || Cells.Height == 0;
}
