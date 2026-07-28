// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Xterm;

/// <summary>
/// Contains one validated XTWINOPS geometry response. The default value is an
/// empty sentinel and is identified by <see cref="IsEmpty"/>.
/// </summary>
[PublicAPI]
public readonly record struct MetricsResponse
{
    /// <summary>Initializes one validated geometry response.</summary>
    /// <param name="kind">The window-pixel, cell-pixel, or window-cell family.</param>
    /// <param name="size">The positive bounded width and height.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The response family or either dimension is invalid.
    /// </exception>
    public MetricsResponse(ResponseKind kind, Size size)
    {
        if (kind is not ResponseKind.WindowPixels and
            not ResponseKind.CellPixels and
            not ResponseKind.WindowCells)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The response is not a metrics family.");
        }

        if (size.Width is <= 0 or > ushort.MaxValue ||
            size.Height is <= 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "Metrics dimensions must be from 1 through 65535.");
        }

        Kind = kind;
        Size = size;
    }

    /// <summary>Gets the geometry response family.</summary>
    public ResponseKind Kind { get; }

    /// <summary>Gets whether this value is the valid empty default sentinel.</summary>
    public bool IsEmpty => Kind == ResponseKind.None;

    /// <summary>Gets width and height in the units identified by <see cref="Kind"/>.</summary>
    public Size Size { get; }
}
