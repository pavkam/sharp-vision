// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

/// <summary>
/// Encodes typed operating system commands used by the terminal runtime.
/// </summary>
/// <example>
/// <code>
/// Osc.Title(writer, "SharpVision"u8);
/// Osc.OpenHyperlink(writer, "https://example.test"u8, "docs"u8);
/// </code>
/// </example>
[PublicAPI]
public static class Osc
{
    private const int _stackPayloadLimit = 512;

    /// <summary>Sets both the icon label and window title using OSC 0.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="title">Borrowed UTF-8 title bytes without controls.</param>
    /// <exception cref="ArgumentException"><paramref name="title"/> contains a control byte.</exception>
    public static void IconAndTitle(ProtocolWriter writer, ReadOnlySpan<byte> title) => writer.Osc(0, title);

    /// <summary>Sets the window title using OSC 2.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="title">Borrowed UTF-8 title bytes without controls.</param>
    /// <exception cref="ArgumentException"><paramref name="title"/> contains a control byte.</exception>
    public static void Title(ProtocolWriter writer, ReadOnlySpan<byte> title) => writer.Osc(2, title);

    /// <summary>Opens an OSC 8 hyperlink.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="uri">Borrowed UTF-8 target bytes; the library never opens it.</param>
    /// <param name="id">Optional safe hyperlink identifier.</param>
    /// <exception cref="ArgumentException">A value contains forbidden metadata or control bytes.</exception>
    /// <exception cref="OverflowException">The combined payload length exceeds <see cref="int.MaxValue"/>.</exception>
    public static void OpenHyperlink(
        ProtocolWriter writer,
        ReadOnlySpan<byte> uri,
        ReadOnlySpan<byte> id = default)
    {
        ValidateIdentifier(id);

        var prefixLength = id.IsEmpty ? 0 : 3;
        var length = checked(prefixLength + id.Length + uri.Length + 1);
        var rented = length > _stackPayloadLimit
            ? ArrayPool<byte>.Shared.Rent(length)
            : null;
        var payload = rented is null
            ? stackalloc byte[length]
            : rented.AsSpan();

        try
        {
            var position = 0;

            if (!id.IsEmpty)
            {
                "id="u8.CopyTo(payload);
                position = 3;
                id.CopyTo(payload[position..]);
                position += id.Length;
            }

            payload[position++] = (byte) ';';
            uri.CopyTo(payload[position..]);
            writer.Osc(8, payload[..length]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented, clearArray: true);
            }
        }
    }

    /// <summary>Closes the active OSC 8 hyperlink.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    public static void CloseHyperlink(ProtocolWriter writer) => writer.Osc(8, ";"u8);

    /// <summary>Queries one indexed palette color using OSC 4.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="index">The palette index from 0 through 255.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is outside 0 through 255.
    /// </exception>
    public static void QueryPalette(ProtocolWriter writer, int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, byte.MaxValue);

        Span<byte> payload = stackalloc byte[5];
        var formatted = Utf8Formatter.TryFormat(index, payload, out var written);
        Debug.Assert(formatted, "Three bytes must hold a palette index.");
        payload[written++] = (byte) ';';
        payload[written++] = (byte) '?';
        writer.Osc(4, payload[..written]);
    }

    /// <summary>Queries the terminal's default foreground color using OSC 10.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    public static void QueryForeground(ProtocolWriter writer) => writer.Osc(10, "?"u8);

    /// <summary>Queries the terminal's default background color using OSC 11.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    public static void QueryBackground(ProtocolWriter writer) => writer.Osc(11, "?"u8);

    /// <summary>Queries iTerm2 feature-reporting capabilities using OSC 1337 ; Capabilities.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    public static void QueryItermCapabilities(ProtocolWriter writer) => writer.Osc(1337, "Capabilities"u8);

    private static void ValidateIdentifier(ReadOnlySpan<byte> value)
    {
        foreach (var item in value)
        {
            var valid = item is (>= (byte) 'a' and <= (byte) 'z') or
                (>= (byte) 'A' and <= (byte) 'Z') or
                (>= (byte) '0' and <= (byte) '9') or
                (byte) '-' or (byte) '_' or (byte) '.' or (byte) '+';

            if (!valid)
            {
                throw new ArgumentException(
                    "A hyperlink identifier contains a forbidden byte.",
                    nameof(value));
            }
        }
    }
}
