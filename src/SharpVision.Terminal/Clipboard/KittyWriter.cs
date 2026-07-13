namespace SharpVision.Terminal.Clipboard;

using System.Buffers;
using System.Buffers.Text;
using System.Text;

using SharpVision.Terminal.Protocols;

/// <summary>
/// Encodes canonical Kitty OSC 5522 clipboard requests and MIME data packets.
/// </summary>
/// <remarks>
/// MIME data is split into independently padded Base64 chunks of at most 4096
/// raw bytes. Callers send all chunks for one MIME type before the next type and
/// finish a write with <see cref="WriteEnd"/>. Invalid requests write no bytes.
/// </remarks>
public static class KittyWriter
{
    private const int _chunkBytes = 4_096;
    private const int _defaultMaxBytes = 16_777_216;
    private const int _maxMetadataBytes = 8_192;
    private const int _stackLimit = 512;

    /// <summary>Requests one or more space-separated MIME types.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="mimeTypes">Valid space-separated UTF-8 MIME types.</param>
    /// <param name="selection">Clipboard or primary selection.</param>
    /// <param name="id">Optional multiplexer correlation identifier.</param>
    /// <param name="password">Optional UTF-8 permission password.</param>
    /// <param name="name">Required UTF-8 human name when password is supplied.</param>
    /// <exception cref="ArgumentException">A text, MIME, identifier, or credential value is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="selection"/> is unsupported.</exception>
    public static void Read(
        Writer writer,
        ReadOnlySpan<byte> mimeTypes,
        Selection selection = Selection.Clipboard,
        ReadOnlySpan<byte> id = default,
        ReadOnlySpan<byte> password = default,
        ReadOnlySpan<byte> name = default)
    {
        ValidateMimeList(mimeTypes, nameof(mimeTypes));
        WriteRequest(writer, "read"u8, selection, id, password, name, mimeTypes, hasPayload: true);
    }

    /// <summary>Requests the available MIME type list.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="selection">Clipboard or primary selection.</param>
    /// <param name="id">Optional multiplexer correlation identifier.</param>
    /// <param name="password">Optional UTF-8 permission password.</param>
    /// <param name="name">Required UTF-8 human name when password is supplied.</param>
    /// <exception cref="ArgumentException">An identifier or credential value is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="selection"/> is unsupported.</exception>
    public static void List(
        Writer writer,
        Selection selection = Selection.Clipboard,
        ReadOnlySpan<byte> id = default,
        ReadOnlySpan<byte> password = default,
        ReadOnlySpan<byte> name = default) =>
        WriteRequest(writer, "read"u8, selection, id, password, name, "."u8, hasPayload: true);

    /// <summary>Starts a MIME-aware clipboard write.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="selection">Clipboard or primary selection.</param>
    /// <param name="id">Optional multiplexer correlation identifier.</param>
    /// <param name="password">Optional UTF-8 permission password.</param>
    /// <param name="name">Required UTF-8 human name when password is supplied.</param>
    /// <exception cref="ArgumentException">An identifier or credential value is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="selection"/> is unsupported.</exception>
    public static void WriteStart(
        Writer writer,
        Selection selection = Selection.Clipboard,
        ReadOnlySpan<byte> id = default,
        ReadOnlySpan<byte> password = default,
        ReadOnlySpan<byte> name = default) =>
        WriteRequest(writer, "write"u8, selection, id, password, name, [], hasPayload: false);

    /// <summary>Writes all independently padded chunks for one MIME type.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="mime">A non-empty ASCII MIME type.</param>
    /// <param name="data">Borrowed arbitrary binary MIME data.</param>
    /// <param name="maxBytes">The positive total data bound.</param>
    /// <exception cref="ArgumentException">The MIME type is invalid or data exceeds the bound.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBytes"/> is not positive.</exception>
    public static void WriteData(
        Writer writer,
        ReadOnlySpan<byte> mime,
        ReadOnlySpan<byte> data,
        int maxBytes = _defaultMaxBytes)
    {
        ValidateMime(mime, nameof(mime));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        if (data.Length > maxBytes)
        {
            throw new ArgumentException("Clipboard data exceeds the configured byte limit.", nameof(data));
        }

        var mimeLength = Base64.GetMaxEncodedToUtf8Length(mime.Length);
        var metadataLength = checked("type=wdata:mime="u8.Length + mimeLength);

        if (metadataLength > _maxMetadataBytes)
        {
            throw new ArgumentException("Encoded MIME metadata exceeds the protocol limit.", nameof(mime));
        }

        var rented = metadataLength > _stackLimit
            ? ArrayPool<byte>.Shared.Rent(metadataLength)
            : null;
        var metadata = rented is null
            ? stackalloc byte[metadataLength]
            : rented.AsSpan();

        try
        {
            "type=wdata:mime="u8.CopyTo(metadata);
            var mimeStatus = Base64.EncodeToUtf8(
                mime,
                metadata["type=wdata:mime="u8.Length..],
                out _,
                out var mimeWritten);

            if (mimeStatus != OperationStatus.Done || mimeWritten != mimeLength)
            {
                throw new InvalidOperationException("Validated MIME metadata failed Base64 encoding.");
            }

            if (data.IsEmpty)
            {
                WritePacket(writer, metadata[..metadataLength], [], hasPayload: true);
                return;
            }

            for (var offset = 0; offset < data.Length; offset += _chunkBytes)
            {
                var length = Math.Min(_chunkBytes, data.Length - offset);
                WritePacket(
                    writer,
                    metadata[..metadataLength],
                    data.Slice(offset, length),
                    hasPayload: true);
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented, clearArray: true);
            }
        }
    }

    /// <summary>Creates aliases for a transmitted target MIME type.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="targetMime">The MIME type whose data is reused.</param>
    /// <param name="aliases">Space-separated MIME aliases.</param>
    /// <exception cref="ArgumentException">A MIME type or alias list is invalid.</exception>
    public static void WriteAlias(
        Writer writer,
        ReadOnlySpan<byte> targetMime,
        ReadOnlySpan<byte> aliases)
    {
        ValidateMime(targetMime, nameof(targetMime));
        ValidateMimeList(aliases, nameof(aliases));

        var mimeLength = Base64.GetMaxEncodedToUtf8Length(targetMime.Length);
        var metadataLength = checked("type=walias:mime="u8.Length + mimeLength);

        if (metadataLength > _maxMetadataBytes)
        {
            throw new ArgumentException("Encoded MIME metadata exceeds the protocol limit.", nameof(targetMime));
        }

        var rented = metadataLength > _stackLimit
            ? ArrayPool<byte>.Shared.Rent(metadataLength)
            : null;
        var metadata = rented is null
            ? stackalloc byte[metadataLength]
            : rented.AsSpan();

        try
        {
            "type=walias:mime="u8.CopyTo(metadata);
            var status = Base64.EncodeToUtf8(
                targetMime,
                metadata["type=walias:mime="u8.Length..],
                out _,
                out var written);

            if (status != OperationStatus.Done || written != mimeLength)
            {
                throw new InvalidOperationException("Validated MIME metadata failed Base64 encoding.");
            }

            WritePacket(writer, metadata[..metadataLength], aliases, hasPayload: true);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented, clearArray: true);
            }
        }
    }

    /// <summary>Ends the active write with an empty <c>wdata</c> packet.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    public static void WriteEnd(Writer writer) => writer.Osc(5522, "type=wdata"u8);

    /// <summary>Queries support using standard DECRQM mode 5522.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    public static void QuerySupport(Writer writer) => Csi.QueryPrivateMode(writer, 5522);

    /// <summary>Enables or disables mode 5522 paste events.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="enabled">Whether paste events are enabled.</param>
    public static void PasteEvents(Writer writer, bool enabled) =>
        Modes.ClipboardPasteEvents(writer, enabled);

    private static int AppendBase64(ReadOnlySpan<byte> value, Span<byte> destination)
    {
        var status = Base64.EncodeToUtf8(value, destination, out _, out var written);

        return status == OperationStatus.Done
            ? written
            : throw new InvalidOperationException("Validated metadata failed Base64 encoding.");
    }

    private static int AppendLiteral(ReadOnlySpan<byte> value, Span<byte> destination, int position)
    {
        value.CopyTo(destination[position..]);
        return position + value.Length;
    }

    private static bool IsIdentifier(ReadOnlySpan<byte> value)
    {
        foreach (var item in value)
        {
            if (item is not (
                (>= (byte) 'a' and <= (byte) 'z') or
                (>= (byte) 'A' and <= (byte) 'Z') or
                (>= (byte) '0' and <= (byte) '9') or
                (byte) '-' or (byte) '_' or (byte) '+' or (byte) '.'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidUtf8(ReadOnlySpan<byte> value)
    {
        while (!value.IsEmpty)
        {
            if (Rune.DecodeFromUtf8(value, out _, out var consumed) != OperationStatus.Done)
            {
                return false;
            }

            value = value[consumed..];
        }

        return true;
    }

    private static void ValidateIdentifier(ReadOnlySpan<byte> value, string parameterName)
    {
        if (!IsIdentifier(value))
        {
            throw new ArgumentException(
                "A correlation identifier contains a forbidden byte.",
                parameterName);
        }
    }

    private static void ValidateMime(ReadOnlySpan<byte> value, string parameterName)
    {
        if (value.IsEmpty)
        {
            throw new ArgumentException("A MIME type cannot be empty.", parameterName);
        }

        foreach (var item in value)
        {
            if (item is < 0x21 or > 0x7e or (byte) ':' or (byte) ';')
            {
                throw new ArgumentException("A MIME type contains a forbidden byte.", parameterName);
            }
        }
    }

    private static void ValidateMimeList(ReadOnlySpan<byte> value, string parameterName)
    {
        if (value.IsEmpty)
        {
            throw new ArgumentException("A MIME list cannot be empty.", parameterName);
        }

        var remaining = value;

        while (!remaining.IsEmpty)
        {
            var separator = remaining.IndexOf((byte) ' ');
            var mime = separator < 0 ? remaining : remaining[..separator];
            ValidateMime(mime, parameterName);
            remaining = separator < 0 ? [] : remaining[(separator + 1)..];

            if (separator >= 0 && remaining.IsEmpty)
            {
                throw new ArgumentException("A MIME list cannot end with a separator.", parameterName);
            }
        }
    }

    private static void ValidateSelection(Selection selection)
    {
        if (selection is not (Selection.Clipboard or Selection.Primary))
        {
            throw new ArgumentOutOfRangeException(
                nameof(selection),
                selection,
                "Kitty clipboard packets support clipboard or primary selection.");
        }
    }

    private static void ValidateUtf8(ReadOnlySpan<byte> value, string parameterName)
    {
        if (!IsValidUtf8(value))
        {
            throw new ArgumentException("The value must be valid UTF-8.", parameterName);
        }
    }

    private static void WritePacket(
        Writer writer,
        ReadOnlySpan<byte> metadata,
        ReadOnlySpan<byte> rawPayload,
        bool hasPayload)
    {
        var payloadLength = hasPayload
            ? Base64.GetMaxEncodedToUtf8Length(rawPayload.Length)
            : 0;
        var length = checked(metadata.Length + (hasPayload ? payloadLength + 1 : 0));
        var rented = length > _stackLimit
            ? ArrayPool<byte>.Shared.Rent(length)
            : null;
        var packet = rented is null
            ? stackalloc byte[length]
            : rented.AsSpan();

        try
        {
            metadata.CopyTo(packet);
            var written = metadata.Length;

            if (hasPayload)
            {
                packet[written++] = (byte) ';';
                written += AppendBase64(rawPayload, packet[written..]);
            }

            writer.Osc(5522, packet[..written]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented, clearArray: true);
            }
        }
    }

    private static void WriteRequest(
        Writer writer,
        ReadOnlySpan<byte> type,
        Selection selection,
        ReadOnlySpan<byte> id,
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> name,
        ReadOnlySpan<byte> rawPayload,
        bool hasPayload)
    {
        ValidateSelection(selection);
        ValidateIdentifier(id, nameof(id));
        ValidateUtf8(password, nameof(password));
        ValidateUtf8(name, nameof(name));

        if (!password.IsEmpty && name.IsEmpty)
        {
            throw new ArgumentException("A password requires a human-readable name.", nameof(name));
        }

        var passwordLength = Base64.GetMaxEncodedToUtf8Length(password.Length);
        var nameLength = Base64.GetMaxEncodedToUtf8Length(name.Length);
        var length = checked(
            "type="u8.Length + type.Length +
            (selection == Selection.Primary ? ":loc=primary"u8.Length : 0) +
            (id.IsEmpty ? 0 : ":id="u8.Length + id.Length) +
            (password.IsEmpty ? 0 : ":pw="u8.Length + passwordLength) +
            (name.IsEmpty ? 0 : ":name="u8.Length + nameLength));

        if (length > _maxMetadataBytes)
        {
            throw new ArgumentException("Encoded clipboard metadata exceeds the protocol limit.");
        }

        var rented = length > _stackLimit
            ? ArrayPool<byte>.Shared.Rent(length)
            : null;
        var metadata = rented is null
            ? stackalloc byte[length]
            : rented.AsSpan();

        try
        {
            var position = AppendLiteral("type="u8, metadata, 0);
            position = AppendLiteral(type, metadata, position);

            if (selection == Selection.Primary)
            {
                position = AppendLiteral(":loc=primary"u8, metadata, position);
            }

            if (!id.IsEmpty)
            {
                position = AppendLiteral(":id="u8, metadata, position);
                position = AppendLiteral(id, metadata, position);
            }

            if (!password.IsEmpty)
            {
                position = AppendLiteral(":pw="u8, metadata, position);
                position += AppendBase64(password, metadata[position..]);
            }

            if (!name.IsEmpty)
            {
                position = AppendLiteral(":name="u8, metadata, position);
                position += AppendBase64(name, metadata[position..]);
            }

            WritePacket(writer, metadata[..position], rawPayload, hasPayload);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented, clearArray: true);
            }
        }
    }
}
