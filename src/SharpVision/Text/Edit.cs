// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;



/// <summary>Provides pure grapheme-boundary text navigation and mutation transactions.</summary>
public static class Edit
{
    /// <summary>Validates that both selection endpoints are contained grapheme boundaries.</summary>
    /// <param name="text">The non-null UTF-16 source.</param>
    /// <param name="selection">The proposed directional selection.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An endpoint exceeds the source.</exception>
    /// <exception cref="ArgumentException">An endpoint splits a grapheme cluster.</exception>
    public static void Validate(string text, Selection selection)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (selection.Anchor > text.Length || selection.Caret > text.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selection),
                selection,
                "Selection endpoints must not exceed the UTF-16 source length.");
        }

        if (!IsBoundary(text, selection.Anchor) || !IsBoundary(text, selection.Caret))
        {
            throw new ArgumentException(
                "Selection endpoints must be complete grapheme boundaries.",
                nameof(selection));
        }
    }

    /// <summary>Gets whether an index is a contained extended-grapheme boundary.</summary>
    /// <param name="text">The non-null UTF-16 source.</param>
    /// <param name="index">The candidate UTF-16 index.</param>
    /// <returns>True for the start, end, or start of a segmented cluster.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public static bool IsBoundary(string text, int index)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (index < 0 || index > text.Length)
        {
            return false;
        }

        if (index is 0 || index == text.Length)
        {
            return true;
        }

        foreach (var grapheme in Graphemes.Enumerate(text))
        {
            if (grapheme.Offset == index)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Counts complete extended grapheme clusters without allocating per cluster.</summary>
    /// <param name="text">The non-null UTF-16 source.</param>
    /// <returns>The non-negative cluster count.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public static int GraphemeCount(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Count(text.AsSpan());
    }

    /// <summary>Moves or extends to the previous complete cluster.</summary>
    /// <param name="text">The non-null source.</param>
    /// <param name="selection">The valid current selection.</param>
    /// <param name="extend">Whether to retain the anchor.</param>
    /// <returns>The immutable navigation result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException">An endpoint splits a grapheme.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An endpoint exceeds the source.</exception>
    public static EditResult MovePrevious(string text, Selection selection, bool extend)
    {
        Validate(text, selection);
        var caret = !extend && !selection.IsEmpty
            ? selection.Start
            : PreviousBoundary(text, selection.Caret);
        return Move(text, selection, caret, extend);
    }

    /// <summary>Moves or extends to the next complete cluster.</summary>
    /// <param name="text">The non-null source.</param>
    /// <param name="selection">The valid current selection.</param>
    /// <param name="extend">Whether to retain the anchor.</param>
    /// <returns>The immutable navigation result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException">An endpoint splits a grapheme.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An endpoint exceeds the source.</exception>
    public static EditResult MoveNext(string text, Selection selection, bool extend)
    {
        Validate(text, selection);
        var caret = !extend && !selection.IsEmpty
            ? selection.End
            : NextBoundary(text, selection.Caret);
        return Move(text, selection, caret, extend);
    }

    /// <summary>Moves or extends to the start of the current logical line.</summary>
    /// <param name="text">The non-null source.</param>
    /// <param name="selection">The valid current selection.</param>
    /// <param name="extend">Whether to retain the anchor.</param>
    /// <returns>The immutable navigation result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException">An endpoint splits a grapheme.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An endpoint exceeds the source.</exception>
    public static EditResult MoveHome(string text, Selection selection, bool extend)
    {
        Validate(text, selection);
        var separator = text.AsSpan(0, selection.Caret).LastIndexOfAny('\r', '\n');
        return Move(text, selection, separator < 0 ? 0 : separator + 1, extend);
    }

    /// <summary>Moves or extends to the end of the current logical line.</summary>
    /// <param name="text">The non-null source.</param>
    /// <param name="selection">The valid current selection.</param>
    /// <param name="extend">Whether to retain the anchor.</param>
    /// <returns>The immutable navigation result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException">An endpoint splits a grapheme.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An endpoint exceeds the source.</exception>
    public static EditResult MoveEnd(string text, Selection selection, bool extend)
    {
        Validate(text, selection);
        var relative = text.AsSpan(selection.Caret).IndexOfAny('\r', '\n');
        var caret = relative < 0 ? text.Length : selection.Caret + relative;
        return Move(text, selection, caret, extend);
    }

    /// <summary>Moves or extends to the next Unicode word start or source end.</summary>
    /// <param name="text">The non-null source.</param>
    /// <param name="selection">The valid current selection.</param>
    /// <param name="extend">Whether to retain the anchor.</param>
    /// <returns>The immutable navigation result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException">An endpoint splits a grapheme.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An endpoint exceeds the source.</exception>
    public static EditResult MoveNextWord(string text, Selection selection, bool extend)
    {
        Validate(text, selection);
        var position = !extend && !selection.IsEmpty ? selection.End : selection.Caret;

        if (position < text.Length && Kind(text, position) == 2)
        {
            position = SkipForward(text, position, kind: 2);
        }

        while (position < text.Length && Kind(text, position) != 2)
        {
            position = NextBoundary(text, position);
        }

        return Move(text, selection, position, extend);
    }

    /// <summary>Moves or extends to the previous Unicode word start or source start.</summary>
    /// <param name="text">The non-null source.</param>
    /// <param name="selection">The valid current selection.</param>
    /// <param name="extend">Whether to retain the anchor.</param>
    /// <returns>The immutable navigation result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException">An endpoint splits a grapheme.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An endpoint exceeds the source.</exception>
    public static EditResult MovePreviousWord(string text, Selection selection, bool extend)
    {
        Validate(text, selection);
        var position = !extend && !selection.IsEmpty ? selection.Start : selection.Caret;

        while (position > 0 && Kind(text, PreviousBoundary(text, position)) != 2)
        {
            position = PreviousBoundary(text, position);
        }

        while (position > 0 && Kind(text, PreviousBoundary(text, position)) == 2)
        {
            position = PreviousBoundary(text, position);
        }

        return Move(text, selection, position, extend);
    }

    /// <summary>Selects the complete Unicode-safe word or non-word grapheme at one boundary.</summary>
    /// <param name="text">The non-null source.</param>
    /// <param name="index">The contained grapheme start, or the source end.</param>
    /// <returns>
    /// A forward word range for letters, digits, and underscore; one complete non-word grapheme;
    /// or an empty range at the source end.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the source.</exception>
    /// <exception cref="ArgumentException"><paramref name="index"/> splits a grapheme cluster.</exception>
    public static Selection SelectWord(string text, int index)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (index < 0 || index > text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The word index must be contained by the source.");
        }

        if (!IsBoundary(text, index))
        {
            throw new ArgumentException("The word index must be a complete grapheme boundary.", nameof(index));
        }

        if (index == text.Length)
        {
            return new Selection(index, index);
        }

        if (Kind(text, index) != 2)
        {
            return new Selection(index, NextBoundary(text, index));
        }

        var start = index;

        while (start > 0 && Kind(text, PreviousBoundary(text, start)) == 2)
        {
            start = PreviousBoundary(text, start);
        }

        return new Selection(start, SkipForward(text, index, kind: 2));
    }

    /// <summary>Deletes the selection or complete cluster before the caret.</summary>
    /// <param name="text">The non-null source.</param>
    /// <param name="selection">The valid current selection.</param>
    /// <returns>The immutable deletion result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException">An endpoint splits a grapheme.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An endpoint exceeds the source.</exception>
    public static EditResult Backspace(string text, Selection selection)
    {
        Validate(text, selection);

        if (!selection.IsEmpty)
        {
            return Replace(text, selection, string.Empty);
        }

        var previous = PreviousBoundary(text, selection.Caret);
        return previous == selection.Caret
            ? Unchanged(text, selection)
            : Replace(text, new Selection(previous, selection.Caret), string.Empty);
    }

    /// <summary>Deletes the selection or complete cluster after the caret.</summary>
    /// <param name="text">The non-null source.</param>
    /// <param name="selection">The valid current selection.</param>
    /// <returns>The immutable deletion result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException">An endpoint splits a grapheme.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An endpoint exceeds the source.</exception>
    public static EditResult Delete(string text, Selection selection)
    {
        Validate(text, selection);

        if (!selection.IsEmpty)
        {
            return Replace(text, selection, string.Empty);
        }

        var next = NextBoundary(text, selection.Caret);
        return next == selection.Caret
            ? Unchanged(text, selection)
            : Replace(text, new Selection(selection.Caret, next), string.Empty);
    }

    /// <summary>Replaces a selection under grapheme-count and control-character policy.</summary>
    /// <param name="text">The non-null source.</param>
    /// <param name="selection">The valid range to replace.</param>
    /// <param name="replacement">The non-null UTF-16 replacement.</param>
    /// <param name="maxLength">Zero for unlimited, otherwise the maximum grapheme count.</param>
    /// <param name="acceptsReturn">Whether CR and LF are accepted.</param>
    /// <param name="acceptsTab">Whether tab is accepted.</param>
    /// <returns>The immutable replacement result, truncated only at a grapheme boundary.</returns>
    /// <exception cref="ArgumentNullException">A string is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is negative.</exception>
    /// <exception cref="ArgumentException">Control policy rejects input or retained text exceeds maximum.</exception>
    public static EditResult Replace(
        string text,
        Selection selection,
        string replacement,
        int maxLength = 0,
        bool acceptsReturn = false,
        bool acceptsTab = false)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);
        Validate(text, selection);
        ValidateControls(replacement, acceptsReturn, acceptsTab);
        var retained = Count(text.AsSpan()) - Count(text.AsSpan(selection.Start, selection.Length));

        if (maxLength > 0 && retained > maxLength)
        {
            throw new ArgumentException(
                "Text retained outside the selection already exceeds maximum length.",
                nameof(maxLength));
        }

        var allowed = maxLength == 0 ? int.MaxValue : maxLength - retained;
        var replacementLength = Prefix(replacement, allowed);
        var next = string.Concat(
            text.AsSpan(0, selection.Start),
            replacement.AsSpan(0, replacementLength),
            text.AsSpan(selection.End));
        var caret = checked(selection.Start + replacementLength);
        var nextSelection = new Selection(caret, caret);
        var changed = !string.Equals(text, next, StringComparison.Ordinal) || selection != nextSelection;

        return changed
            ? new EditResult(next, nextSelection, changed: true)
            : Unchanged(text, selection);
    }

    /// <summary>Projects one printable narrow mask Rune per source grapheme.</summary>
    /// <param name="text">The non-null source whose content is never copied to output.</param>
    /// <param name="mask">The printable one-cell mask.</param>
    /// <returns>The owned masked projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException">The mask is a control or not one cell wide.</exception>
    /// <exception cref="OverflowException">The projected UTF-16 length exceeds an integer.</exception>
    public static string ProjectPassword(string text, Rune mask)
    {
        ArgumentNullException.ThrowIfNull(text);
        Span<char> encoded = stackalloc char[2];
        var encodedLength = mask.EncodeToUtf16(encoded);
        var measurement = Width.Measure(encoded[..encodedLength]);

        if (measurement.Controls != 0 || measurement.Cells != 1)
        {
            throw new ArgumentException("Password mask must be printable and one cell wide.", nameof(mask));
        }

        var count = Count(text.AsSpan());
        return string.Create(
            checked(count * encodedLength),
            (Mask: mask, Length: encodedLength),
            static (destination, state) =>
            {
                for (var offset = 0; offset < destination.Length; offset += state.Length)
                {
                    _ = state.Mask.EncodeToUtf16(destination[offset..]);
                }
            });
    }

    private static EditResult Move(string text, Selection previous, int caret, bool extend)
    {
        var next = extend ? new Selection(previous.Anchor, caret) : new Selection(caret, caret);
        return next == previous
            ? Unchanged(text, previous)
            : new EditResult(text, next, changed: true);
    }

    private static EditResult Unchanged(string text, Selection selection) =>
        new(text, selection, changed: false);

    private static int PreviousBoundary(string text, int index)
    {
        var previous = 0;

        foreach (var grapheme in Graphemes.Enumerate(text.AsSpan(0, index)))
        {
            previous = grapheme.Offset;
        }

        return previous;
    }

    private static int NextBoundary(string text, int index)
    {
        if (index >= text.Length)
        {
            return text.Length;
        }

        var enumerator = Graphemes.Enumerate(text.AsSpan(index)).GetEnumerator();
        var moved = enumerator.MoveNext();
        Debug.Assert(moved, "A non-empty valid suffix contains one grapheme.");
        return index + enumerator.Current.Length;
    }

    private static int SkipForward(string text, int position, int kind)
    {
        while (position < text.Length && Kind(text, position) == kind)
        {
            position = NextBoundary(text, position);
        }

        return position;
    }

    private static int Kind(string text, int position)
    {
        var status = Rune.DecodeFromUtf16(text.AsSpan(position), out var rune, out _);

        return status != OperationStatus.Done ? 0 : Rune.IsLetterOrDigit(rune) || rune.Value == '_' ? 2 : Rune.IsWhiteSpace(rune) ? 1 : 0;
    }

    private static int Count(ReadOnlySpan<char> value)
    {
        var count = 0;

        foreach (var unused in Graphemes.Enumerate(value))
        {
            _ = unused;
            count = checked(count + 1);
        }

        return count;
    }

    private static int Prefix(string value, int allowed)
    {
        var count = 0;
        var length = 0;

        foreach (var grapheme in Graphemes.Enumerate(value))
        {
            if (count == allowed)
            {
                break;
            }

            count++;
            length = grapheme.Offset + grapheme.Length;
        }

        return length;
    }

    private static void ValidateControls(string value, bool acceptsReturn, bool acceptsTab)
    {
        if (!acceptsReturn && value.AsSpan().IndexOfAny('\r', '\n') >= 0)
        {
            throw new ArgumentException("Replacement contains a line break.", nameof(value));
        }

        if (!acceptsTab && value.Contains('\t', StringComparison.Ordinal))
        {
            throw new ArgumentException("Replacement contains a tab.", nameof(value));
        }
    }
}
