// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Graphics;

using System.IO.Compression;

using SharpVision.Terminal.Clipboard;
using SharpVision.Terminal.Graphics;

using MustUseReturnValue = JetBrains.Annotations.MustUseReturnValueAttribute;

/// <summary>Encodes validated direct Kitty graphics APC commands with finite canonical chunking.</summary>
[PublicAPI]
public static class KittyGraphicsWriter
{
    private const int _encodedChunkBytes = 4_096;
    private const int _rawChunkBytes = 3_072;
    private const int _metadataBytes = 512;
    private const int _defaultMaxPayloadBytes = 256 * 1024 * 1024;

    /// <summary>Gets the exact worst-case complete byte count of one encoded APC frame: opening
    /// ESC and '_' (2), 'G' (1), the metadata key/value bytes, ';' (1), the encoded chunk payload,
    /// and the closing ST (2). A route that cannot carry at least this many bytes cannot carry a
    /// single Kitty chunk regardless of how small the actual payload turns out to be.</summary>
    internal const int MaximumFrameBytes = 2 + 1 + _metadataBytes + 1 + _encodedChunkBytes + 2;

    #region Public encoding

    /// <summary>Writes one command, Base64-encoding its complete raw payload.</summary>
    /// <param name="command">The non-null validated command.</param>
    /// <param name="payload">The complete borrowed raw payload.</param>
    /// <param name="destination">The non-null synchronous destination.</param>
    /// <exception cref="ArgumentNullException">A reference is null.</exception>
    /// <exception cref="ArgumentException">
    /// Payload shape does not match the command, or the command requests zlib compression.
    /// </exception>
    public static void Write(
        KittyGraphicsCommand command,
        ReadOnlySpan<byte> payload,
        IBufferWriter<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(destination);
        ThrowIfCompressed(command);
        ValidatePayload(command, payload);

        if (payload.Length > _rawChunkBytes)
        {
            throw new ArgumentException("A single Kitty APC payload exceeds the chunk bound.", nameof(payload));
        }

        Span<byte> encoded = stackalloc byte[_encodedChunkBytes];
        var encodedLength = Encode(payload, encoded);
        WriteEncodedCore(command, encoded[..encodedLength], destination);
    }

    /// <summary>Writes one command carrying already encoded canonical Base64.</summary>
    /// <param name="command">The non-null validated command.</param>
    /// <param name="encodedPayload">Canonical Base64 no longer than 4096 bytes.</param>
    /// <param name="destination">The non-null synchronous destination.</param>
    /// <exception cref="ArgumentNullException">A reference is null.</exception>
    /// <exception cref="ArgumentException">
    /// The action cannot carry data, the command requests zlib compression, or the encoded payload
    /// is malformed, noncanonical, oversized, or does not match the command's required raw shape.
    /// </exception>
    public static void WriteEncoded(
        KittyGraphicsCommand command,
        ReadOnlySpan<byte> encodedPayload,
        IBufferWriter<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(destination);
        ThrowIfCompressed(command);

        if (command.Action is not KittyGraphicsAction.Query and not KittyGraphicsAction.Transmit
            and not KittyGraphicsAction.TransmitFrame ||
            encodedPayload.Length > _encodedChunkBytes ||
            !encodedPayload.IsCanonicalBase64())
        {
            throw new ArgumentException("The Kitty payload is not bounded canonical Base64.", nameof(encodedPayload));
        }

        Span<byte> decoded = stackalloc byte[_rawChunkBytes];
        var status = Base64.DecodeFromUtf8(
            encodedPayload,
            decoded,
            out var consumed,
            out var decodedLength);

        if (status != OperationStatus.Done || consumed != encodedPayload.Length)
        {
            throw new ArgumentException("The Kitty payload is not bounded canonical Base64.", nameof(encodedPayload));
        }

        AssertRoundTripsToTheSameCanonicalForm(decoded[..decodedLength], encodedPayload);

        ValidatePayload(command, decoded[..decodedLength]);
        WriteEncodedCore(command, encodedPayload, destination);
    }

    /// <summary>Writes a complete direct payload as independently framed Kitty chunks.</summary>
    /// <param name="command">A non-null transmit, frame transmission, or query command.</param>
    /// <param name="payload">The complete borrowed raw source.</param>
    /// <param name="destination">The non-null synchronous destination.</param>
    /// <param name="maxPayloadBytes">The positive finite total source bound.</param>
    /// <exception cref="ArgumentNullException">A reference is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxPayloadBytes"/> is not positive.</exception>
    /// <exception cref="ArgumentException">Payload size or structure is invalid.</exception>
    public static void WriteTransmission(
        KittyGraphicsCommand command,
        ReadOnlySpan<byte> payload,
        IBufferWriter<byte> destination,
        int maxPayloadBytes = _defaultMaxPayloadBytes)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPayloadBytes);

        if (command.Action is not KittyGraphicsAction.Transmit and not KittyGraphicsAction.Query
            and not KittyGraphicsAction.TransmitFrame)
        {
            throw new ArgumentException(
                "Only query, transmit, and frame transmission actions carry direct data.",
                nameof(command));
        }

        if (payload.Length > maxPayloadBytes)
        {
            throw new ArgumentException("The Kitty source exceeds the finite payload bound.", nameof(payload));
        }

        ValidatePayload(command, payload);

        if (command.Compression == KittyGraphicsCompression.Zlib)
        {
            // Compression applies only to the wire bytes chunked here: ValidatePayload above already
            // checked the caller's raw pre-compression shape against the command's declared
            // dimensions, so the raw payload is compressed once and the compressed bytes are then
            // chunked exactly like raw bytes are below. Write and WriteEncoded reject a compressed
            // command outright (see ThrowIfCompressed) rather than take this path, since neither has
            // a place to compress raw bytes or to validate already-compressed ones.
            WriteCompressedChunks(command, payload, destination);
            return;
        }

        if (payload.Length <= _rawChunkBytes)
        {
            Write(command, payload, destination);
            return;
        }

        Span<byte> encoded = stackalloc byte[_encodedChunkBytes];
        var offset = 0;
        var first = true;

        while (offset < payload.Length)
        {
            var length = Math.Min(_rawChunkBytes, payload.Length - offset);
            var more = offset + length < payload.Length;
            var encodedLength = Encode(payload.Slice(offset, length), encoded);
            var chunk = first
                ? command.WithMore(more)
                : KittyGraphicsCommand.Continuation(command.Action, more, command.Quiet);
            WriteEncodedCore(chunk, encoded[..encodedLength], destination);
            offset += length;
            first = false;
        }
    }

    #endregion

    #region Metadata and framing

    [MustUseReturnValue]
    private static int Append(Span<byte> destination, int position, ReadOnlySpan<byte> value)
    {
        value.CopyTo(destination[position..]);
        return checked(position + value.Length);
    }

    [MustUseReturnValue]
    private static int Append(Span<byte> destination, int position, byte key, int value)
    {
        destination[position++] = key;
        destination[position++] = (byte) '=';

        return !Utf8Formatter.TryFormat(value, destination[position..], out var written)
            ? throw new InvalidOperationException("A validated Kitty integer failed formatting.")
            : position + written;
    }

    [MustUseReturnValue]
    private static int Append(Span<byte> destination, int position, byte key, uint value)
    {
        destination[position++] = key;
        destination[position++] = (byte) '=';

        return !Utf8Formatter.TryFormat(value, destination[position..], out var written)
            ? throw new InvalidOperationException("A validated Kitty identifier failed formatting.")
            : position + written;
    }

    [MustUseReturnValue]
    private static int AppendField(Span<byte> destination, int position, byte key, int value)
    {
        if (position != 0)
        {
            destination[position++] = (byte) ',';
        }

        return Append(destination, position, key, value);
    }

    [MustUseReturnValue]
    private static int AppendField(Span<byte> destination, int position, byte key, uint value)
    {
        if (position != 0)
        {
            destination[position++] = (byte) ',';
        }

        return Append(destination, position, key, value);
    }

    [MustUseReturnValue]
    private static int AppendLiteralField(
        Span<byte> destination,
        int position,
        byte key,
        byte value)
    {
        if (position != 0)
        {
            destination[position++] = (byte) ',';
        }

        destination[position++] = key;
        destination[position++] = (byte) '=';
        destination[position++] = value;
        return position;
    }

    [MustUseReturnValue]
    private static int BuildMetadata(KittyGraphicsCommand command, Span<byte> destination)
    {
        var position = 0;

        if (command.Action == KittyGraphicsAction.Query)
        {
            position = AppendField(destination, position, command.UsesImageNumber ? (byte) 'I' : (byte) 'i', command.ImageId);
            position = AppendField(destination, position, (byte) 's', command.PixelSize.Width);
            position = AppendField(destination, position, (byte) 'v', command.PixelSize.Height);
            position = AppendLiteralField(destination, position, (byte) 'a', (byte) 'q');
            position = AppendLiteralField(destination, position, (byte) 't', (byte) 'd');
            return AppendField(destination, position, (byte) 'f', (int) command.Format);
        }

        if (command.Action == KittyGraphicsAction.Transmit && command.ImageId == 0)
        {
            position = AppendField(destination, position, (byte) 'm', command.More ? 1 : 0);

            return command.Quiet == 0
                ? position
                : AppendField(destination, position, (byte) 'q', command.Quiet);
        }

        if (command.Action == KittyGraphicsAction.Transmit)
        {
            position = AppendLiteralField(destination, position, (byte) 'a', (byte) 't');
            position = AppendField(destination, position, (byte) 'f', (int) command.Format);
            position = AppendLiteralField(destination, position, (byte) 't', (byte) 'd');

            if (command.Compression == KittyGraphicsCompression.Zlib)
            {
                position = AppendLiteralField(destination, position, (byte) 'o', (byte) 'z');
            }

            if (command.Format is KittyGraphicsFormat.Rgb or KittyGraphicsFormat.Rgba)
            {
                position = AppendField(destination, position, (byte) 's', command.PixelSize.Width);
                position = AppendField(destination, position, (byte) 'v', command.PixelSize.Height);
            }

            position = AppendField(destination, position, command.UsesImageNumber ? (byte) 'I' : (byte) 'i', command.ImageId);

            if (command.More)
            {
                position = AppendField(destination, position, (byte) 'm', 1);
            }

            return command.Quiet == 0
                ? position
                : AppendField(destination, position, (byte) 'q', command.Quiet);
        }

        if (command.Action == KittyGraphicsAction.Place)
        {
            position = AppendLiteralField(destination, position, (byte) 'a', (byte) 'p');
            position = AppendField(destination, position, command.UsesImageNumber ? (byte) 'I' : (byte) 'i', command.ImageId);
            position = AppendField(destination, position, (byte) 'p', command.PlacementId);
            position = AppendField(destination, position, (byte) 'x', command.Source.X);
            position = AppendField(destination, position, (byte) 'y', command.Source.Y);
            position = AppendField(destination, position, (byte) 'w', command.Source.Width);
            position = AppendField(destination, position, (byte) 'h', command.Source.Height);
            position = AppendField(destination, position, (byte) 'c', command.Destination.Width);
            position = AppendField(destination, position, (byte) 'r', command.Destination.Height);

            if (command.ZIndex != 0)
            {
                position = AppendField(destination, position, (byte) 'z', command.ZIndex);
            }

            if (command.Quiet != 0)
            {
                position = AppendField(destination, position, (byte) 'q', command.Quiet);
            }

            return command.DoNotMoveCursor
                ? AppendField(destination, position, (byte) 'C', 1)
                : position;
        }

        if (command.Action == KittyGraphicsAction.TransmitFrame && command.ImageId == 0)
        {
            position = AppendLiteralField(destination, position, (byte) 'a', (byte) 'f');
            position = AppendField(destination, position, (byte) 'm', command.More ? 1 : 0);

            return command.Quiet == 0
                ? position
                : AppendField(destination, position, (byte) 'q', command.Quiet);
        }

        if (command.Action == KittyGraphicsAction.TransmitFrame)
        {
            position = AppendLiteralField(destination, position, (byte) 'a', (byte) 'f');
            position = AppendField(destination, position, (byte) 'f', (int) command.Format);
            position = AppendLiteralField(destination, position, (byte) 't', (byte) 'd');

            if (command.Format is KittyGraphicsFormat.Rgb or KittyGraphicsFormat.Rgba)
            {
                position = AppendField(destination, position, (byte) 's', command.PixelSize.Width);
                position = AppendField(destination, position, (byte) 'v', command.PixelSize.Height);
            }

            if (command.BaseFrameId != 0)
            {
                position = AppendField(destination, position, (byte) 'c', command.BaseFrameId);
            }

            if (command.FrameOffset.X != 0)
            {
                position = AppendField(destination, position, (byte) 'x', command.FrameOffset.X);
            }

            if (command.FrameOffset.Y != 0)
            {
                position = AppendField(destination, position, (byte) 'y', command.FrameOffset.Y);
            }

            if (command.FrameGap != 0)
            {
                position = AppendField(destination, position, (byte) 'z', command.FrameGap);
            }

            position = AppendField(destination, position, command.UsesImageNumber ? (byte) 'I' : (byte) 'i', command.ImageId);

            if (command.More)
            {
                position = AppendField(destination, position, (byte) 'm', 1);
            }

            return command.Quiet == 0
                ? position
                : AppendField(destination, position, (byte) 'q', command.Quiet);
        }

        if (command.Action == KittyGraphicsAction.Animate)
        {
            position = AppendLiteralField(destination, position, (byte) 'a', (byte) 'a');
            position = AppendField(destination, position, (byte) 'i', command.ImageId);
            position = AppendField(destination, position, (byte) 's', (int) command.Control);

            if (command.LoopCount != 0)
            {
                position = AppendField(destination, position, (byte) 'v', command.LoopCount);
            }

            return command.Quiet == 0
                ? position
                : AppendField(destination, position, (byte) 'q', command.Quiet);
        }

        position = AppendLiteralField(destination, position, (byte) 'a', (byte) 'd');
        position = AppendLiteralField(
            destination,
            position,
            (byte) 'd',
            command.DeleteData ? (byte) 'I' : (byte) 'i');
        position = AppendField(destination, position, command.UsesImageNumber ? (byte) 'I' : (byte) 'i', command.ImageId);

        if (!command.DeleteData)
        {
            position = AppendField(destination, position, (byte) 'p', command.PlacementId);
        }

        return command.Quiet == 0
            ? position
            : AppendField(destination, position, (byte) 'q', command.Quiet);
    }

    [MustUseReturnValue]
    private static int Encode(ReadOnlySpan<byte> payload, Span<byte> destination) =>
        payload.EncodeBase64OrThrow(destination, "A bounded Kitty chunk failed Base64 encoding.");

    private static void WriteCompressedChunks(
        KittyGraphicsCommand command,
        ReadOnlySpan<byte> rawPayload,
        IBufferWriter<byte> destination)
    {
        var compressed = CompressZlib(rawPayload);
        Span<byte> encoded = stackalloc byte[_encodedChunkBytes];

        if (compressed.Length <= _rawChunkBytes)
        {
            var encodedLength = Encode(compressed, encoded);
            WriteEncodedCore(command.WithMore(false), encoded[..encodedLength], destination);
            return;
        }

        var offset = 0;
        var first = true;

        while (offset < compressed.Length)
        {
            var length = Math.Min(_rawChunkBytes, compressed.Length - offset);
            var more = offset + length < compressed.Length;
            var encodedLength = Encode(compressed.AsSpan(offset, length), encoded);
            var chunk = first
                ? command.WithMore(more)
                : KittyGraphicsCommand.Continuation(command.Action, more, command.Quiet);
            WriteEncodedCore(chunk, encoded[..encodedLength], destination);
            offset += length;
            first = false;
        }
    }

    private static byte[] CompressZlib(ReadOnlySpan<byte> raw)
    {
        using var compressed = new MemoryStream();

        // A fixed compression level, rather than one derived from input size or content, is what
        // makes compressing the same payload always produce byte-identical output. This mirrors
        // Graphics.Png's PNG IDAT compression technique exactly.
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        return compressed.ToArray();
    }

    [Conditional("DEBUG")]
    private static void AssertRoundTripsToTheSameCanonicalForm(
        ReadOnlySpan<byte> decoded,
        ReadOnlySpan<byte> canonicalPayload)
    {
        Span<byte> reencoded = stackalloc byte[_encodedChunkBytes];
        var reencodedLength = Encode(decoded, reencoded);

        Debug.Assert(
            reencoded[..reencodedLength].SequenceEqual(canonicalPayload),
            "A payload that decoded via Base64.DecodeFromUtf8 with status Done and full " +
            "consumption is necessarily canonical, so re-encoding it must reproduce the exact " +
            "input bytes.");
    }

    private static void ThrowIfCompressed(KittyGraphicsCommand command)
    {
        if (command.Compression == KittyGraphicsCompression.Zlib)
        {
            // Write and WriteEncoded pass their payload straight through: there is no step where
            // raw bytes could be compressed before framing, or compressed bytes could be validated
            // against the command's declared uncompressed shape. Only WriteTransmission's dedicated
            // compression path can honor an o=z command without emitting bytes that contradict
            // their own metadata (claiming zlib framing over bytes that are not actually compressed,
            // or the reverse).
            throw new ArgumentException(
                "Zlib-compressed commands can only be written through WriteTransmission.",
                nameof(command));
        }
    }

    private static void ValidatePayload(KittyGraphicsCommand command, ReadOnlySpan<byte> payload)
    {
        if (command.Action is KittyGraphicsAction.Place or KittyGraphicsAction.Delete or KittyGraphicsAction.Animate)
        {
            if (!payload.IsEmpty)
            {
                throw new ArgumentException(
                    "Placement, delete, and animation control commands cannot carry data.",
                    nameof(payload));
            }

            return;
        }

        if (command.Format == KittyGraphicsFormat.Png)
        {
            var size = payload.ReadSize();

            if (size != command.PixelSize)
            {
                throw new ArgumentException("PNG dimensions do not match the command.", nameof(payload));
            }

            return;
        }

        var bytesPerPixel = command.Format == KittyGraphicsFormat.Rgb ? 3 : 4;
        var expected = checked((long) command.PixelSize.Width * command.PixelSize.Height * bytesPerPixel);

        if (payload.Length != expected)
        {
            throw new ArgumentException("Raw pixel data length does not match command dimensions.", nameof(payload));
        }
    }

    private static void WriteEncodedCore(
        KittyGraphicsCommand command,
        ReadOnlySpan<byte> encodedPayload,
        IBufferWriter<byte> destination)
    {
        Span<byte> metadata = stackalloc byte[_metadataBytes];
        var metadataLength = BuildMetadata(command, metadata);
        var separatorLength = encodedPayload.IsEmpty ? 0 : 1;
        var length = checked(2 + 1 + metadataLength + separatorLength + encodedPayload.Length + 2);
        var output = destination.GetSpan(length);
        var position = 0;
        output[position++] = ControlBytes.Escape;
        output[position++] = (byte) '_';
        output[position++] = (byte) 'G';
        metadata[..metadataLength].CopyTo(output[position..]);
        position += metadataLength;

        if (!encodedPayload.IsEmpty)
        {
            output[position++] = (byte) ';';
            encodedPayload.CopyTo(output[position..]);
            position += encodedPayload.Length;
        }

        output[position++] = ControlBytes.Escape;
        output[position++] = (byte) '\\';
        Debug.Assert(position == length, "Kitty frame length preflight must be exact.");
        destination.Advance(length);
    }

    #endregion
}
