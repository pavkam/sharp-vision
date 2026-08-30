// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Formats untrusted text and bytes without hiding control characters.</summary>
internal static class DiagnosticTextFormatter
{
    /// <summary>Escapes text for a readable diagnostic value.</summary>
    /// <param name="value">The text to format.</param>
    /// <returns>A bounded escaped representation.</returns>
    internal static string EscapeText(ReadOnlySpan<char> value)
    {
        const int maximumRunes = 4_096;
        var builder = new StringBuilder(Math.Min(value.Length, maximumRunes));
        var count = 0;

        foreach (var rune in value.EnumerateRunes())
        {
            if (count++ == maximumRunes)
            {
                _ = builder.Append("… [truncated after 4096 Unicode scalars]");
                break;
            }

            _ = rune.Value switch
            {
                0x00 => builder.Append("\\0 [NUL]"),
                0x07 => builder.Append("\\a [BEL]"),
                0x09 => builder.Append("\\t [TAB]"),
                0x0A => builder.Append("\\n [LF]"),
                0x0D => builder.Append("\\r [CR]"),
                0x1B => builder.Append("\\e [ESC]"),
                0x5C => builder.Append("\\\\"),
                < 0x20 or (>= 0x7F and <= 0x9F) => builder.Append(CultureInfo.InvariantCulture, $"\\u{{{rune.Value:X}}} [control]"),
                _ => builder.Append(rune.ToString())
            };
        }

        return builder.ToString();
    }

    /// <summary>Formats bytes as hexadecimal and valid UTF-8 text.</summary>
    /// <param name="value">The bytes to format.</param>
    /// <returns>A bounded hexadecimal and text representation.</returns>
    internal static string FormatBytes(ReadOnlySpan<byte> value)
    {
        const int maximumBytes = 4_096;
        var visible = value[..Math.Min(value.Length, maximumBytes)];
        var hex = Convert.ToHexString(visible);
        var suffix = value.Length > maximumBytes
            ? $"… [truncated after {maximumBytes} of {value.Length} bytes]"
            : string.Empty;

        try
        {
            var utf8 = new UTF8Encoding(false, true).GetString(visible);
            return $"{hex}{suffix} | UTF-8: {EscapeText(utf8)}";
        }
        catch (DecoderFallbackException)
        {
            return $"{hex}{suffix} | UTF-8: [invalid]";
        }
    }
}
