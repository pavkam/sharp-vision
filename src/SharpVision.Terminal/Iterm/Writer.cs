// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Iterm;

using Graphics;

/// <summary>Writes canonical bounded iTerm2 3.5 multipart inline PNG images.</summary>
[PublicAPI]
public static class Writer
{
    private const int _defaultMaxOutputBytes = 256 * 1024 * 1024;
    private const int _fileEndFrameBytes = 16;
    private const int _filePartFramingBytes = 18;
    private const int _metadataFixedBytes = 74;

    /// <summary>Gets the official maximum complete OSC sequence bytes.</summary>
    internal const int MaximumSequenceBytes = 1_048_576;

    /// <summary>Writes one complete PNG as an inline multipart transaction using canonical ST.</summary>
    /// <param name="image">The non-null owned PNG image.</param>
    /// <param name="destinationCells">The positive inline dimensions in terminal cells.</param>
    /// <param name="mode">Contain or stretch behavior.</param>
    /// <param name="destination">The non-null synchronous destination.</param>
    /// <param name="maxSequenceBytes">The positive per-OSC bound no greater than 1,048,576.</param>
    /// <param name="maxOutputBytes">The positive complete prepared transaction bound.</param>
    /// <exception cref="ArgumentNullException">A reference is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A dimension, mode, or finite policy is invalid or exceeded.</exception>
    /// <exception cref="NotSupportedException">The image is not PNG or the mode requires cropping.</exception>
    public static void Write(
        Image image,
        Size destinationCells,
        PlacementMode mode,
        IBufferWriter<byte> destination,
        int maxSequenceBytes = MaximumSequenceBytes,
        int maxOutputBytes = _defaultMaxOutputBytes)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxOutputBytes);
        ValidateSequenceLimit(maxSequenceBytes);

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "The placement mode is unknown.");
        }

        if (image.Format != Format.Png)
        {
            throw new NotSupportedException("iTerm2 inline images accept owned PNG bytes only.");
        }

        if (destinationCells.Width <= 0 || destinationCells.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(destinationCells),
                destinationCells,
                "Inline cell dimensions must be positive.");
        }

        if (mode is not PlacementMode.Contain and not PlacementMode.Stretch)
        {
            throw new NotSupportedException("iTerm2 multipart images cannot express cover cropping.");
        }

        if (!TryCalculateTransactionBytes(
                image.ByteCount,
                destinationCells,
                maxSequenceBytes,
                out var totalBytes))
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxSequenceBytes),
                maxSequenceBytes,
                "The complete iTerm2 OSC frames exceed the finite sequence policy.");
        }

        if (totalBytes > maxOutputBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxOutputBytes),
                maxOutputBytes,
                "The iTerm2 multipart transaction exceeds the finite output policy.");
        }

        using var transaction = new PreparedBuffer(maxOutputBytes);

        try
        {
            WriteTransaction(image, destinationCells, mode, transaction, maxSequenceBytes);
        }
        catch (InvalidOperationException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxOutputBytes),
                maxOutputBytes,
                "The iTerm2 multipart transaction exceeds the finite output policy.");
        }

        destination.Write(transaction.WrittenSpan);
    }

    /// <summary>Derives the largest raw payload whose complete FilePart OSC obeys a byte limit.</summary>
    /// <param name="maxSequenceBytes">The positive complete-sequence limit.</param>
    /// <returns>A positive raw chunk size divisible by three.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The limit is invalid or cannot hold data.</exception>
    internal static int GetMaximumRawPartBytes(int maxSequenceBytes)
    {
        ValidateSequenceLimit(maxSequenceBytes);
        var encodedBytes = (maxSequenceBytes - _filePartFramingBytes) / 4 * 4;

        return encodedBytes < 4
            ? throw new ArgumentOutOfRangeException(
                nameof(maxSequenceBytes),
                maxSequenceBytes,
                "The complete sequence limit cannot hold one Base64 group.")
            : checked(encodedBytes / 4 * 3);
    }

    /// <summary>Checks exact multipart frame and total bounds without producing output.</summary>
    /// <param name="image">The proposed PNG image.</param>
    /// <param name="destinationCells">The proposed positive cell dimensions.</param>
    /// <param name="mode">The proposed contain or stretch mode.</param>
    /// <param name="maxSequenceBytes">The complete per-OSC bound.</param>
    /// <param name="maxOutputBytes">The complete prepared transaction bound.</param>
    /// <returns>Whether every frame and the complete transaction fit.</returns>
    internal static bool CanWrite(
        Image image,
        Size destinationCells,
        PlacementMode mode,
        int maxSequenceBytes,
        int maxOutputBytes)
    {
        if (image.Format != Format.Png ||
            destinationCells.Width <= 0 ||
            destinationCells.Height <= 0 ||
            mode is not PlacementMode.Contain and not PlacementMode.Stretch ||
            maxSequenceBytes is <= 0 or > MaximumSequenceBytes ||
            maxOutputBytes <= 0)
        {
            return false;
        }

        try
        {
            return TryCalculateTransactionBytes(
                image.ByteCount,
                destinationCells,
                maxSequenceBytes,
                out var total) &&
                total <= maxOutputBytes;
        }
        catch (OverflowException)
        {
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    /// <summary>Calculates exact multipart bytes in constant time for validated positive metadata.</summary>
    /// <param name="sourceBytes">The positive PNG source byte count.</param>
    /// <param name="destinationCells">The positive cell dimensions.</param>
    /// <param name="maxSequenceBytes">The complete per-OSC bound.</param>
    /// <param name="totalBytes">Receives the exact transaction byte count on success.</param>
    /// <returns>Whether metadata, every part, and FileEnd fit the sequence bound without overflow.</returns>
    internal static bool TryCalculateTransactionBytes(
        int sourceBytes,
        Size destinationCells,
        int maxSequenceBytes,
        out long totalBytes)
    {
        totalBytes = 0;

        if (sourceBytes <= 0 || destinationCells.Width <= 0 || destinationCells.Height <= 0)
        {
            return false;
        }

        try
        {
            var rawChunkBytes = GetMaximumRawPartBytes(maxSequenceBytes);
            var metadataBytes = _metadataFixedBytes +
                                DecimalBytes(sourceBytes) +
                                DecimalBytes(destinationCells.Width) +
                                DecimalBytes(destinationCells.Height);

            if (metadataBytes > maxSequenceBytes || _fileEndFrameBytes > maxSequenceBytes)
            {
                return false;
            }

            var fullParts = sourceBytes / rawChunkBytes;
            var remainder = sourceBytes % rawChunkBytes;
            var fullEncodedBytes = checked(rawChunkBytes / 3 * 4);
            var total = checked(
                metadataBytes +
                _fileEndFrameBytes +
                ((long) fullParts * (fullEncodedBytes + _filePartFramingBytes)));

            if (remainder != 0)
            {
                var remainderEncodedBytes = checked((remainder + 2L) / 3L * 4L);
                total = checked(total + remainderEncodedBytes + _filePartFramingBytes);
            }

            totalBytes = total;
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static void WriteTransaction(
        Image image,
        Size destinationCells,
        PlacementMode mode,
        PreparedBuffer output,
        int maxSequenceBytes)
    {
        var start = output.WrittenCount;
        WriteBytes(output, "\u001b]1337;MultipartFile=size="u8);
        WriteInteger(output, image.ByteCount);
        WriteBytes(output, ";width="u8);
        WriteInteger(output, destinationCells.Width);
        WriteBytes(output, ";height="u8);
        WriteInteger(output, destinationCells.Height);
        WriteBytes(output, ";preserveAspectRatio="u8);
        WriteByte(output, mode == PlacementMode.Stretch ? (byte) '0' : (byte) '1');
        WriteBytes(output, ";inline=1\u001b\\"u8);
        ValidateSequenceLength(output.WrittenCount - start, maxSequenceBytes);

        var rawChunkBytes = GetMaximumRawPartBytes(maxSequenceBytes);
        var encodedCapacity = checked(rawChunkBytes / 3 * 4);
        var rented = ArrayPool<byte>.Shared.Rent(encodedCapacity);

        try
        {
            var source = image.Source;
            var offset = 0;

            while (offset < source.Length)
            {
                var length = Math.Min(rawChunkBytes, source.Length - offset);
                var sequenceStart = output.WrittenCount;
                WriteBytes(output, "\u001b]1337;FilePart="u8);
                var status = Base64.EncodeToUtf8(
                    source.Slice(offset, length),
                    rented,
                    out var consumed,
                    out var written);

                if (status != OperationStatus.Done || consumed != length)
                {
                    throw new InvalidOperationException("Validated PNG bytes failed bounded Base64 encoding.");
                }

                WriteBytes(output, rented.AsSpan(0, written));
                WriteBytes(output, "\u001b\\"u8);
                ValidateSequenceLength(output.WrittenCount - sequenceStart, maxSequenceBytes);
                offset += length;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }

        start = output.WrittenCount;
        WriteBytes(output, "\u001b]1337;FileEnd\u001b\\"u8);
        ValidateSequenceLength(output.WrittenCount - start, maxSequenceBytes);
    }

    private static void ValidateSequenceLength(int length, int maxSequenceBytes)
    {
        if (length > maxSequenceBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxSequenceBytes),
                maxSequenceBytes,
                "A complete iTerm2 OSC sequence exceeds policy.");
        }
    }

    private static void ValidateSequenceLimit(int maxSequenceBytes)
    {
        if (maxSequenceBytes is <= 0 or > MaximumSequenceBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxSequenceBytes),
                maxSequenceBytes,
                "The iTerm2 complete-sequence limit must be from 1 through 1,048,576 bytes.");
        }
    }

    private static int DecimalBytes(int value)
    {
        Debug.Assert(value > 0, "Validated iTerm2 metadata values are positive.");
        var digits = 1;

        while (value >= 10)
        {
            value /= 10;
            digits++;
        }

        return digits;
    }

    private static void WriteByte(PreparedBuffer output, byte value)
    {
        var span = output.GetSpan(1);
        span[0] = value;
        output.Advance(1);
    }

    private static void WriteBytes(PreparedBuffer output, ReadOnlySpan<byte> value)
    {
        var span = output.GetSpan(value.Length);
        value.CopyTo(span);
        output.Advance(value.Length);
    }

    private static void WriteInteger(PreparedBuffer output, int value)
    {
        Span<byte> buffer = stackalloc byte[16];

        if (!Utf8Formatter.TryFormat(value, buffer, out var written))
        {
            throw new InvalidOperationException("A validated iTerm2 integer failed formatting.");
        }

        WriteBytes(output, buffer[..written]);
    }
}
