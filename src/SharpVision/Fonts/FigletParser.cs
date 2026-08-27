// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Fonts;

/// <summary>Parses complete bounded FIG-font version 2 payloads.</summary>
internal static class FigletParser
{
    [SuppressMessage(
        "Style",
        "IDE0052:Remove unread private members",
        Justification = "Read only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static readonly int[] _additionalCodes = [196, 214, 220, 228, 246, 252, 223];

    extension(FigletFont)
    {
        /// <summary>Parses one complete UTF-8 payload after byte-limit validation.</summary>
        /// <param name="bytes">The complete borrowed encoded payload.</param>
        /// <param name="name">The validated logical font name.</param>
        /// <param name="limits">The validated finite limits.</param>
        /// <returns>A fully validated immutable font.</returns>
        /// <exception cref="FormatException">The payload violates FIG-font syntax.</exception>
        /// <exception cref="InvalidDataException">A structural limit is exceeded.</exception>
        public static FigletFont Parse(ReadOnlySpan<byte> bytes, string name, FigletLimits limits)
        {
            string text;

            try
            {
                text = new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException exception)
            {
                _ = exception;
                // FIGfont 2 predates Unicode and many catalog fonts retain an
                // undeclared single-byte encoding. Latin-1 is the only lossless
                // byte-to-scalar fallback; callers still receive deterministic text.
                text = Encoding.Latin1.GetString(bytes);
            }

            var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
            var cursor = 0;
            var header = ReadLine(lines, ref cursor, "header").Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (header.Length < 6 ||
                header[0].Length < 6 ||
                (!header[0].StartsWith("flf2a", StringComparison.Ordinal) &&
                 !header[0].StartsWith("tlf2a", StringComparison.Ordinal)))
            {
                throw new FormatException("The FIG-font header signature is invalid.");
            }

            var hardblank = header[0][5];
            var height = Positive(header[1], "height");
            var baseline = NonNegative(header[2], "baseline");
            var maxLength = Positive(header[3], "maximum row length");
            var oldLayout = Integer(header[4], "old layout");
            var comments = NonNegative(header[5], "comment count");

            if (height > limits.MaxHeight)
            {
                throw new InvalidDataException("The FIG-font height exceeds the configured limit.");
            }

            baseline = Math.Min(baseline, height);

            if (maxLength > limits.MaxRowWidth)
            {
                throw new InvalidDataException("The FIG-font row width exceeds the configured limit.");
            }

            if (comments > limits.MaxComments)
            {
                throw new InvalidDataException("The FIG-font comment count exceeds the configured limit.");
            }

            var direction = header.Length > 6 &&
                            int.TryParse(header[6], NumberStyles.Integer, CultureInfo.InvariantCulture,
                                out var directionValue)
                ? Direction(directionValue)
                : FigletDirection.LeftToRight;
            var fullLayout = header.Length > 7 &&
                             int.TryParse(header[7], NumberStyles.Integer, CultureInfo.InvariantCulture,
                                 out var layoutValue)
                ? layoutValue
                : -1;
            var layout = fullLayout >= 0 ? FullLayout(fullLayout) : OldLayout(oldLayout);

            for (var index = 0; index < comments; index++)
            {
                _ = ReadLine(lines, ref cursor, "comment");
            }

            Dictionary<int, FigletGlyph> glyphs = [];
            for (var code = 32; code <= 126; code++)
            {
                glyphs.Add(code, ReadGlyph(lines, ref cursor, height, limits));
            }

            if (glyphs.Count > limits.MaxGlyphs)
            {
                throw new InvalidDataException("The FIG-font glyph count exceeds the configured limit.");
            }

            if (lines.Length - cursor >= _additionalCodes.Length * height)
            {
                // The block carries no tag line of its own, so simply having enough remaining lines
                // doesn't prove it's actually present: a font that omits it but has trailing
                // code-tagged extension glyphs can reach the same line count. Prefer the
                // no-German-block interpretation whenever the remainder parses cleanly as extension
                // glyphs on its own - real German-umlaut glyph art essentially never itself parses as
                // a run of legal code tags, so this resolves the ambiguity correctly without ever
                // risking silently swallowing real glyph data as fake German rows.
                var extensionCursor = cursor;
                var extensionGlyphs = new Dictionary<int, FigletGlyph>(glyphs);

                if (TryReadExtensionGlyphs(lines, ref extensionCursor, height, limits, extensionGlyphs))
                {
                    glyphs = extensionGlyphs;
                    cursor = extensionCursor;
                }
                else
                {
                    for (var index = 0; index < _additionalCodes.Length; index++)
                    {
                        glyphs.Add(_additionalCodes[index], ReadGlyph(lines, ref cursor, height, limits));
                    }
                }
            }

            while (cursor < lines.Length && glyphs.Count < limits.MaxGlyphs)
            {
                var tag = lines[cursor++].Trim();

                if (tag.Length == 0)
                {
                    continue;
                }

                var separator = tag.IndexOfAny([' ', '\t']);
                var token = separator < 0 ? tag : tag[..separator];
                var code = CodePoint(token);
                var glyph = ReadGlyph(lines, ref cursor, height, limits);

                // Negative code tags are FIGlet extension records such as legacy
                // character maps. They own normal glyph rows but do not identify a
                // Unicode scalar and are therefore consumed without publication.
                if (code < 0)
                {
                    continue;
                }

                if (!Rune.IsValid(code))
                {
                    throw new FormatException($"The FIG-font code tag '{token}' is not a Unicode scalar.");
                }

                glyphs[code] = glyph;
            }

            // The tag-driven loops above never let their own additions push the count past the
            // limit (each checks glyphs.Count against limits.MaxGlyphs before adding), but the
            // untagged legacy German-umlaut block above is added unconditionally once detected, so
            // it alone can still carry the count past the limit. The trailing-data check that
            // follows catches a different failure: a tag-driven loop stopping early because it hit
            // the limit while genuine tag/glyph lines remained unconsumed, which no longer shows up
            // as a raw count overrun since those loops stop at exactly the limit.
            return glyphs.Count > limits.MaxGlyphs
                    || (cursor < lines.Length && lines[cursor..].Any(line => line.Length != 0))
                ? throw new InvalidDataException("The FIG-font glyph count exceeds the configured limit.")
                : new FigletFont(
                    name,
                    height,
                    baseline,
                    hardblank,
                    direction,
                    layout,
                    glyphs,
                    limits);
        }
    }

    /// <summary>
    /// Attempts to parse the entire remainder of the file as code-tagged extension glyphs, without
    /// ever assuming an untagged legacy block (such as the German-umlaut block) exists. This is the
    /// same logic as the code-tagged extension-glyph loop in <c>FigletFont.Parse</c>, extracted into a
    /// reusable, catchable form so it can be tried speculatively: a genuine parse failure means
    /// the remainder is not actually extension glyphs, which is exactly the signal needed to
    /// distinguish a real trailing German block (glyph art, not code tags) from a font that merely has
    /// enough trailing lines to match the German block's size but is actually all extension glyphs.
    /// </summary>
    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static bool TryReadExtensionGlyphs(
        string[] lines,
        ref int cursor,
        int height,
        FigletLimits limits,
        Dictionary<int, FigletGlyph> glyphs)
    {
        try
        {
            while (cursor < lines.Length && glyphs.Count < limits.MaxGlyphs)
            {
                var tag = lines[cursor++].Trim();

                if (tag.Length == 0)
                {
                    continue;
                }

                var separator = tag.IndexOfAny([' ', '\t']);
                var token = separator < 0 ? tag : tag[..separator];
                var code = CodePoint(token);
                var glyph = ReadGlyph(lines, ref cursor, height, limits);

                if (code < 0)
                {
                    continue;
                }

                if (!Rune.IsValid(code))
                {
                    throw new FormatException($"The FIG-font code tag '{token}' is not a Unicode scalar.");
                }

                glyphs[code] = glyph;
            }

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static FigletGlyph ReadGlyph(
        string[] lines,
        ref int cursor,
        int height,
        FigletLimits limits)
    {
        var rows = new string[height];
        var width = 0;

        for (var row = 0; row < height; row++)
        {
            var encoded = ReadLine(lines, ref cursor, "glyph row").TrimEnd();

            // Legacy FIGfonts sometimes understate the header's allocation hint. The caller's
            // configured limit remains the hard safety boundary for encoded storage.
            if (encoded.Length == 0 || encoded.Length > limits.MaxRowWidth)
            {
                throw new FormatException(
                    $"FIG-font glyph row {cursor} has length {encoded.Length}; expected 1 through " +
                    $"{limits.MaxRowWidth}.");
            }

            var endMark = encoded[^1];
            var end = encoded.Length;

            while (end > 0 && encoded[end - 1] == endMark)
            {
                end--;
            }

            rows[row] = encoded[..end];
            width = Math.Max(width, rows[row].Length);
        }

        for (var row = 0; row < rows.Length; row++)
        {
            rows[row] = rows[row].PadRight(width);
        }

        return new FigletGlyph(rows);
    }

    private static string ReadLine(string[] lines, ref int cursor, string area) =>
        cursor < lines.Length
            ? lines[cursor++]
            : throw new FormatException($"The FIG-font ended inside {area}.");

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    [Pure]
    private static int Positive(string value, string area)
    {
        var result = Integer(value, area);
        return result > 0 ? result : throw new FormatException($"The FIG-font {area} must be positive.");
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    [Pure]
    private static int NonNegative(string value, string area)
    {
        var result = Integer(value, area);
        return result >= 0 ? result : throw new FormatException($"The FIG-font {area} cannot be negative.");
    }

    [Pure]
    private static int Integer(string value, string area) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new FormatException($"The FIGfont {area} is not an integer.");

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    [Pure]
    private static int CodePoint(string value)
    {
        var sign = 1;
        var token = value.AsSpan();

        if (!token.IsEmpty && token[0] == '-')
        {
            sign = -1;
            token = token[1..];
        }

        var style = NumberStyles.Integer;
        var radix = 10;

        if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            token = token[2..];
            style = NumberStyles.AllowHexSpecifier;
            radix = 16;
        }
        else if (token.Length > 1 && token[0] == '0')
        {
            token = token[1..];
            radix = 8;
        }

        if (radix == 8)
        {
            try
            {
                return checked(sign * Convert.ToInt32(token.ToString(), radix));
            }
            catch (Exception exception) when (exception is FormatException or OverflowException)
            {
                throw new FormatException($"The FIG-font code tag '{value}' is invalid.", exception);
            }
        }

        return int.TryParse(token, style, CultureInfo.InvariantCulture, out var result)
            ? checked(sign * result)
            : throw new FormatException($"The FIG-font code tag '{value}' is invalid.");
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    [Pure]
    private static FigletDirection Direction(int value) => value switch
    {
        1 => FigletDirection.RightToLeft,
        _ => FigletDirection.LeftToRight
    };

    /// <summary>
    /// Translates the legacy <c>old_layout</c> header field, whose values are not literal
    /// <see cref="FigletLayout"/> bits: <c>-1</c> means full width, <c>0</c> means fitting, and
    /// <c>1..63</c> is a sum of smush-rule bits that implies horizontal smushing mode.
    /// </summary>
    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    [SuppressMessage(
        "Style",
        "IDE0052:Remove unread private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    [Pure]
    private static FigletLayout OldLayout(int value)
    {
        if (value == -1)
        {
            return FigletLayout.None;
        }

        if (value == 0)
        {
            return FigletLayout.HorizontalFitting;
        }

        var layout = (FigletLayout) value;

        if (value is > 0 and < 64)
        {
            layout |= FigletLayout.HorizontalSmushing;
        }

        return layout;
    }

    /// <summary>
    /// Translates the modern <c>full_layout</c> header field, whose value is a literal
    /// <see cref="FigletLayout"/> bit mask (including its own explicit
    /// <see cref="FigletLayout.HorizontalFitting"/> and <see cref="FigletLayout.HorizontalSmushing"/>
    /// bits). Unlike <see cref="OldLayout"/>, zero genuinely means full width and no bit is ever
    /// injected automatically.
    /// </summary>
    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    [Pure]
    private static FigletLayout FullLayout(int value) => (FigletLayout) value;
}
