// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using Text;

/// <summary>
/// Implements the transient text-and-selection buffer, partial-state validation, and commit
/// parsing shared by every buffer-then-commit numeric field control (currently
/// <see cref="Controls.Input.NumberInput"/>).
/// </summary>
/// <remarks>
/// Currency-agnostic by design: it reads its decimal separator, group separator, and sign tokens
/// as strings from a caller-supplied <see cref="NumberFormatInfo"/> rather than assuming a single
/// character or a specific codepoint, so a future currency-symbol-aware field can reuse it
/// unchanged. Caret and selection math is grapheme-safe, built on the shared
/// <see cref="Edit"/>/<see cref="Selection"/>/<see cref="EditResult"/> primitives; every operation
/// here runs in time proportional to the buffered text length, which is acceptable for the short
/// values a numeric field holds.
/// </remarks>
internal sealed class NumericEditBuffer
{
    private NumberFormatInfo _format = NumberFormatInfo.InvariantInfo;
    private bool _integerOnly;

    /// <summary>Gets the current transient text.</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>Gets the current grapheme-safe selection.</summary>
    public Selection Selection { get; private set; }

    /// <summary>Gets whether the buffer holds no text at all.</summary>
    public bool IsEmpty => Text.Length == 0;

    /// <summary>Configures the separator, sign, and integer-only policy every subsequent operation
    /// validates and parses against.</summary>
    /// <param name="format">The non-null source of decimal separator, group separator, and sign tokens.</param>
    /// <param name="integerOnly">Whether the decimal separator is rejected and only whole digits are admitted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="format"/> is null.</exception>
    public void Configure(NumberFormatInfo format, bool integerOnly)
    {
        ArgumentNullException.ThrowIfNull(format);
        _format = format;
        _integerOnly = integerOnly;
    }

    /// <summary>Replaces the buffer with caller-supplied text, unconditionally, placing the caret at
    /// its end.</summary>
    /// <param name="text">The non-null replacement text, assumed already valid under the current
    /// configuration - typically the formatted committed value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public void Load(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
        Selection = new Selection(text.Length, text.Length);
    }

    /// <summary>Splices text over the current selection and applies it only when the resulting whole
    /// buffer is still a valid partial numeric state - used for both single typed characters and
    /// whole pasted strings, rejecting a paste in full rather than silently stripping it.</summary>
    /// <param name="value">The non-null text to insert.</param>
    /// <returns>True when the insertion was accepted and changed the buffer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public bool Insert(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        EditResult candidate;

        try
        {
            candidate = Edit.Replace(Text, Selection, value);
        }
        catch (ArgumentException)
        {
            // A control character (for example an embedded newline in a paste) can never form a
            // valid partial numeric state, so this is just another rejection path.
            return false;
        }

        if (!candidate.IsChanged || !IsValidPartialState(candidate.Text))
        {
            return false;
        }

        Text = candidate.Text;
        Selection = candidate.Selection;
        return true;
    }

    /// <summary>Deletes the selection, or the complete grapheme before the caret.</summary>
    /// <returns>True when the buffer changed.</returns>
    public bool Backspace()
    {
        var result = Edit.Backspace(Text, Selection);

        if (!result.IsChanged)
        {
            return false;
        }

        Text = result.Text;
        Selection = result.Selection;
        return true;
    }

    /// <summary>Deletes the selection, or the complete grapheme after the caret.</summary>
    /// <returns>True when the buffer changed.</returns>
    public bool Delete()
    {
        var result = Edit.Delete(Text, Selection);

        if (!result.IsChanged)
        {
            return false;
        }

        Text = result.Text;
        Selection = result.Selection;
        return true;
    }

    /// <summary>Moves or extends the caret to the previous complete grapheme.</summary>
    /// <param name="extend">Whether to retain the anchor.</param>
    /// <returns>True when the selection changed.</returns>
    public bool MovePrevious(bool extend)
    {
        var result = Edit.MovePrevious(Text, Selection, extend);

        if (!result.IsChanged)
        {
            return false;
        }

        Selection = result.Selection;
        return true;
    }

    /// <summary>Moves or extends the caret to the next complete grapheme.</summary>
    /// <param name="extend">Whether to retain the anchor.</param>
    /// <returns>True when the selection changed.</returns>
    public bool MoveNext(bool extend)
    {
        var result = Edit.MoveNext(Text, Selection, extend);

        if (!result.IsChanged)
        {
            return false;
        }

        Selection = result.Selection;
        return true;
    }

    /// <summary>Collapses the selection to a caret at a known grapheme boundary, such as one located
    /// by <see cref="IndexAtColumn"/>.</summary>
    /// <param name="index">The zero-based UTF-16 index to place the caret at.</param>
    public void SetCaret(int index)
    {
        var clamped = Math.Clamp(index, 0, Text.Length);
        Selection = new Selection(clamped, clamped);
    }

    /// <summary>Resolves the grapheme boundary whose rendered column range contains a column.</summary>
    /// <param name="column">The zero-based column within the field's content area.</param>
    /// <param name="ambiguousWidth">The caller's ambient East Asian Ambiguous width policy.</param>
    /// <returns>The UTF-16 index of the grapheme occupying that column, or the buffer length past the last one.</returns>
    public int IndexAtColumn(int column, Ambiguous ambiguousWidth)
    {
        var x = 0;

        foreach (var grapheme in Graphemes.Enumerate(Text.AsSpan()))
        {
            var cluster = Text.AsSpan(grapheme.Offset, grapheme.Length);
            var width = Width.Measure(cluster, ambiguousWidth).Cells;

            if (column < x + width)
            {
                return grapheme.Offset;
            }

            x += width;
        }

        return Text.Length;
    }

    /// <summary>Strips group separators, normalizes signs and the decimal separator to invariant
    /// form, and parses the result, guarding overflow so no exception ever escapes.</summary>
    /// <param name="value">Receives the parsed value on success.</param>
    /// <returns>True when the buffer held a complete, parseable number.</returns>
    public bool TryCommit(out decimal value)
    {
        value = default;

        if (Text.Length == 0)
        {
            return false;
        }

        var builder = new StringBuilder(Text.Length);
        var position = 0;

        if (TryConsumeToken(Text, ref position, _format.NegativeSign) ||
            TryConsumeToken(Text, ref position, "-"))
        {
            _ = builder.Append('-');
        }
        else
        {
            _ = TryConsumeToken(Text, ref position, _format.PositiveSign) ||
                TryConsumeToken(Text, ref position, "+");
        }

        while (position < Text.Length)
        {
            if (IsAsciiDigit(Text[position]))
            {
                _ = builder.Append(Text[position]);
                position++;
                continue;
            }

            if (TryConsumeToken(Text, ref position, _format.NumberGroupSeparator))
            {
                continue;
            }

            if (!_integerOnly && TryConsumeToken(Text, ref position, _format.NumberDecimalSeparator))
            {
                _ = builder.Append('.');
                continue;
            }

            return false;
        }

        try
        {
            return decimal.TryParse(
                builder.ToString(),
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out value);
        }
        catch (OverflowException)
        {
            value = default;
            return false;
        }
    }

    /// <summary>Validates a candidate whole buffer against the partial numeric grammar: an optional
    /// leading sign, digits optionally interleaved with a group separator following at least one
    /// digit, and - unless integer-only - one decimal separator followed by digits.</summary>
    private bool IsValidPartialState(string text)
    {
        if (text.Length == 0)
        {
            return true;
        }

        var position = 0;

        _ = TryConsumeToken(text, ref position, _format.NegativeSign) ||
            TryConsumeToken(text, ref position, "-") ||
            TryConsumeToken(text, ref position, _format.PositiveSign) ||
            TryConsumeToken(text, ref position, "+");

        var sawDigit = false;
        var lastWasGroupSeparator = false;

        while (position < text.Length)
        {
            if (IsAsciiDigit(text[position]))
            {
                sawDigit = true;
                lastWasGroupSeparator = false;
                position++;
                continue;
            }

            if (TryConsumeToken(text, ref position, _format.NumberGroupSeparator))
            {
                if (!sawDigit || lastWasGroupSeparator)
                {
                    return false;
                }

                lastWasGroupSeparator = true;
                continue;
            }

            break;
        }

        if (position == text.Length)
        {
            return true;
        }

        if (_integerOnly || !TryConsumeToken(text, ref position, _format.NumberDecimalSeparator))
        {
            return false;
        }

        while (position < text.Length)
        {
            if (!IsAsciiDigit(text[position]))
            {
                return false;
            }

            position++;
        }

        return true;
    }

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';

    private static bool TryConsumeToken(string text, ref int position, string token)
    {
        if (string.IsNullOrEmpty(token) || position + token.Length > text.Length)
        {
            return false;
        }

        if (string.CompareOrdinal(text, position, token, 0, token.Length) != 0)
        {
            return false;
        }

        position += token.Length;
        return true;
    }
}
