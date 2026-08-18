// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Text;

/// <summary>Verifies grapheme-boundary editing, policy, movement, and projection.</summary>
public sealed class EditTests
{
    /// <summary>Verifies anchor/caret direction normalizes without being discarded.</summary>
    [Fact]
    public void Constructor_WhenSelectionIsBackward_PreservesDirectionAndRange()
    {
        var selection = new Selection(anchor: 8, caret: 2);

        selection.Anchor.ShouldBe(8);
        selection.Caret.ShouldBe(2);
        selection.Start.ShouldBe(2);
        selection.Length.ShouldBe(6);
        selection.End.ShouldBe(8);
        selection.IsEmpty.ShouldBeFalse();
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Selection(-1, 0));
    }

    /// <summary>Verifies every direct index must be a complete grapheme boundary.</summary>
    [Fact]
    public void Validate_WhenIndexSplitsClusterOrSurrogate_Throws()
    {
        const string value = "Ae\u0301👩‍💻Z";

        Edit.Validate(value, new Selection(0, value.Length));
        _ = Should.Throw<ArgumentException>(() => Edit.Validate(value, new Selection(2, 2)));
        _ = Should.Throw<ArgumentException>(() => Edit.Validate(value, new Selection(4, 4)));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            Edit.Validate(value, new Selection(value.Length + 1, value.Length + 1)));
    }

    /// <summary>Verifies IsBoundary still resolves lookahead-dependent breaks correctly once it stops
    /// scanning past the candidate index instead of always scanning to the source end:
    /// regional-indicator pairing needs to see whether a run has odd or even parity, and ZWJ
    /// emoji sequences need to see whether an extended-pictographic run follows the joiner.</summary>
    [Fact]
    public void IsBoundary_WhenCandidateIndexIsInsideALookaheadDependentCluster_ReturnsFalse()
    {
        // Two flag emoji: each is a pair of regional indicators forming one cluster.
        const string flags = "\U0001F1EB\U0001F1F7\U0001F1E9\U0001F1EA";
        Edit.IsBoundary(flags, 0).ShouldBeTrue();
        Edit.IsBoundary(flags, 2).ShouldBeFalse();
        Edit.IsBoundary(flags, 4).ShouldBeTrue();
        Edit.IsBoundary(flags, 6).ShouldBeFalse();
        Edit.IsBoundary(flags, 8).ShouldBeTrue();

        // Woman + ZWJ + laptop: one cluster joined through the zero-width joiner.
        const string zwj = "A👩‍💻Z";
        Edit.IsBoundary(zwj, 0).ShouldBeTrue();
        Edit.IsBoundary(zwj, 1).ShouldBeTrue();
        Edit.IsBoundary(zwj, 3).ShouldBeFalse();
        Edit.IsBoundary(zwj, 4).ShouldBeFalse();
        Edit.IsBoundary(zwj, 6).ShouldBeTrue();
    }

    /// <summary>Verifies invalid UTF-16 source units remain individually addressable replacement clusters.</summary>
    [Fact]
    public void MoveNext_WhenTextHasInvalidUtf16_PreservesSourceUnitBoundaries()
    {
        var value = "A\uD800\uDC00\uD800B";
        var first = Edit.MoveNext(value, new Selection(1, 1), extend: false);
        var second = Edit.MoveNext(value, first.Selection, extend: false);

        first.Selection.Caret.ShouldBe(3);
        second.Selection.Caret.ShouldBe(4);
        second.Text.ShouldBeSameAs(value);
    }

    /// <summary>Verifies repeated extension keeps the original anchor in both directions.</summary>
    [Fact]
    public void MovePrevious_WhenExtendingRepeatedly_PreservesAnchorAndCaretDirection()
    {
        const string value = "Ae\u0301界";
        var first = Edit.MovePrevious(value, new Selection(value.Length, value.Length), extend: true);
        var second = Edit.MovePrevious(value, first.Selection, extend: true);

        first.Selection.ShouldBe(new Selection(value.Length, 3));
        second.Selection.ShouldBe(new Selection(value.Length, 1));
        Edit.MoveNext(value, second.Selection, extend: false).Selection.ShouldBe(new Selection(4, 4));
    }

    /// <summary>Verifies the internal unchecked variants match their validating counterparts for
    /// already-valid selections, and skip the boundary re-check entirely -- the point of the
    /// TextInput fast path, since TextInput's own selection is always already valid.</summary>
    [Fact]
    public void MoveUnchecked_WhenSelectionIsValid_MatchesValidatingCounterpart()
    {
        const string value = "Ae\u0301\u754C";
        var selection = new Selection(3, 3);

        Edit.MovePreviousUnchecked(value, selection, extend: false)
            .ShouldBe(Edit.MovePrevious(value, selection, extend: false));
        Edit.MoveNextUnchecked(value, selection, extend: false)
            .ShouldBe(Edit.MoveNext(value, selection, extend: false));
    }

    /// <summary>Verifies the unchecked variants skip Validate: an endpoint that splits a grapheme
    /// cluster would make the validating counterpart throw, but the unchecked one does not.</summary>
    [Fact]
    public void MoveUnchecked_WhenSelectionSplitsACluster_DoesNotThrow()
    {
        const string value = "Ae\u0301\u754C";
        var splitting = new Selection(2, 2);

        // Index 2 sits between "e" and its combining acute; Validate rejects it as a non-boundary.
        // The unchecked path trusts the caller instead of re-scanning for one.
        _ = Should.Throw<ArgumentException>(() => Edit.Validate(value, splitting));
        _ = Should.NotThrow(() => Edit.MovePreviousUnchecked(value, splitting, extend: false));
        _ = Should.NotThrow(() => Edit.MoveNextUnchecked(value, splitting, extend: false));
    }

    /// <summary>Verifies Backspace and Delete remove whole extended grapheme clusters.</summary>
    [Fact]
    public void Delete_WhenCaretTouchesComplexClusters_RemovesCompleteCluster()
    {
        const string value = "A👩‍💻e\u0301Z";
        var afterBackspace = Edit.Backspace(value, new Selection(8, 8));
        var afterDelete = Edit.Delete(afterBackspace.Text, new Selection(1, 1));

        afterBackspace.Text.ShouldBe("A👩‍💻Z");
        afterBackspace.Selection.ShouldBe(new Selection(6, 6));
        afterDelete.Text.ShouldBe("AZ");
        afterDelete.Selection.ShouldBe(new Selection(1, 1));
    }

    /// <summary>
    /// Verifies deleting a character that separates two grapheme clusters which then merge across
    /// the seam (Hangul jamo composition: a lone leading consonant and a lone vowel become one
    /// composed syllable once nothing separates them) does not throw, even though the old caret
    /// index is no longer a grapheme boundary once the merge happens. Backspace/Delete/Replace
    /// only ever validated the *old* text's boundaries; the composed result can invalidate them.
    /// </summary>
    [Fact]
    public void Backspace_WhenDeletionMergesSurroundingClustersAcrossTheSeam_DoesNotThrow()
    {
        const string value = "\u1100M\u1161"; // lone Hangul leading consonant + 'M' + lone vowel

        var result = Edit.Backspace(value, new Selection(2, 2));

        // The jamo are not normalized into one precomposed code point - they simply become
        // adjacent - but Hangul grapheme-break rules (GB6) still treat the pair as one cluster.
        result.Text.ShouldBe("\u1100\u1161");
        Edit.IsBoundary(result.Text, result.Selection.Caret).ShouldBeTrue();
    }

    /// <summary>
    /// Verifies a bounded paste whose truncation point is computed only from the pasted text in
    /// isolation, but whose actual splice point sits inside a regional-indicator (flag) run that
    /// spans the pasted text and the retained suffix, does not throw. The suffix already contains
    /// a lone regional indicator that the truncated replacement's own trailing indicator pairs
    /// with once composed, which is not visible when segmenting the replacement alone.
    /// </summary>
    [Fact]
    public void Replace_WhenTruncationBoundaryMergesWithRetainedSuffix_DoesNotThrow()
    {
        const string suffix = "\U0001F1EDZ"; // lone regional indicator 'H' + 'Z'
        const string replacement = "\U0001F1E9\U0001F1EA\U0001F1EC"; // regional indicators D, E, G

        var result = Edit.Replace(
            suffix,
            new Selection(0, 0),
            replacement,
            maxLength: 4,
            acceptsReturn: false,
            acceptsTab: false);

        Edit.IsBoundary(result.Text, result.Selection.Caret).ShouldBeTrue();
    }

    /// <summary>Verifies replacement collapses selection after the inserted grapheme-safe text.</summary>
    [Fact]
    public void Replace_WhenSelectionExists_ReplacesAtomicallyAndCollapsesCaret()
    {
        var result = Edit.Replace(
            "A界Z",
            new Selection(anchor: 2, caret: 1),
            "e\u0301");

        result.Text.ShouldBe("Ae\u0301Z");
        result.Selection.ShouldBe(new Selection(3, 3));
        result.IsChanged.ShouldBeTrue();
    }

    /// <summary>Verifies maximum length truncates only at complete replacement graphemes.</summary>
    [Fact]
    public void Replace_WhenMaximumWouldBeExceeded_TruncatesAtGraphemeBoundary()
    {
        var result = Edit.Replace(
            "AB",
            new Selection(2, 2),
            "e\u0301界Z",
            maxLength: 4);

        result.Text.ShouldBe("ABe\u0301界");
        Edit.GraphemeCount(result.Text).ShouldBe(4);
        result.Selection.Caret.ShouldBe(result.Text.Length);
    }

    /// <summary>Verifies newline and tab policy rejects the complete proposal before mutation.</summary>
    [Fact]
    public void Replace_WhenControlPolicyRejectsInput_ThrowsWithoutResult()
    {
        _ = Should.Throw<ArgumentException>(() => Edit.Replace(
            "safe",
            new Selection(4, 4),
            "\n"));
        _ = Should.Throw<ArgumentException>(() => Edit.Replace(
            "safe",
            new Selection(4, 4),
            "\t",
            acceptsReturn: true));

        Edit.Replace(
            "safe",
            new Selection(4, 4),
            "\r\n\t",
            acceptsReturn: true,
            acceptsTab: true).Text.ShouldBe("safe\r\n\t");
    }

    /// <summary>Verifies control characters other than CR, LF, and tab are always rejected, even
    /// with both accepted, since they paint nothing and would silently corrupt the value and
    /// freeze the caret at that index.</summary>
    [Theory]
    [InlineData('\u0000')] // NUL
    [InlineData('\u0007')] // BEL
    [InlineData('\u001b')] // ESC
    [InlineData('\u007f')] // DEL
    [InlineData('\u0085')] // NEL
    public void Replace_WhenNonLineOrTabControlCharacterIsProposed_ThrowsEvenWhenAccepted(char character)
    {
        _ = Should.Throw<ArgumentException>(() => Edit.Replace(
            "safe",
            new Selection(4, 4),
            $"a{character}b",
            acceptsReturn: true,
            acceptsTab: true));
    }

    /// <summary>Verifies Home and End move within CRLF-delimited logical lines.</summary>
    [Fact]
    public void MoveHome_WhenTextIsMultiline_UsesLogicalLineBoundaries()
    {
        const string value = "one\r\ntwo\nthree";

        Edit.MoveHome(value, new Selection(8, 8), extend: false)
            .Selection.Caret.ShouldBe(5);
        Edit.MoveEnd(value, new Selection(6, 6), extend: false)
            .Selection.Caret.ShouldBe(8);
    }

    /// <summary>Verifies word movement crosses punctuation, spaces, and Unicode letters by cluster.</summary>
    [Fact]
    public void MoveWord_WhenTextHasMixedClasses_StopsAtWordBoundaries()
    {
        const string value = "one,  世界!";

        var next = Edit.MoveNextWord(value, new Selection(0, 0), extend: false);
        var nextAgain = Edit.MoveNextWord(value, next.Selection, extend: false);

        next.Selection.Caret.ShouldBe(6);
        nextAgain.Selection.Caret.ShouldBe(9);
        Edit.MovePreviousWord(value, nextAgain.Selection, extend: false)
            .Selection.Caret.ShouldBe(6);
    }

    /// <summary>Verifies word selection returns complete Unicode clusters and one non-word grapheme.</summary>
    [Fact]
    public void SelectWord_WhenIndexTouchesMixedText_ReturnsGraphemeAlignedToken()
    {
        const string value = "one cafe\u0301!";

        Edit.SelectWord(value, 1).ShouldBe(new Selection(0, 3));
        Edit.SelectWord(value, 6).ShouldBe(new Selection(4, 9));
        Edit.SelectWord(value, 9).ShouldBe(new Selection(9, 10));
        Edit.SelectWord(value, value.Length).ShouldBe(new Selection(value.Length, value.Length));
        _ = Should.Throw<ArgumentException>(() => Edit.SelectWord(value, 8));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Edit.SelectWord(value, value.Length + 1));
    }

    /// <summary>Verifies password projection emits one caller-selected Rune per grapheme.</summary>
    [Fact]
    public void ProjectPassword_WhenTextIsComplex_MasksWithoutSourceText()
    {
        Edit.ProjectPassword("Ae\u0301👩‍💻\uD800", new Rune('●')).ShouldBe("●●●●");
        _ = Should.Throw<ArgumentException>(() => Edit.ProjectPassword("secret", new Rune('\n')));
    }

    /// <summary>Verifies immutable results are safe caller-owned undo and redo snapshots.</summary>
    [Fact]
    public void Replace_WhenSnapshotsAreRetained_RemainsDeterministicAndIndependent()
    {
        var original = new EditResult("A", new Selection(1, 1), changed: false);
        var edited = Edit.Replace(original.Text, original.Selection, "界");

        original.Text.ShouldBe("A");
        original.Selection.ShouldBe(new Selection(1, 1));
        edited.Text.ShouldBe("A界");
        Edit.Replace(original.Text, original.Selection, "界").ShouldBe(edited);
    }

    private const int _caseCount = 10_000;
    private const int _seed = 0x00ED_175A;

    private static readonly string[] _insertions =
    [
        "a",
        "界",
        "e\u0301",
        "👩‍💻",
        "🇵🇹",
        "\uD800",
        ""
    ];

    /// <summary>Verifies mixed operations never create a split index or exceed maximum length.</summary>
    [Fact]
    public void Apply_WhenOperationsAreRandomized_PreservesEveryEditInvariant()
    {
        var first = Replay(_seed);
        var second = Replay(_seed);

        second.ShouldBe(first);
    }

    private static EditResult Replay(int seed)
    {
        var random = new Random(seed);
        var state = new EditResult(string.Empty, new Selection(0, 0), changed: false);

        for (var sample = 0; sample < _caseCount; sample++)
        {
            state = random.Next(0, 5) switch
            {
                0 => Edit.Replace(
                    state.Text,
                    state.Selection,
                    _insertions[random.Next(_insertions.Length)],
                    maxLength: 64),
                1 => Edit.Backspace(state.Text, state.Selection),
                2 => Edit.Delete(state.Text, state.Selection),
                3 => Edit.MovePrevious(state.Text, state.Selection, random.Next(0, 2) == 0),
                _ => Edit.MoveNext(state.Text, state.Selection, random.Next(0, 2) == 0)
            };

            var context = $"seed=0x{seed:X8}, case={sample}";
            Edit.Validate(state.Text, state.Selection);
            Edit.GraphemeCount(state.Text).ShouldBeLessThanOrEqualTo(64, context);
            Boundary(state.Text, state.Selection.Anchor).ShouldBeTrue(context);
            Boundary(state.Text, state.Selection.Caret).ShouldBeTrue(context);
        }

        return state;
    }

    private static bool Boundary(string value, int index)
    {
        if (index is 0 || index == value.Length)
        {
            return true;
        }

        foreach (var grapheme in Graphemes.Enumerate(value))
        {
            if (grapheme.Offset == index)
            {
                return true;
            }
        }

        return false;
    }
}
