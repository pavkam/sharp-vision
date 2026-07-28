// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using Terminal.Rendering;

/// <summary>Parses, measures, and draws ampersand-marked access text.</summary>
internal static class AccessKeyText
{
    private const int _stackLimit = 256;

    /// <summary>Returns the first Unicode scalar marked by one unescaped ampersand.</summary>
    /// <param name="text">The borrowed caption text.</param>
    /// <param name="key">The invariant-matchable marked scalar, or the replacement scalar on failure.</param>
    /// <returns>True when the caption contains a complete marked scalar.</returns>
    public static bool TryGetKey(ReadOnlySpan<char> text, out Rune key)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '&')
            {
                continue;
            }

            if (index + 1 >= text.Length)
            {
                break;
            }

            if (text[index + 1] == '&')
            {
                index++;
                continue;
            }

            var status = Rune.DecodeFromUtf16(text[(index + 1)..], out key, out _);
            return status == OperationStatus.Done;
        }

        key = Rune.ReplacementChar;
        return false;
    }

    /// <summary>Measures visible caption cells after applying mnemonic marker escaping.</summary>
    /// <param name="text">The non-null caption.</param>
    /// <param name="ambiguous">The validated ambiguous-width policy.</param>
    /// <param name="useMnemonic">Whether ampersands have access-text semantics.</param>
    /// <returns>The non-negative visible terminal-cell width.</returns>
    public static int Measure(string text, Ambiguous ambiguous, bool useMnemonic)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!useMnemonic || text.IndexOf('&', StringComparison.Ordinal) < 0)
        {
            return Width.Measure(text, ambiguous).Cells;
        }

        char[]? rented = null;
        var buffer = text.Length <= _stackLimit
            ? stackalloc char[text.Length]
            : rented = ArrayPool<char>.Shared.Rent(text.Length);

        try
        {
            var length = Collapse(text, buffer, out _);
            return Width.Measure(buffer[..length], ambiguous).Cells;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented, clearArray: true);
            }
        }
    }

    /// <summary>Draws visible caption text and underlines the complete marked grapheme.</summary>
    /// <param name="canvas">The borrowed clipped destination.</param>
    /// <param name="text">The non-null caption.</param>
    /// <param name="origin">The absolute starting cell.</param>
    /// <param name="style">The inherited caption style.</param>
    /// <param name="background">The validated background composition mode.</param>
    /// <param name="ambiguous">The validated ambiguous-width policy.</param>
    /// <param name="useMnemonic">Whether ampersands have access-text semantics.</param>
    /// <param name="accessKeyForeground">The resolved enabled access-key foreground, or null to inherit.</param>
    /// <returns>The logical visible-cell advance.</returns>
    public static int Draw(
        TerminalCanvas canvas,
        string text,
        Point origin,
        TerminalStyle style,
        BackgroundMode background,
        Ambiguous ambiguous,
        bool useMnemonic,
        Color? accessKeyForeground = null)
    {
        if (!useMnemonic || text.IndexOf('&', StringComparison.Ordinal) < 0)
        {
            return canvas.Draw(text.AsSpan(), origin, style, background: background).Cells;
        }

        char[]? rented = null;
        var buffer = text.Length <= _stackLimit
            ? stackalloc char[text.Length]
            : rented = ArrayPool<char>.Shared.Rent(text.Length);

        try
        {
            var length = Collapse(text, buffer, out var mnemonicOffset);
            var visible = buffer[..length];

            if (mnemonicOffset < 0)
            {
                return canvas.Draw(visible, origin, style, background: background).Cells;
            }

            var mnemonicLength = FindMnemonicGraphemeLength(visible, mnemonicOffset);
            var before = canvas.Draw(visible[..mnemonicOffset], origin, style, background: background);
            var mnemonicStyle = WithAccessKey(style, accessKeyForeground);
            var marked = canvas.Draw(
                visible.Slice(mnemonicOffset, mnemonicLength),
                before.Final,
                mnemonicStyle,
                background: background);
            var after = canvas.Draw(
                visible[(mnemonicOffset + mnemonicLength)..],
                marked.Final,
                style,
                background: background);
            return before.Cells + marked.Cells + after.Cells;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented, clearArray: true);
            }
        }
    }

    /// <summary>Converts mnemonic syntax into existing inline markup syntax for <see cref="Text"/>.</summary>
    /// <param name="text">The non-null source markup.</param>
    /// <param name="useMnemonic">Whether ampersands have access-text semantics.</param>
    /// <param name="highlightMnemonic">Whether the marked grapheme receives the access-key foreground.</param>
    /// <param name="hotkeyColor">Optional concrete hotkey color to apply as an inline foreground tag.</param>
    /// <returns>Source markup with marker escapes collapsed and the mnemonic grapheme underlined.</returns>
    public static string ToMarkup(
        string text,
        bool useMnemonic,
        bool highlightMnemonic = true,
        Color? hotkeyColor = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!useMnemonic || text.IndexOf('&', StringComparison.Ordinal) < 0)
        {
            return text;
        }

        var buffer = new char[text.Length];
        var length = Collapse(text, buffer, out var mnemonicOffset);

        if (mnemonicOffset < 0)
        {
            return new string(buffer, 0, length);
        }

        var visible = buffer.AsSpan(0, length);
        var mnemonicLength = FindMnemonicGraphemeLength(visible, mnemonicOffset);
        var builder = new StringBuilder(length + (highlightMnemonic ? 30 : 7));
        _ = builder.Append(visible[..mnemonicOffset]);

        if (highlightMnemonic && hotkeyColor is { IsRgb: true } color)
        {
            _ = builder.Append(CultureInfo.InvariantCulture, $"<fg=#{color.Red:x2}{color.Green:x2}{color.Blue:x2}>");
        }

        _ = builder.Append("<u>");
        _ = builder.Append(visible.Slice(mnemonicOffset, mnemonicLength));
        _ = builder.Append("</u>");

        if (highlightMnemonic && hotkeyColor is { IsRgb: true })
        {
            _ = builder.Append("</fg>");
        }

        _ = builder.Append(visible[(mnemonicOffset + mnemonicLength)..]);
        return builder.ToString();
    }

    private static int Collapse(ReadOnlySpan<char> source, Span<char> destination, out int mnemonicOffset)
    {
        Debug.Assert(destination.Length >= source.Length, "Access-text storage retains the complete source capacity.");
        mnemonicOffset = -1;
        var written = 0;

        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] != '&')
            {
                destination[written++] = source[index];
                continue;
            }

            if (index + 1 >= source.Length)
            {
                destination[written++] = '&';
                continue;
            }

            if (source[index + 1] == '&')
            {
                destination[written++] = '&';
                index++;
                continue;
            }

            if (mnemonicOffset < 0)
            {
                mnemonicOffset = written;
            }
        }

        return written;
    }

    private static int FindMnemonicGraphemeLength(ReadOnlySpan<char> visible, int mnemonicOffset)
    {
        foreach (var grapheme in Graphemes.Enumerate(visible[mnemonicOffset..]))
        {
            Debug.Assert(grapheme.Offset == 0, "The first enumerated access-key grapheme begins at its marker.");
            return grapheme.Length;
        }

        throw new InvalidOperationException("A mnemonic marker must precede one complete grapheme.");
    }

    private static TerminalStyle WithAccessKey(TerminalStyle style, Color? foreground)
    {
        var attributes = style.Attributes;

        if ((attributes & TerminalAttributes.Underline) == 0 && style.Underline == Underline.None)
        {
            attributes |= TerminalAttributes.Underline;
        }

        return new TerminalStyle(
            foreground ?? style.Foreground,
            style.Background,
            attributes,
            style.Hyperlink,
            style.Underline,
            style.UnderlineColor);
    }
}
