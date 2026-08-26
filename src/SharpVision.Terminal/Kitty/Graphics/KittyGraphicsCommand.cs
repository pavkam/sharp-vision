// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Graphics;

using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>Owns one validated protocol-safe Kitty graphics command description.</summary>
[PublicAPI]
public sealed class KittyGraphicsCommand
{
    #region Construction and state

    private KittyGraphicsCommand(
        KittyGraphicsAction action,
        KittyGraphicsMedium medium,
        KittyGraphicsFormat format,
        KittyGraphicsCompression compression,
        uint imageId,
        uint placementId,
        Size pixelSize,
        Rect source,
        Size destination,
        int zIndex,
        int quiet,
        bool doNotMoveCursor,
        bool more,
        bool deleteData = false,
        Point frameOffset = default,
        uint baseFrameId = 0,
        int frameGap = 0,
        KittyGraphicsAnimationControl control = default,
        int loopCount = 0)
    {
        Action = action;
        Medium = medium;
        Format = format;
        Compression = compression;
        ImageId = imageId;
        PlacementId = placementId;
        PixelSize = pixelSize;
        Source = source;
        Destination = destination;
        ZIndex = zIndex;
        Quiet = quiet;
        DoNotMoveCursor = doNotMoveCursor;
        More = more;
        DeleteData = deleteData;
        FrameOffset = frameOffset;
        BaseFrameId = baseFrameId;
        FrameGap = frameGap;
        Control = control;
        LoopCount = loopCount;
    }

    /// <summary>Gets the typed action.</summary>
    public KittyGraphicsAction Action { get; }

    /// <summary>Gets the direct-only transmission medium.</summary>
    public KittyGraphicsMedium Medium { get; }

    /// <summary>Gets the image source format.</summary>
    public KittyGraphicsFormat Format { get; }

    /// <summary>Gets transmission compression: none or zlib for a transmit command, always none otherwise.</summary>
    public KittyGraphicsCompression Compression { get; }

    /// <summary>Gets the nonzero renderer-owned image identifier.</summary>
    public uint ImageId { get; }

    /// <summary>Gets the nonzero renderer-owned placement identifier, or zero when absent.</summary>
    public uint PlacementId { get; }

    /// <summary>Gets positive transmitted pixel dimensions.</summary>
    public Size PixelSize { get; }

    /// <summary>Gets the positive source pixel rectangle for placement.</summary>
    public Rect Source { get; }

    /// <summary>Gets the positive destination cell size for placement.</summary>
    public Size Destination { get; }

    /// <summary>Gets the signed Kitty z-index.</summary>
    public int ZIndex { get; }

    /// <summary>Gets response suppression mode zero, one, or two.</summary>
    [ValueRange(0, 2)]
    public int Quiet { get; }

    /// <summary>Gets whether placement uses the Kitty <c>C=1</c> no-cursor-movement policy.</summary>
    public bool DoNotMoveCursor { get; }

    /// <summary>Gets whether another direct payload chunk follows.</summary>
    public bool More { get; }

    /// <summary>Gets whether delete frees image data rather than one exact placement.</summary>
    public bool DeleteData { get; }

    /// <summary>Gets the frame data placement offset within the image, used only by frame transmission.</summary>
    public Point FrameOffset { get; }

    /// <summary>
    /// Gets the nonzero base frame identifier this frame composites onto, or zero for a transparent black
    /// base, used only by frame transmission.
    /// </summary>
    public uint BaseFrameId { get; }

    /// <summary>
    /// Gets the frame gap in milliseconds before the next frame, where zero is ignored and negative values
    /// create a gapless frame, used only by frame transmission.
    /// </summary>
    public int FrameGap { get; }

    /// <summary>Gets the animation playback control sub-action, used only by animation control.</summary>
    public KittyGraphicsAnimationControl Control { get; }

    /// <summary>
    /// Gets the raw Kitty loop count value, used only by animation control: zero is ignored, one plays
    /// infinitely, and a larger value plays that many loops minus one.
    /// </summary>
    public int LoopCount { get; }

    #endregion

    #region Factories

    /// <summary>Creates the official one-pixel direct RGB support query.</summary>
    /// <param name="imageId">The nonzero correlation identifier.</param>
    /// <returns>A validated query command.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="imageId"/> is zero.</exception>
    [Pure]
    public static KittyGraphicsCommand Query(uint imageId)
    {
        RequireId(imageId, nameof(imageId));
        return new KittyGraphicsCommand(
            KittyGraphicsAction.Query,
            KittyGraphicsMedium.Direct,
            KittyGraphicsFormat.Rgb,
            KittyGraphicsCompression.None,
            imageId,
            0,
            new Size(1, 1),
            default,
            default,
            0,
            0,
            doNotMoveCursor: false,
            more: false);
    }

    /// <summary>Creates one soft delete for an exact image and placement pair.</summary>
    /// <param name="imageId">The nonzero image identifier.</param>
    /// <param name="placementId">The nonzero placement identifier.</param>
    /// <param name="quiet">Response suppression mode zero, one, or two.</param>
    /// <returns>A validated exact-placement delete command.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An identifier is zero or <paramref name="quiet"/> is invalid.</exception>
    [Pure]
    public static KittyGraphicsCommand DeletePlacement(uint imageId, uint placementId, int quiet = 2)
    {
        RequireId(imageId, nameof(imageId));
        RequireId(placementId, nameof(placementId));
        ValidateQuiet(quiet);
        return new KittyGraphicsCommand(
            KittyGraphicsAction.Delete,
            KittyGraphicsMedium.Direct,
            KittyGraphicsFormat.Rgba,
            KittyGraphicsCompression.None,
            imageId,
            placementId,
            default,
            default,
            default,
            0,
            quiet,
            doNotMoveCursor: false,
            more: false);
    }

    /// <summary>Creates one direct bounded image transmission command.</summary>
    /// <param name="imageId">The nonzero renderer-owned image identifier.</param>
    /// <param name="pixelSize">Positive dimensions no greater than 16,384 on either axis.</param>
    /// <param name="format">RGB, RGBA, or PNG.</param>
    /// <param name="quiet">Response suppression mode zero, one, or two.</param>
    /// <param name="medium">The transmission medium; only direct is supported.</param>
    /// <param name="compression">The transmission compression; none or zlib.</param>
    /// <returns>A validated transmission command.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An identifier, dimension, enum, or quiet value is invalid.</exception>
    /// <exception cref="NotSupportedException">The medium is unsupported.</exception>
    [Pure]
    public static KittyGraphicsCommand Transmit(
        uint imageId,
        Size pixelSize,
        KittyGraphicsFormat format,
        int quiet = 2,
        KittyGraphicsMedium medium = KittyGraphicsMedium.Direct,
        KittyGraphicsCompression compression = KittyGraphicsCompression.None)
    {
        RequireId(imageId, nameof(imageId));
        ValidateSize(pixelSize, nameof(pixelSize));
        ValidateFormat(format);
        ValidateQuiet(quiet);
        ValidateCompression(compression);

        return !Enum.IsDefined(medium)
            ? throw new ArgumentOutOfRangeException(nameof(medium), medium, "The Kitty medium is unknown.")
            : medium != KittyGraphicsMedium.Direct
            ? throw new NotSupportedException("Only direct Kitty graphics transmission is supported.")
            : new KittyGraphicsCommand(
            KittyGraphicsAction.Transmit,
            medium,
            format,
            compression,
            imageId,
            0,
            pixelSize,
            default,
            default,
            0,
            quiet,
            doNotMoveCursor: false,
            more: false);
    }

    /// <summary>Creates one retained placement or update command.</summary>
    /// <param name="imageId">The nonzero image identifier.</param>
    /// <param name="placementId">The nonzero placement identifier.</param>
    /// <param name="source">The positive source pixel rectangle.</param>
    /// <param name="destination">The positive destination cell size.</param>
    /// <param name="zIndex">The signed stacking order.</param>
    /// <param name="quiet">Response suppression mode zero, one, or two.</param>
    /// <param name="doNotMoveCursor">Whether to emit <c>C=1</c>.</param>
    /// <returns>A validated placement command.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An identifier, rectangle, dimension, or quiet value is invalid.</exception>
    [Pure]
    public static KittyGraphicsCommand Place(
        uint imageId,
        uint placementId,
        Rect source,
        Size destination,
        int zIndex = 0,
        int quiet = 2,
        bool doNotMoveCursor = true)
    {
        RequireId(imageId, nameof(imageId));
        RequireId(placementId, nameof(placementId));
        ValidateRect(source, nameof(source));
        ValidateSize(destination, nameof(destination));
        ValidateQuiet(quiet);
        return new KittyGraphicsCommand(
            KittyGraphicsAction.Place,
            KittyGraphicsMedium.Direct,
            KittyGraphicsFormat.Rgba,
            KittyGraphicsCompression.None,
            imageId,
            placementId,
            default,
            source,
            destination,
            zIndex,
            quiet,
            doNotMoveCursor,
            more: false);
    }

    /// <summary>Creates one hard delete that frees image data and all placements.</summary>
    /// <param name="imageId">The nonzero image identifier.</param>
    /// <param name="quiet">Response suppression mode zero, one, or two.</param>
    /// <returns>A validated delete command.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="imageId"/> is zero or <paramref name="quiet"/> is invalid.</exception>
    [Pure]
    public static KittyGraphicsCommand DeleteImage(uint imageId, int quiet = 2)
    {
        RequireId(imageId, nameof(imageId));
        ValidateQuiet(quiet);
        return new KittyGraphicsCommand(
            KittyGraphicsAction.Delete,
            KittyGraphicsMedium.Direct,
            KittyGraphicsFormat.Rgba,
            KittyGraphicsCompression.None,
            imageId,
            0,
            default,
            default,
            default,
            0,
            quiet,
            doNotMoveCursor: false,
            more: false,
            deleteData: true);
    }

    /// <summary>Creates one direct bounded animation frame transmission command.</summary>
    /// <param name="imageId">The nonzero renderer-owned image identifier the frame belongs to.</param>
    /// <param name="pixelSize">Positive dimensions no greater than 16,384 on either axis for the frame data.</param>
    /// <param name="format">RGBA or PNG; RGB is reserved for the official query.</param>
    /// <param name="baseFrameId">
    /// The nonzero frame this frame composites onto, or zero for a transparent black base.
    /// </param>
    /// <param name="offset">The non-negative left/top edge, in pixels, where the frame data is placed.</param>
    /// <param name="frameGap">
    /// The gap in milliseconds before the next frame; zero is ignored and negative values are gapless.
    /// </param>
    /// <param name="quiet">Response suppression mode zero, one, or two.</param>
    /// <param name="medium">The transmission medium; only direct is supported.</param>
    /// <param name="compression">The transmission compression; only none is supported.</param>
    /// <returns>A validated frame transmission command.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An identifier, dimension, offset, enum, or quiet value is invalid.
    /// </exception>
    /// <exception cref="NotSupportedException">The format, medium, or compression is unsupported.</exception>
    [Pure]
    public static KittyGraphicsCommand TransmitFrame(
        uint imageId,
        Size pixelSize,
        KittyGraphicsFormat format,
        uint baseFrameId = 0,
        Point offset = default,
        int frameGap = 0,
        int quiet = 2,
        KittyGraphicsMedium medium = KittyGraphicsMedium.Direct,
        KittyGraphicsCompression compression = KittyGraphicsCompression.None)
    {
        RequireId(imageId, nameof(imageId));
        ValidateSize(pixelSize, nameof(pixelSize));
        ValidateFormat(format);
        ValidateOffset(offset, nameof(offset));
        ValidateQuiet(quiet);
        ValidateCompression(compression);

        return !Enum.IsDefined(medium)
            ? throw new ArgumentOutOfRangeException(nameof(medium), medium, "The Kitty medium is unknown.")
            : format == KittyGraphicsFormat.Rgb
            ? throw new NotSupportedException("RGB is supported only by the Kitty capability query.")
            : compression != KittyGraphicsCompression.None
            ? throw new NotSupportedException("Compressed Kitty transmission is not supported.")
            : medium != KittyGraphicsMedium.Direct
            ? throw new NotSupportedException("Only direct Kitty graphics transmission is supported.")
            : new KittyGraphicsCommand(
            KittyGraphicsAction.TransmitFrame,
            medium,
            format,
            compression,
            imageId,
            0,
            pixelSize,
            default,
            default,
            0,
            quiet,
            doNotMoveCursor: false,
            more: false,
            frameOffset: offset,
            baseFrameId: baseFrameId,
            frameGap: frameGap);
    }

    /// <summary>Creates one animation playback control command.</summary>
    /// <param name="imageId">The nonzero image identifier whose animation is controlled.</param>
    /// <param name="control">The playback control sub-action.</param>
    /// <param name="loopCount">
    /// The raw Kitty loop count: zero is ignored, one plays infinitely, and a larger value plays that many
    /// loops minus one.
    /// </param>
    /// <param name="quiet">Response suppression mode zero, one, or two.</param>
    /// <returns>A validated animation control command.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="imageId"/> is zero, <paramref name="control"/> is unknown, <paramref name="loopCount"/>
    /// is negative, or <paramref name="quiet"/> is invalid.
    /// </exception>
    [Pure]
    public static KittyGraphicsCommand Animate(
        uint imageId,
        KittyGraphicsAnimationControl control,
        int loopCount = 0,
        int quiet = 2)
    {
        RequireId(imageId, nameof(imageId));
        ValidateAnimationControl(control);
        ArgumentOutOfRangeException.ThrowIfNegative(loopCount);
        ValidateQuiet(quiet);
        return new KittyGraphicsCommand(
            KittyGraphicsAction.Animate,
            KittyGraphicsMedium.Direct,
            KittyGraphicsFormat.Rgba,
            KittyGraphicsCompression.None,
            imageId,
            0,
            default,
            default,
            default,
            0,
            quiet,
            doNotMoveCursor: false,
            more: false,
            control: control,
            loopCount: loopCount);
    }

    /// <summary>Creates the first transmission chunk with an exact continuation flag.</summary>
    [Pure]
    internal KittyGraphicsCommand WithMore(bool more) => new(
        Action,
        Medium,
        Format,
        Compression,
        ImageId,
        PlacementId,
        PixelSize,
        Source,
        Destination,
        ZIndex,
        Quiet,
        DoNotMoveCursor,
        more,
        DeleteData,
        FrameOffset,
        BaseFrameId,
        FrameGap);

    /// <summary>Creates a metadata-minimal continuation chunk for the given data-bearing action.</summary>
    [Pure]
    internal static KittyGraphicsCommand Continuation(KittyGraphicsAction action, bool more, int quiet) => new(
        action,
        KittyGraphicsMedium.Direct,
        KittyGraphicsFormat.Rgba,
        KittyGraphicsCompression.None,
        0,
        0,
        default,
        default,
        default,
        0,
        quiet,
        doNotMoveCursor: false,
        more);

    #endregion

    #region Validation

    private static void RequireId(uint value, string name)
    {
        if (value == 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "A Kitty identifier must be nonzero.");
        }
    }

    private static void ValidateAnimationControl(KittyGraphicsAnimationControl value) =>
        ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The animation control sub-action is unknown.");

    private static void ValidateCompression(KittyGraphicsCompression value) =>
        ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The compression kind is unknown.");

    private static void ValidateFormat(KittyGraphicsFormat value) =>
        ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The image format is unknown.");

    private static void ValidateQuiet(int value)
    {
        if (value is < 0 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Quiet mode must be zero, one, or two.");
        }
    }

    private static void ValidateOffset(Point value, string name)
    {
        if (value.X < 0 || value.Y < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "The frame offset must be non-negative.");
        }
    }

    private static void ValidateRect(Rect value, string name)
    {
        if (value.X < 0 || value.Y < 0 || value.Width <= 0 || value.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "The source rectangle must be positive.");
        }
    }

    private static void ValidateSize(Size value, string name)
    {
        if (value.Width is <= 0 or > 16_384 || value.Height is <= 0 or > 16_384)
        {
            throw new ArgumentOutOfRangeException(name, value, "Kitty dimensions must be one through 16,384.");
        }
    }

    #endregion
}
