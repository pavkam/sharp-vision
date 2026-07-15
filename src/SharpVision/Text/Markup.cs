// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;



/// <summary>Parses lenient inline text markup into visible text and semantic style spans.</summary>
internal static class Markup
{
    /// <summary>Parses markup without discarding malformed source fragments.</summary>
    /// <param name="source">The markup borrowed for this synchronous call.</param>
    /// <param name="display">The visible text with valid tags removed and escapes resolved.</param>
    /// <returns>Positive spans that tile the complete visible text in source order.</returns>
    internal static StyleSpan[] Parse(ReadOnlySpan<char> source, out string display)
    {
        var text = new StringBuilder(source.Length);
        List<StyleSpan> spans = [];
        List<OpenTag> open = [];
        var spanStart = 0;
        var current = Style.Inherit;
        var position = 0;

        while (position < source.Length)
        {
            var value = source[position];

            if (value == '\\' &&
                position + 1 < source.Length &&
                source[position + 1] is '<' or '\\')
            {
                _ = text.Append(source[position + 1]);
                position += 2;
                continue;
            }

            if (value != '<')
            {
                _ = text.Append(value);
                position++;
                continue;
            }

            var close = source[(position + 1)..].IndexOf('>');

            if (close < 0)
            {
                _ = text.Append(source[position..]);
                _ = source.Length;
                break;
            }

            close += position + 1;
            var body = source[(position + 1)..close];

            if (body.Contains('<') || !TryApplyTag(body, open))
            {
                _ = text.Append(source[position..(close + 1)]);
                position = close + 1;
                continue;
            }

            var next = Style.From(open);

            if (next != current)
            {
                AddSpan(spans, current, spanStart, text.Length);
                spanStart = text.Length;
                current = next;
            }

            position = close + 1;
        }

        AddSpan(spans, current, spanStart, text.Length);
        display = text.ToString();
        return [.. spans];
    }

    /// <summary>Escapes visible text so markup metacharacters round-trip literally.</summary>
    /// <param name="value">The non-null visible text.</param>
    /// <returns>A string with backslash and opening-angle characters escaped.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    internal static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (character is '\\' or '<')
            {
                _ = builder.Append('\\');
            }

            _ = builder.Append(character);
        }

        return builder.ToString();
    }

    private static void AddSpan(List<StyleSpan> spans, Style style, int start, int end)
    {
        if (end > start)
        {
            spans.Add(style.ToSpan(start, end - start));
        }
    }

    private static bool TryApplyTag(ReadOnlySpan<char> body, List<OpenTag> open)
    {
        body = body.Trim();

        if (body.IsEmpty)
        {
            return false;
        }

        if (body[0] == '/')
        {
            var closeName = body[1..].Trim();

            if (closeName.Contains('=') || closeName.Contains('<'))
            {
                return false;
            }

            if (closeName.IsEmpty)
            {
                if (open.Count > 0)
                {
                    open.RemoveAt(open.Count - 1);
                }

                return true;
            }

            var key = closeName.ToString();

            for (var index = open.Count - 1; index >= 0; index--)
            {
                if (open[index].Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    open.RemoveAt(index);
                    return true;
                }
            }

            return true;
        }

        var equals = body.IndexOf('=');
        var hasValue = equals >= 0;
        var name = (hasValue ? body[..equals] : body).Trim();
        var value = hasValue ? body[(equals + 1)..].Trim() : default;

        if (name.IsEmpty || !TryResolveFacet(name, value, hasValue, out var tag))
        {
            return false;
        }

        open.Add(tag);
        return true;
    }

    private static bool TryResolveFacet(
        ReadOnlySpan<char> name,
        ReadOnlySpan<char> value,
        bool hasValue,
        out OpenTag tag)
    {
        tag = default;
        var key = name.ToString().ToLowerInvariant();

        if (!hasValue && TryAttribute(key, name, out tag))
        {
            return true;
        }

        switch (key)
        {
            case "u" or "underline":
                if (!hasValue)
                {
                    tag = OpenTag.UnderlineTag(name, Underline.Straight);
                    return true;
                }

                if (TryUnderline(value, out var underline))
                {
                    tag = OpenTag.UnderlineTag(name, underline);
                    return true;
                }

                return false;
            case "uc" when hasValue:
                if (TryColor(value, out var underlineColor))
                {
                    tag = OpenTag.UnderlineColorTag(name, underlineColor);
                    return true;
                }

                return false;
            case "fg" or "color" when hasValue:
                if (TryColor(value, out var foreground))
                {
                    tag = OpenTag.ForegroundTag(name, foreground);
                    return true;
                }

                return false;
            case "bg" when hasValue:
                if (TryColor(value, out var background))
                {
                    tag = OpenTag.BackgroundTag(name, background);
                    return true;
                }

                return false;
            case "link" or "a" when hasValue:
                if (IsValidLink(value))
                {
                    tag = OpenTag.LinkTag(name, value.ToString());
                    return true;
                }

                return false;
            default:
                if (!hasValue && TryColor(name, out var bare))
                {
                    tag = OpenTag.ForegroundTag(name, bare);
                    return true;
                }

                return false;
        }
    }

    private static bool TryAttribute(string key, ReadOnlySpan<char> name, out OpenTag tag)
    {
        var attribute = key switch
        {
            "b" or "bold" => TerminalAttributes.Bold,
            "d" or "dim" => TerminalAttributes.Dim,
            "i" or "italic" => TerminalAttributes.Italic,
            "s" or "strike" => TerminalAttributes.Strike,
            "reverse" => TerminalAttributes.Reverse,
            "blink" => TerminalAttributes.Blink,
            "rapidblink" => TerminalAttributes.RapidBlink,
            "hidden" or "conceal" => TerminalAttributes.Hidden,
            "overline" => TerminalAttributes.Overline,
            _ => TerminalAttributes.None,
        };

        tag = attribute == TerminalAttributes.None ? default : OpenTag.Attribute(name, attribute);
        return attribute != TerminalAttributes.None;
    }

    private static bool TryUnderline(ReadOnlySpan<char> value, out Underline underline)
    {
        underline = value.ToString().ToLowerInvariant() switch
        {
            "straight" => Underline.Straight,
            "double" => Underline.Paired,
            "curly" => Underline.Curly,
            "dotted" => Underline.Dotted,
            "dashed" => Underline.Dashed,
            _ => Underline.None,
        };

        return underline != Underline.None;
    }

    private static bool TryColor(ReadOnlySpan<char> value, out Color color)
    {
        color = Color.Default;

        if (value.IsEmpty)
        {
            return false;
        }

        if (value[0] == '#')
        {
            return Color.TryFromHex(value.ToString(), out color);
        }

        if (int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var index) &&
            index is >= 0 and <= byte.MaxValue)
        {
            color = Color.Indexed(index);
            return true;
        }

        var key = value.ToString().ToLowerInvariant();
        var ansi = key switch
        {
            "black" => 0,
            "red" => 1,
            "green" => 2,
            "yellow" => 3,
            "blue" => 4,
            "magenta" => 5,
            "cyan" => 6,
            "white" => 7,
            "brightblack" or "gray" or "grey" => 8,
            "brightred" => 9,
            "brightgreen" => 10,
            "brightyellow" => 11,
            "brightblue" => 12,
            "brightmagenta" => 13,
            "brightcyan" => 14,
            "brightwhite" => 15,
            _ => -1,
        };

        if (ansi >= 0)
        {
            color = Color.Indexed(ansi);
            return true;
        }

        if (Enum.TryParse(key, ignoreCase: true, out ColorRole role) && Enum.IsDefined(role))
        {
            color = Color.Role((int) role);
            return true;
        }

        return false;
    }

    private static bool IsValidLink(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return false;
            }
        }

        return true;
    }
}
