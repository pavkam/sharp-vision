// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;
using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>Provides pure grapheme-boundary text navigation and mutation transactions.</summary>
[PublicAPI]
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
    [Pure]
    public static bool IsBoundary(string text, int index) => IsBoundaryCore(text, index, out _);

    /// <summary>
    /// Implements <see cref="IsBoundary"/>, additionally reporting how many graphemes were
    /// enumerated. Kept as a pure function returning that count - rather than a static counter
    /// field - so this stateless, thread-safe class stays that way; a mutable field shared across
    /// every caller would make every consumer of <see cref="IsBoundary"/> a potential data race
    /// just to let a test observe the early-exit bound described below.
    /// </summary>
    /// <param name="text">The non-null UTF-16 source.</param>
    /// <param name="index">The candidate UTF-16 index.</param>
    /// <param name="iterations">
    /// Receives the number of graphemes enumerated. Exposed internally only so a test can prove
    /// the early-exit bound stays proportional to how close <paramref name="index"/> is to the
    /// start of the source, not to the source's total length, without a flaky wall-clock timing
    /// gate.
    /// </param>
    /// <returns>True for the start, end, or start of a segmented cluster.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    [Pure]
    internal static bool IsBoundaryCore(string text, int index, out int iterations)
    {
        ArgumentNullException.ThrowIfNull(text);
        iterations = 0;

        if (index < 0 || index > text.Length)
        {
            return false;
        }

        if (index is 0 || index == text.Length)
        {
            return true;
        }

        // Stops once a cluster starts past index instead of always enumerating the whole source.
        // Grapheme offsets are strictly increasing, so once a cluster starts past index no later
        // cluster can start at index either. The enumerator still sees the full source (only the
        // loop exits early), so lookahead-dependent boundary rules near index — regional-indicator
        // pairs, extended-pictographic ZWJ sequences — resolve exactly as they would scanning to
        // the end; only the wasted work past the answer is skipped. Previously this always scanned
        // the whole source regardless of index, so a single boundary check near the start of a
        // large document cost the same as one at the end, and every caller that validates one
        // endpoint near the caret (Validate, above, on every navigation and edit call) paid for the
        // whole document each time.
        foreach (var grapheme in Graphemes.Enumerate(text))
        {
            iterations++;

            if (grapheme.Offset == index)
            {
                return true;
            }

            if (grapheme.Offset > index)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Counts complete extended grapheme clusters without allocating per cluster.</summary>
    /// <param name="text">The non-null UTF-16 source.</param>
    /// <returns>The non-negative cluster count.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    [Pure]
    [NonNegativeValue]
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
    [Pure]
    public static EditResult MovePrevious(string text, Selection selection, bool extend)
    {
        Validate(text, selection);
        return MovePreviousUnchecked(text, selection, extend);
    }

    /// <summary>Moves or extends to the previous complete cluster, without re-validating that
    /// <paramref name="selection"/> is already a contained grapheme boundary.</summary>
    /// <param name="text">The non-null source.</param>
    /// <param name="selection">A selection already known valid for <paramref name="text"/>, such as
    /// one this API previously returned.</param>
    /// <param name="extend">Whether to retain the anchor.</param>
    /// <returns>The immutable navigation result.</returns>
    /// <remarks>
    /// Skips the two full-prefix <see cref="IsBoundary"/> scans <see cref="Validate"/> performs on
    /// every call. Internal callers that maintain their own selection as an invariant (see
    /// <c>TextInput</c>) can hold Left/Right through a large document without paying for that
    /// re-validation on every repeat.
    /// </remarks>
    [Pure]
    internal static EditResult MovePreviousUnchecked(string text, Selection selection, bool extend)
    {
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
    [Pure]
    public static EditResult MoveNext(string text, Selection selection, bool extend)
    {
        Validate(text, selection);
        return MoveNextUnchecked(text, selection, extend);
    }

    /// <summary>Moves or extends to the next complete cluster, without re-validating that
    /// <paramref name="selection"/> is already a contained grapheme boundary.</summary>
    /// <param name="text">The non-null source.</param>
    /// <param name="selection">A selection already known valid for <paramref name="text"/>, such as
    /// one this API previously returned.</param>
    /// <param name="extend">Whether to retain the anchor.</param>
    /// <returns>The immutable navigation result.</returns>
    /// <remarks>See <see cref="MovePreviousUnchecked"/>.</remarks>
    [Pure]
    internal static EditResult MoveNextUnchecked(string text, Selection selection, bool extend)
    {
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
    [Pure]
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
    [Pure]
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
    [Pure]
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
    [Pure]
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
    [Pure]
    public static Selection SelectWord(string text, int index)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (index < 0 || index > text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index,
                "The word index must be contained by the source.");
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
    [Pure]
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
    [Pure]
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
    /// <exception cref="ArgumentException">ControlBase policy rejects input or retained text exceeds maximum.</exception>
    [Pure]
    public static EditResult Replace(
        string text,
        Selection selection,
        string replacement,
        [NonNegativeValue] int maxLength = 0,
        bool acceptsReturn = false,
        bool acceptsTab = false) =>
        ReplaceCore(text, selection, replacement, maxLength, acceptsReturn, acceptsTab, out _);

    /// <summary>
    /// Implements <see cref="Replace"/>, additionally reporting how many times the private
    /// grapheme-counting helper ran. Kept as a pure function returning that count - rather than a
    /// static counter field - so this stateless, thread-safe class stays that way (see the
    /// matching remark on <see cref="IsBoundaryCore"/>).
    /// </summary>
    /// <param name="text">The non-null source.</param>
    /// <param name="selection">The valid range to replace.</param>
    /// <param name="replacement">The non-null UTF-16 replacement.</param>
    /// <param name="maxLength">Zero for unlimited, otherwise the maximum grapheme count.</param>
    /// <param name="acceptsReturn">Whether CR and LF are accepted.</param>
    /// <param name="acceptsTab">Whether tab is accepted.</param>
    /// <param name="graphemeCountCalls">
    /// Receives the number of grapheme-counting calls performed. Exposed internally only so a
    /// test can prove an unbounded (<paramref name="maxLength"/> zero) call skips the
    /// retained-length scan entirely instead of merely running fast, without a flaky wall-clock
    /// timing comparison.
    /// </param>
    /// <returns>The immutable replacement result, truncated only at a grapheme boundary.</returns>
    /// <exception cref="ArgumentNullException">A string is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is negative.</exception>
    /// <exception cref="ArgumentException">ControlBase policy rejects input or retained text exceeds maximum.</exception>
    [Pure]
    internal static EditResult ReplaceCore(
        string text,
        Selection selection,
        string replacement,
        [NonNegativeValue] int maxLength,
        bool acceptsReturn,
        bool acceptsTab,
        out int graphemeCountCalls)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);
        Validate(text, selection);
        ValidateControls(replacement, acceptsReturn, acceptsTab);
        graphemeCountCalls = 0;
        var allowed = int.MaxValue;

        if (maxLength > 0)
        {
            var retained = Count(text.AsSpan()) - Count(text.AsSpan(selection.Start, selection.Length));
            graphemeCountCalls = 2;

            if (retained > maxLength)
            {
                throw new ArgumentException(
                    "Text retained outside the selection already exceeds maximum length.",
                    nameof(maxLength));
            }

            allowed = maxLength - retained;
        }
        var replacementLength = Prefix(replacement, allowed);
        var next = string.Concat(
            text.AsSpan(0, selection.Start),
            replacement.AsSpan(0, replacementLength),
            text.AsSpan(selection.End));
        var caret = checked(selection.Start + replacementLength);

        // selection.Start and selection.End are boundaries of the OLD text; several grapheme
        // segmentation rules are context-dependent on what surrounds a cluster (Hangul jamo
        // composition, regional-indicator flag pairing, ZWJ attachment), so removing or replacing
        // the content between them can make the prefix and suffix newly adjacent and merge across
        // that seam into one cluster the old boundary no longer falls on. Snapping caret back to
        // the nearest actual boundary in the composed text - rather than assuming the raw old
        // index still is one - keeps an ordinary Backspace/Delete/paste from throwing instead of
        // completing the edit.
        if (!IsBoundary(next, caret))
        {
            caret = PreviousBoundary(next, caret);
        }

        var nextSelection = new Selection(caret, caret);
        var changed = !string.Equals(text, next, StringComparison.Ordinal) || selection != nextSelection;

        return changed
            ? new EditResult(next, nextSelection, changed: true)
            : Unchanged(text, selection);
    }

    /// <summary>Projects one printable mask Rune per source grapheme.</summary>
    /// <param name="text">The non-null source whose content is never copied to output.</param>
    /// <param name="mask">The printable one-cell mask.</param>
    /// <param name="ambiguousWidth">
    /// The caller's ambient East Asian Ambiguous width policy - the mask must occupy one cell
    /// under the same policy that will measure and draw it, not always <see cref="Ambiguous.Narrow"/>.
    /// </param>
    /// <returns>The owned masked projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException">The mask is a control or not one cell wide.</exception>
    /// <exception cref="OverflowException">The projected UTF-16 length exceeds an integer.</exception>
    [Pure]
    public static string ProjectPassword(string text, Rune mask, Ambiguous ambiguousWidth = Ambiguous.Narrow)
    {
        ArgumentNullException.ThrowIfNull(text);
        Span<char> encoded = stackalloc char[2];
        var encodedLength = mask.EncodeToUtf16(encoded);
        var measurement = Width.Measure(encoded[..encodedLength], ambiguousWidth);

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

    [Pure]
    private static EditResult Move(string text, Selection previous, int caret, bool extend)
    {
        var next = extend ? new Selection(previous.Anchor, caret) : new Selection(caret, caret);
        return next == previous
            ? Unchanged(text, previous)
            : new EditResult(text, next, changed: true);
    }

    [Pure]
    private static EditResult Unchanged(string text, Selection selection) =>
        new(text, selection, changed: false);

    [Pure]
    [NonNegativeValue]
    private static int PreviousBoundary(string text, int index)
    {
        var previous = 0;

        foreach (var grapheme in Graphemes.Enumerate(text.AsSpan(0, index)))
        {
            previous = grapheme.Offset;
        }

        return previous;
    }

    [Pure]
    [NonNegativeValue]
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

    [Pure]
    [NonNegativeValue]
    private static int SkipForward(string text, int position, int kind)
    {
        while (position < text.Length && Kind(text, position) == kind)
        {
            position = NextBoundary(text, position);
        }

        return position;
    }

    /// <summary>Classifies the grapheme starting at <paramref name="position"/> as word (2),
    /// whitespace (1), or other (0). O(1): decodes only the rune at <paramref name="position"/>.
    /// Exposed internally so <c>TextInput</c> can replicate <see cref="MovePreviousWord"/>'s
    /// classification against its own cached boundary offsets instead of this type's O(n)
    /// <see cref="PreviousBoundary"/> scan.</summary>
    [Pure]
    [ValueRange(0, 2)]
    internal static int Kind(string text, int position)
    {
        var status = Rune.DecodeFromUtf16(text.AsSpan(position), out var rune, out _);

        return status != OperationStatus.Done ? 0 :
            Rune.IsLetterOrDigit(rune) || rune.Value == '_' ? 2 :
            Rune.IsWhiteSpace(rune) ? 1 : 0;
    }

    [Pure]
    [NonNegativeValue]
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

    [Pure]
    [NonNegativeValue]
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

        // CR, LF, and tab are the only control characters this policy admits, and only when
        // explicitly accepted above; every other control cluster (ESC, DEL, NEL, LS/PS, ...) would
        // otherwise be stored invisibly with no paint width, corrupting the value and freezing the
        // caret at that index. Classify with the same grapheme-break data the renderer
        // itself uses, so this policy can never diverge from what actually paints nothing.
        foreach (var segment in Graphemes.Enumerate(value.AsSpan()))
        {
            var cluster = value.AsSpan(segment.Offset, segment.Length);

            // CRLF forms a single grapheme cluster, so this must clear the whole cluster - not just
            // its first character - to keep admitting it once accepted above.
            if (cluster.IndexOfAnyExcept('\r', '\n', '\t') < 0)
            {
                continue;
            }

            if (Width.Measure(cluster, Ambiguous.Narrow).Controls > 0)
            {
                throw new ArgumentException("Replacement contains a control character.", nameof(value));
            }
        }
    }
}
