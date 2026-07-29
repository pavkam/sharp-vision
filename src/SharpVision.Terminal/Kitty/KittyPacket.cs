// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty;

using Clipboard;

/// <summary>
/// Represents an immutable, redaction-safe Kitty OSC 5522 packet.
/// </summary>
[PublicAPI]
public sealed class KittyPacket
{
    #region Construction and properties

    private KittyPacket(
        bool isValid,
        KittyOperation operation,
        KittyReplyStatus replyStatus,
        Selection selection,
        string? id,
        ReadOnlyMemory<byte> mime,
        ReadOnlyMemory<byte> password,
        ReadOnlyMemory<byte> name,
        ReadOnlyMemory<byte> data,
        bool hasPayload,
        string[] unknownKeys,
        Diagnostic? diagnostic)
    {
        Debug.Assert(unknownKeys is not null, "Packet construction always owns a non-null unknown-key array.");
        Debug.Assert(isValid == (diagnostic is null), "Exactly invalid packets carry a structural diagnostic.");

        IsValid = isValid;
        Operation = operation;
        ReplyStatus = replyStatus;
        Selection = selection;
        Id = id;
        Mime = mime;
        Password = password;
        Name = name;
        Data = data;
        HasPayload = hasPayload;
        UnknownKeys = unknownKeys;
        Diagnostic = diagnostic;
    }

    /// <summary>Gets whether the packet passed grammar and bound checks.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the typed packet operation.</summary>
    public KittyOperation Operation { get; }

    /// <summary>Gets the typed response status.</summary>
    public KittyReplyStatus ReplyStatus { get; }

    /// <summary>Gets the clipboard or primary selection.</summary>
    public Selection Selection { get; }

    /// <summary>Gets the optional sanitized multiplexer correlation identifier.</summary>
    public string? Id { get; }

    /// <summary>Gets owned decoded MIME type bytes.</summary>
    public ReadOnlyMemory<byte> Mime { get; }

    /// <summary>Gets owned decoded password bytes; never included in text output.</summary>
    public ReadOnlyMemory<byte> Password { get; }

    /// <summary>Gets owned decoded human-readable name bytes.</summary>
    public ReadOnlyMemory<byte> Name { get; }

    /// <summary>
    /// Gets owned decoded payload bytes when payload decoding was requested.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>Gets whether the wire packet included a payload separator.</summary>
    public bool HasPayload { get; }

    /// <summary>Gets unknown metadata key names without their untrusted values.</summary>
    public IReadOnlyList<string> UnknownKeys { get; }

    /// <summary>Gets the non-sensitive diagnostic for an invalid packet.</summary>
    public Diagnostic? Diagnostic { get; }

    #endregion

    #region Parsing

    /// <summary>
    /// Parses a bounded raw OSC callback value including the <c>5522;</c>
    /// selector.
    /// </summary>
    /// <param name="value">Borrowed raw OSC callback bytes.</param>
    /// <param name="limits">Optional immutable protocol limits.</param>
    /// <param name="decodePayload">
    /// Whether to decode the Base64 payload into owned memory.
    /// </param>
    /// <returns>A valid typed packet or a redacted diagnostic packet.</returns>
    public static KittyPacket Parse(
        ReadOnlySpan<byte> value,
        Limits? limits = null,
        bool decodePayload = true)
    {
        var effectiveLimits = limits ?? Limits.Default;

        if (!value.StartsWith("5522;"u8))
        {
            return Invalid(DiagnosticCode.InvalidMetadata, value.Length);
        }

        var body = value[5..];
        var separator = body.IndexOf((byte) ';');
        var metadata = separator < 0 ? body : body[..separator];
        var payload = separator < 0 ? [] : body[(separator + 1)..];
        var hasPayload = separator >= 0;

        if (metadata.IsEmpty || metadata.Length > effectiveLimits.MaxMetadataBytes)
        {
            return Invalid(DiagnosticCode.InvalidMetadata, value.Length);
        }

        var operation = KittyOperation.None;
        var replyStatus = KittyReplyStatus.None;
        var selection = Selection.Clipboard;
        string? id = null;
        byte[] mime = [];
        byte[] password = [];
        byte[] name = [];
        List<string> unknownKeys = [];
        var seenType = false;
        var seenStatus = false;
        var seenLocation = false;
        var seenId = false;
        var seenMime = false;
        var seenPassword = false;
        var seenName = false;

        // Metadata is a flat, order-independent map on the wire. Parse it in
        // one pass while the seen flags reject duplicate semantic fields before
        // any decoded payload is published by the immutable result.
        while (!metadata.IsEmpty)
        {
            var next = metadata.IndexOf((byte) ':');
            var field = next < 0 ? metadata : metadata[..next];
            metadata = next < 0 ? [] : metadata[(next + 1)..];
            var equals = field.IndexOf((byte) '=');

            if (equals <= 0 || equals == field.Length - 1)
            {
                return Invalid(DiagnosticCode.InvalidMetadata, value.Length, id);
            }

            var key = field[..equals];
            var fieldValue = field[(equals + 1)..];

            if (!IsMetadataAscii(key) || !IsMetadataAscii(fieldValue))
            {
                return Invalid(DiagnosticCode.InvalidMetadata, value.Length, id);
            }

            if (key.SequenceEqual("type"u8))
            {
                if (seenType || !TryParseOperation(fieldValue, out operation))
                {
                    return Invalid(DiagnosticCode.InvalidMetadata, value.Length, id);
                }

                seenType = true;
            }
            else if (key.SequenceEqual("status"u8))
            {
                if (seenStatus || !TryParseStatus(fieldValue, out replyStatus))
                {
                    return Invalid(DiagnosticCode.InvalidMetadata, value.Length, id);
                }

                seenStatus = true;
            }
            else if (key.SequenceEqual("loc"u8))
            {
                if (seenLocation || !TryParseSelection(fieldValue, out selection))
                {
                    return Invalid(DiagnosticCode.InvalidMetadata, value.Length, id);
                }

                seenLocation = true;
            }
            else if (key.SequenceEqual("id"u8))
            {
                if (seenId || !IsIdentifier(fieldValue))
                {
                    // The packet's own id assertion is what's in question here
                    // (malformed or a conflicting duplicate) — it cannot be
                    // trusted for correlation, so it is not retained.
                    return Invalid(DiagnosticCode.InvalidMetadata, value.Length, id: null);
                }

                id = Encoding.ASCII.GetString(fieldValue);
                seenId = true;
            }
            else if (key.SequenceEqual("mime"u8))
            {
                if (seenMime || !TryDecodeUtf8(fieldValue, effectiveLimits.MaxMetadataBytes, out mime))
                {
                    return Invalid(DiagnosticCode.InvalidBase64, value.Length, id);
                }

                seenMime = true;
            }
            else if (key.SequenceEqual("pw"u8))
            {
                if (seenPassword ||
                    !TryDecodeUtf8(fieldValue, effectiveLimits.MaxMetadataBytes, out password))
                {
                    return Invalid(DiagnosticCode.InvalidBase64, value.Length, id);
                }

                seenPassword = true;
            }
            else if (key.SequenceEqual("name"u8))
            {
                if (seenName || !TryDecodeUtf8(fieldValue, effectiveLimits.MaxMetadataBytes, out name))
                {
                    return Invalid(
                        IsCanonicalBase64(fieldValue)
                            ? DiagnosticCode.InvalidMetadata
                            : DiagnosticCode.InvalidBase64,
                        value.Length,
                        id);
                }

                seenName = true;
            }
            else
            {
                unknownKeys.Add(Encoding.ASCII.GetString(key));
            }
        }

        if (!seenType)
        {
            return Invalid(DiagnosticCode.InvalidMetadata, value.Length, id);
        }

        byte[] data = [];

        if (hasPayload)
        {
            if (!TryDecode(payload, effectiveLimits.MaxClipboardBytes, decodePayload, out data))
            {
                return Invalid(DiagnosticCode.InvalidBase64, value.Length, id);
            }
        }

        return new KittyPacket(
            isValid: true,
            operation,
            replyStatus,
            selection,
            id,
            mime,
            password,
            name,
            data,
            hasPayload,
            [.. unknownKeys],
            diagnostic: null);
    }

    #endregion

    #region Diagnostics and validation

    /// <summary>
    /// Returns structural packet details while redacting payloads and credentials.
    /// </summary>
    /// <returns>A non-sensitive packet description.</returns>
    public override string ToString()
    {
        var unknown = UnknownKeys.Count == 0
            ? "none"
            : string.Join(',', UnknownKeys);

        return $"KittyPacket valid={IsValid} operation={Operation} status={ReplyStatus} " +
               $"selection={Selection} id={(Id is null ? "none" : "set")} " +
               $"mimeBytes={Mime.Length} payloadBytes={Data.Length} unknown={unknown}";
    }

    private static KittyPacket Invalid(DiagnosticCode code, int discarded, string? id = null)
    {
        Debug.Assert(Enum.IsDefined(code), "Invalid packets require a defined diagnostic code.");
        Debug.Assert(discarded >= 0, "Discarded packet byte counts are non-negative.");

        var diagnostic = new Diagnostic(code, SequenceKind.Osc, 0, discarded);

        return new KittyPacket(
            isValid: false,
            KittyOperation.None,
            KittyReplyStatus.None,
            Selection.Clipboard,
            id,
            ReadOnlyMemory<byte>.Empty,
            ReadOnlyMemory<byte>.Empty,
            ReadOnlyMemory<byte>.Empty,
            ReadOnlyMemory<byte>.Empty,
            hasPayload: false,
            [],
            diagnostic);
    }

    private static bool IsCanonicalBase64(ReadOnlySpan<byte> value)
    {
        // The span decoder accepts some non-canonical final quanta. Kitty
        // packets are correlation-sensitive, so require complete quartets and
        // terminal-only padding before allocating an owned decoded copy.
        if (value.Length % 4 != 0)
        {
            return false;
        }

        var padding = 0;

        for (var index = value.Length - 1; index >= 0 && value[index] == (byte) '='; index--)
        {
            padding++;
        }

        if (padding > 2)
        {
            return false;
        }

        for (var index = 0; index < value.Length - padding; index++)
        {
            if (value[index] is not (
                (>= (byte) 'A' and <= (byte) 'Z') or
                (>= (byte) 'a' and <= (byte) 'z') or
                (>= (byte) '0' and <= (byte) '9') or
                (byte) '+' or (byte) '/'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsIdentifier(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return false;
        }

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

    private static bool IsMetadataAscii(ReadOnlySpan<byte> value)
    {
        foreach (var item in value)
        {
            if (item is < 0x21 or > 0x7e)
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

            Debug.Assert(consumed > 0, "Successful UTF-8 decoding always advances the input span.");
            value = value[consumed..];
        }

        return true;
    }

    private static bool TryDecode(
        ReadOnlySpan<byte> encoded,
        int maximum,
        bool decodePayload,
        out byte[] decoded)
    {
        Debug.Assert(maximum >= 0, "Decoded clipboard limits are validated before parsing.");

        decoded = [];

        if (encoded.IsEmpty)
        {
            return true;
        }

        if (!IsCanonicalBase64(encoded) ||
            encoded.Length > Base64.GetMaxEncodedToUtf8Length(maximum))
        {
            return false;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(
            Math.Max(1, Base64.GetMaxDecodedFromUtf8Length(encoded.Length)));

        try
        {
            var status = Base64.DecodeFromUtf8(
                encoded,
                buffer,
                out var consumed,
                out var written);

            if (status != OperationStatus.Done || consumed != encoded.Length || written > maximum)
            {
                return false;
            }

            // decodePayload controls ownership/allocation only; validation above
            // (canonical encoding, decoded-size limit) always runs the same way.
            if (decodePayload)
            {
                decoded = buffer.AsSpan(0, written).ToArray();
            }

            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static bool TryDecodeUtf8(
        ReadOnlySpan<byte> encoded,
        int maximum,
        out byte[] decoded) =>
        TryDecode(encoded, maximum, decodePayload: true, out decoded) && IsValidUtf8(decoded);

    private static bool TryParseOperation(
        ReadOnlySpan<byte> value,
        out KittyOperation operation)
    {
        operation = value switch
        {
            _ when value.SequenceEqual("read"u8) => KittyOperation.Read,
            _ when value.SequenceEqual("write"u8) => KittyOperation.Write,
            _ when value.SequenceEqual("wdata"u8) => KittyOperation.WriteData,
            _ when value.SequenceEqual("walias"u8) => KittyOperation.WriteAlias,
            _ => KittyOperation.None
        };

        return operation != KittyOperation.None;
    }

    private static bool TryParseSelection(ReadOnlySpan<byte> value, out Selection selection)
    {
        if (value.SequenceEqual("primary"u8))
        {
            selection = Selection.Primary;
            return true;
        }

        if (value.SequenceEqual("clipboard"u8))
        {
            selection = Selection.Clipboard;
            return true;
        }

        selection = default;
        return false;
    }

    private static bool TryParseStatus(
        ReadOnlySpan<byte> value,
        out KittyReplyStatus status)
    {
        status = value switch
        {
            _ when value.SequenceEqual("OK"u8) => KittyReplyStatus.Ok,
            _ when value.SequenceEqual("DATA"u8) => KittyReplyStatus.Data,
            _ when value.SequenceEqual("DONE"u8) => KittyReplyStatus.Done,
            _ when value.SequenceEqual("EIO"u8) => KittyReplyStatus.Io,
            _ when value.SequenceEqual("EINVAL"u8) => KittyReplyStatus.Invalid,
            _ when value.SequenceEqual("ENOSYS"u8) => KittyReplyStatus.Unavailable,
            _ when value.SequenceEqual("EPERM"u8) => KittyReplyStatus.Denied,
            _ when value.SequenceEqual("EBUSY"u8) => KittyReplyStatus.Busy,
            _ => KittyReplyStatus.None
        };

        return status != KittyReplyStatus.None;
    }

    #endregion
}
