// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics;

/// <summary>
/// Describes one immutable image source projected into clipped terminal-cell bounds, or the valid
/// image-free <see cref="Empty"/> sentinel represented by the default value.
/// </summary>
[PublicAPI]
public readonly struct Placement: IEquatable<Placement>
{
    /// <summary>Gets the valid empty sentinel containing no image or geometry.</summary>
    public static Placement Empty => default;

    /// <summary>Initializes a validated semantic placement.</summary>
    /// <param name="image">The immutable image retained by the containing frame.</param>
    /// <param name="source">The positive pixel rectangle contained by the image.</param>
    /// <param name="destination">The positive visible rectangle in terminal cells.</param>
    /// <param name="mode">The semantic fitting mode interpreted by a graphics backend.</param>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A rectangle is empty, the source is outside the image, or <paramref name="mode"/> is unknown.
    /// </exception>
    public Placement(Image image, Rect source, Rect destination, PlacementMode mode)
        : this(image, source, destination, mode, paintRevision: null)
    {
    }

    /// <summary>Initializes a placement with frame-local paint provenance.</summary>
    /// <param name="image">The immutable image retained by the containing frame.</param>
    /// <param name="source">The positive pixel rectangle contained by the image.</param>
    /// <param name="destination">The positive visible rectangle in terminal cells.</param>
    /// <param name="mode">The semantic fitting mode interpreted by a graphics backend.</param>
    /// <param name="paintRevision">The frame mutation revision immediately before image paint.</param>
    internal Placement(
        Image image,
        Rect source,
        Rect destination,
        PlacementMode mode,
        ulong paintRevision)
        : this(image, source, destination, mode, (ulong?) paintRevision)
    {
    }

    private Placement(
        Image image,
        Rect source,
        Rect destination,
        PlacementMode mode,
        ulong? paintRevision)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "The placement mode is unknown.");
        }

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

        if (destination.X < 0 ||
            destination.Y < 0 ||
            destination.Width <= 0 ||
            destination.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(destination),
                destination,
                "The visible cell destination must be positive.");
        }

        Image = image;
        Source = source;
        Destination = destination;
        Mode = mode;
        PaintRevision = paintRevision;
    }

    /// <summary>Gets whether this value is the valid empty sentinel.</summary>
    public bool IsEmpty => Image is null;

    /// <summary>Gets the immutable retained image, or null for <see cref="Empty"/>.</summary>
    public Image? Image { get; }

    /// <summary>Gets the stable process-local identity, or zero for <see cref="Empty"/>.</summary>
    public ulong ImageIdentity => Image?.Identity ?? 0;

    /// <summary>Gets the positive source rectangle in image pixels, or an empty rectangle for <see cref="Empty"/>.</summary>
    public Rect Source { get; }

    /// <summary>
    /// Gets the positive clipped destination rectangle in terminal cells, or an empty rectangle
    /// for <see cref="Empty"/>.
    /// </summary>
    public Rect Destination { get; }

    /// <summary>Gets the semantic fitting mode; <see cref="Empty"/> uses <see cref="PlacementMode.Contain"/>.</summary>
    public PlacementMode Mode { get; }

    /// <summary>Gets frame-private paint provenance, or null for an externally constructed placement.</summary>
    internal ulong? PaintRevision { get; }

    /// <summary>Returns the same public placement semantics with frame-local paint provenance.</summary>
    /// <param name="paintRevision">The frame mutation revision immediately before image paint.</param>
    /// <returns>A placement whose public equality and hash remain unchanged.</returns>
    internal Placement WithPaintRevision(ulong paintRevision) => new(
        Image!,
        Source,
        Destination,
        Mode,
        paintRevision);

    /// <inheritdoc />
    public bool Equals(Placement other) =>
        ImageIdentity == other.ImageIdentity &&
        Source == other.Source &&
        Destination == other.Destination &&
        Mode == other.Mode;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Placement other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(ImageIdentity, Source, Destination, Mode);

    /// <summary>Compares complete semantic placement state.</summary>
    public static bool operator ==(Placement left, Placement right) => left.Equals(right);

    /// <summary>Compares complete semantic placement state.</summary>
    public static bool operator !=(Placement left, Placement right) => !left.Equals(right);
}
