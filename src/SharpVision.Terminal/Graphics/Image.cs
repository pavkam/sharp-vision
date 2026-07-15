// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics;


/// <summary>Owns one immutable bounded RGBA or encoded PNG terminal image.</summary>
public sealed class Image
{
    private static long _nextIdentity;
    private readonly byte[] _source;

    private Image(Size size, Format format, ReadOnlySpan<byte> source)
    {
        Size = size;
        Format = format;
        _source = source.ToArray();
        Identity = unchecked((ulong) Interlocked.Increment(ref _nextIdentity));
    }

    /// <summary>Gets the nonzero stable process-local image identity.</summary>
    public ulong Identity { get; }

    /// <summary>Gets the positive image dimensions in pixels.</summary>
    public Size Size { get; }

    /// <summary>Gets the owned source representation.</summary>
    public Format Format { get; }

    /// <summary>Gets the exact owned source byte count.</summary>
    public int ByteCount => _source.Length;

    /// <summary>Creates an image by copying complete sRGB RGBA pixels.</summary>
    /// <param name="size">The positive dimensions in pixels.</param>
    /// <param name="source">Exactly four bytes per pixel in RGBA order.</param>
    /// <param name="limits">The optional finite policy.</param>
    /// <returns>An independently owned immutable image.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Dimensions, pixels, or bytes exceed policy.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> is not exactly four bytes per pixel.</exception>
    public static Image FromRgba(Size size, ReadOnlySpan<byte> source, Limits? limits = null)
    {
        var policy = limits ?? Limits.Default;
        policy.Validate(size, source.Length);
        var expected = checked((long) size.Width * size.Height * 4);

        return source.Length != expected
            ? throw new ArgumentException(
                "RGBA source length must equal width times height times four.",
                nameof(source))
            : new Image(size, Format.Rgba, source);
    }

    /// <summary>Creates an image by copying a structurally validated PNG container.</summary>
    /// <param name="source">The complete encoded PNG bytes.</param>
    /// <param name="limits">The optional finite policy.</param>
    /// <returns>An independently owned immutable image.</returns>
    /// <exception cref="ArgumentException">The PNG container or IHDR fields are invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Dimensions, pixels, or bytes exceed policy.</exception>
    public static Image FromPng(ReadOnlySpan<byte> source, Limits? limits = null)
    {
        var policy = limits ?? Limits.Default;

        if (source.Length > policy.MaxSourceBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(source),
                source.Length,
                "The image source byte count exceeds policy.");
        }

        var size = Png.ReadSize(source);
        policy.Validate(size, source.Length);
        return new Image(size, Format.Png, source);
    }

    /// <summary>Copies the complete owned source into caller-provided memory.</summary>
    /// <param name="destination">The destination with at least <see cref="ByteCount"/> bytes.</param>
    /// <returns>The exact copied byte count.</returns>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is too short.</exception>
    public int CopyTo(Span<byte> destination)
    {
        if (destination.Length < _source.Length)
        {
            throw new ArgumentException("The destination cannot hold the complete image source.", nameof(destination));
        }

        _source.CopyTo(destination);
        return _source.Length;
    }

    /// <summary>Gets borrowed bytes valid for this immutable image's lifetime.</summary>
    internal ReadOnlySpan<byte> Source => _source;
}
