// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Clipboard;

/// <summary>
/// Encodes and decodes bounded plain-text OSC 52 clipboard values.
/// </summary>
/// <remarks>
/// This type handles only OSC 52 text. Kitty OSC 5522 MIME transactions are a
/// separate protocol. Encoders emit ST through <see cref="ProtocolWriter"/>. Decoded
/// data is copied into owned memory, while temporary pooled buffers are cleared.
/// </remarks>
/// <example>
/// <code>
/// Osc52.Write(writer, Selection.Clipboard, "copied text"u8);
/// Osc52.Query(writer, Selection.Clipboard);
/// </code>
/// </example>
[PublicAPI]
public static class Osc52
{
    private const int _defaultMaxBytes = 1_048_576;
    private const int _stackPayloadLimit = 512;

    // The specification's own selection alphabet has 12 members (c, p, q, s, 0-7); a Pc field
    // longer than this cannot contain a new valid character and is rejected before scanning,
    // bounding direct public-API callers who bypass the parser's overall string length cap.
    private const int _maxSelectionListLength = 32;

    /// <summary>
    /// Writes UTF-8 text to one OSC 52 selection.
    /// </summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="selection">The target selection.</param>
    /// <param name="text">Borrowed valid UTF-8 bytes.</param>
    /// <param name="maxBytes">The positive raw text byte limit.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="selection"/> is unknown or <paramref name="maxBytes"/>
    /// is not positive.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="text"/> is invalid UTF-8 or exceeds
    /// <paramref name="maxBytes"/>.
    /// </exception>
    public static void Write(
        ProtocolWriter writer,
        Selection selection,
        ReadOnlySpan<byte> text,
        int maxBytes = _defaultMaxBytes)
    {
        var identifier = GetIdentifier(selection);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        if (text.Length > maxBytes)
        {
            throw new ArgumentException("Clipboard text exceeds the configured byte limit.", nameof(text));
        }

        ValidateUtf8(text, nameof(text));

        var base64Length = Base64.GetMaxEncodedToUtf8Length(text.Length);
        var length = checked(base64Length + 2);
        var rented = length > _stackPayloadLimit
            ? ArrayPool<byte>.Shared.Rent(length)
            : null;
        var payload = rented is null
            ? stackalloc byte[length]
            : rented.AsSpan();

        try
        {
            payload[0] = identifier;
            payload[1] = (byte) ';';
            var written = text.EncodeBase64OrThrow(
                payload[2..],
                "Validated clipboard bytes failed Base64 encoding.");

            writer.Osc(52, payload[..(written + 2)]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented, clearArray: true);
            }
        }
    }

    /// <summary>
    /// Requests the current text from one OSC 52 selection.
    /// </summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="selection">The target selection.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="selection"/> is unknown.
    /// </exception>
    public static void Query(ProtocolWriter writer, Selection selection)
    {
        Span<byte> payload = [GetIdentifier(selection), (byte) ';', (byte) '?'];
        writer.Osc(52, payload);
    }

    /// <summary>
    /// Decodes one raw OSC callback value including its <c>52;</c> selector.
    /// </summary>
    /// <param name="value">Borrowed OSC callback bytes.</param>
    /// <param name="maxBytes">The positive decoded text byte limit.</param>
    /// <returns>
    /// An owned success/query result, or a non-throwing malformed result for
    /// untrusted wire input.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxBytes"/> is not positive.
    /// </exception>
    [Pure]
    public static ClipboardReply Decode(
        ReadOnlySpan<byte> value,
        int maxBytes = _defaultMaxBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        if (value.Length < 4 || !value.StartsWith("52;"u8))
        {
            return Malformed();
        }

        var afterPrefix = value[3..];
        var separatorIndex = afterPrefix.IndexOf((byte) ';');

        if (separatorIndex < 0 ||
            separatorIndex > _maxSelectionListLength ||
            !TryResolveSelection(afterPrefix[..separatorIndex], out var selection))
        {
            return Malformed();
        }

        var encoded = afterPrefix[(separatorIndex + 1)..];

        if (encoded.SequenceEqual("?"u8))
        {
            return new ClipboardReply(ClipboardStatus.Query, selection, ReadOnlyMemory<byte>.Empty);
        }

        if (encoded.IsEmpty)
        {
            return new ClipboardReply(ClipboardStatus.Success, selection, ReadOnlyMemory<byte>.Empty);
        }

        if (!encoded.IsCanonicalBase64() ||
            encoded.Length > Base64.GetMaxEncodedToUtf8Length(maxBytes))
        {
            return Malformed(selection);
        }

        var maximumDecoded = Base64.GetMaxDecodedFromUtf8Length(encoded.Length);
        var rented = ArrayPool<byte>.Shared.Rent(Math.Max(1, maximumDecoded));

        try
        {
            var status = Base64.DecodeFromUtf8(
                encoded,
                rented,
                out var consumed,
                out var written);

            if (status != OperationStatus.Done ||
                consumed != encoded.Length ||
                written > maxBytes)
            {
                return Malformed(selection);
            }

            var decoded = rented.AsSpan(0, written);

            return decoded.Valid()
                ? new ClipboardReply(
                    ClipboardStatus.Success,
                    selection,
                    decoded.ToArray())
                : Malformed(selection);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    [Pure]
    private static byte GetIdentifier(Selection selection) => selection switch
    {
        Selection.Clipboard => (byte) 'c',
        Selection.Primary => (byte) 'p',
        Selection.Secondary => (byte) 'q',
        Selection.Select => (byte) 's',
        Selection.Cut0 => (byte) '0',
        Selection.Cut1 => (byte) '1',
        Selection.Cut2 => (byte) '2',
        Selection.Cut3 => (byte) '3',
        Selection.Cut4 => (byte) '4',
        Selection.Cut5 => (byte) '5',
        Selection.Cut6 => (byte) '6',
        Selection.Cut7 => (byte) '7',
        _ => throw new ArgumentOutOfRangeException(
            nameof(selection), selection, "The OSC 52 selection is unknown.")
    };

    [Pure]
    private static ClipboardReply Malformed(Selection selection = Selection.Clipboard) =>
        new(ClipboardStatus.Malformed, selection, ReadOnlyMemory<byte>.Empty);

    // The specification's Pc field carries zero or more selection characters, resolved to the
    // first entry actually present rather than a fixed offset. An empty Pc has the documented
    // meaning "the configurable primary/clipboard selection and cut-buffer 0"; Selection.Select
    // is the closer of the two for a single-value result, since it names the caller-configured
    // target rather than a specific numbered cut buffer.
    [Pure]
    private static bool TryResolveSelection(ReadOnlySpan<byte> pc, out Selection selection)
    {
        if (pc.IsEmpty)
        {
            selection = Selection.Select;
            return true;
        }

        foreach (var candidate in pc)
        {
            if (TryGetSelection(candidate, out selection))
            {
                return true;
            }
        }

        selection = default;
        return false;
    }

    [Pure]
    private static bool TryGetSelection(byte value, out Selection selection)
    {
        selection = value switch
        {
            (byte) 'c' => Selection.Clipboard,
            (byte) 'p' => Selection.Primary,
            (byte) 'q' => Selection.Secondary,
            (byte) 's' => Selection.Select,
            (byte) '0' => Selection.Cut0,
            (byte) '1' => Selection.Cut1,
            (byte) '2' => Selection.Cut2,
            (byte) '3' => Selection.Cut3,
            (byte) '4' => Selection.Cut4,
            (byte) '5' => Selection.Cut5,
            (byte) '6' => Selection.Cut6,
            (byte) '7' => Selection.Cut7,
            _ => default
        };

        return value is (byte) 'c' or (byte) 'p' or (byte) 'q' or (byte) 's' or
            (>= (byte) '0' and <= (byte) '7');
    }

    private static void ValidateUtf8(ReadOnlySpan<byte> value, string parameterName)
    {
        if (!value.Valid())
        {
            throw new ArgumentException("Clipboard text must be valid UTF-8.", parameterName);
        }
    }
}
