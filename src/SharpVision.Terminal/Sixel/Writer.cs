// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Sixel;

using Graphics;

/// <summary>Validates and atomically writes deterministic bounded DEC sixel images.</summary>
[PublicAPI]
public static class Writer
{
    private const int _defaultMaxOutputBytes = 256 * 1024 * 1024;

    /// <summary>
    /// Writes one transparent-background sixel DCS using a deterministic 6 by 6 by 6 RGB palette.
    /// Fully transparent pixels remain unchanged; any nonzero alpha is encoded as opaque.
    /// </summary>
    /// <param name="image">The non-null owned RGBA image.</param>
    /// <param name="source">The positive source rectangle contained by the image.</param>
    /// <param name="destinationPixels">The positive output raster dimensions in exact pixels.</param>
    /// <param name="mode">The scaling and fitting mode.</param>
    /// <param name="destination">The non-null synchronous byte destination.</param>
    /// <param name="maxOutputBytes">The positive finite maximum encoded transaction size.</param>
    /// <exception cref="ArgumentNullException">A reference is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Geometry, mode, or byte policy is invalid, or the worst-case transaction exceeds policy.
    /// </exception>
    /// <exception cref="NotSupportedException"><paramref name="image"/> is not RGBA.</exception>
    public static void Write(
        Image image,
        Rect source,
        Size destinationPixels,
        PlacementMode mode,
        IBufferWriter<byte> destination,
        int maxOutputBytes = _defaultMaxOutputBytes)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxOutputBytes);

        if (image.Format != Format.Rgba)
        {
            throw new NotSupportedException("Sixel encoding accepts owned RGBA pixels only.");
        }

        ValidateSource(image, source);

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "The placement mode is unknown.");
        }

        Limits.Default.Validate(destinationPixels, sourceBytes: 0);

        using var transaction = new PreparedBuffer(maxOutputBytes);

        try
        {
            _ = Encoder.Encode(
                image,
                source,
                destinationPixels,
                mode,
                transaction,
                maxOutputBytes);
        }
        catch (InvalidOperationException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxOutputBytes),
                maxOutputBytes,
                "The sixel transaction exceeds the finite output policy.");
        }

        destination.Write(transaction.WrittenSpan);
    }

    private static void ValidateSource(Image image, Rect source)
    {
        if (source.X < 0 ||
            source.Y < 0 ||
            source.Width <= 0 ||
            source.Height <= 0 ||
            (long) source.X + source.Width > image.Size.Width ||
            (long) source.Y + source.Height > image.Size.Height)
        {
            throw new ArgumentOutOfRangeException(
                nameof(source),
                source,
                "The source must be positive and contained by the image.");
        }
    }
}
