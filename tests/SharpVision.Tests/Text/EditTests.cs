// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Text;

using System.Text;

using SharpVision.Text;

using Shouldly;

/// <summary>Verifies grapheme-boundary editing, policy, movement, and projection.</summary>
public sealed class EditTests
{
    /// <summary>Verifies anchor/caret direction normalizes without being discarded.</summary>
    [Fact]
    public void Constructor_WhenSelectionIsBackward_PreservesDirectionAndRange()
    {
        Selection selection = new Selection(anchor: 8, caret: 2);

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
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => Edit.Validate(value, new Selection(value.Length + 1, value.Length + 1)));
    }

    /// <summary>Verifies invalid UTF-16 source units remain individually addressable replacement clusters.</summary>
    [Fact]
    public void MoveNext_WhenTextHasInvalidUtf16_PreservesSourceUnitBoundaries()
    {
        var value = "A\uD800\uDC00\uD800B";
        EditResult first = Edit.MoveNext(value, new Selection(1, 1), extend: false);
        EditResult second = Edit.MoveNext(value, first.Selection, extend: false);

        first.Selection.Caret.ShouldBe(3);
        second.Selection.Caret.ShouldBe(4);
        second.Text.ShouldBeSameAs(value);
    }

    /// <summary>Verifies repeated extension keeps the original anchor in both directions.</summary>
    [Fact]
    public void MovePrevious_WhenExtendingRepeatedly_PreservesAnchorAndCaretDirection()
    {
        const string value = "Ae\u0301界";
        EditResult first = Edit.MovePrevious(value, new Selection(value.Length, value.Length), extend: true);
        EditResult second = Edit.MovePrevious(value, first.Selection, extend: true);

        first.Selection.ShouldBe(new Selection(value.Length, 3));
        second.Selection.ShouldBe(new Selection(value.Length, 1));
        Edit.MoveNext(value, second.Selection, extend: false).Selection.ShouldBe(new Selection(4, 4));
    }

    /// <summary>Verifies Backspace and Delete remove whole extended grapheme clusters.</summary>
    [Fact]
    public void Delete_WhenCaretTouchesComplexClusters_RemovesCompleteCluster()
    {
        const string value = "A👩‍💻e\u0301Z";
        EditResult afterBackspace = Edit.Backspace(value, new Selection(8, 8));
        EditResult afterDelete = Edit.Delete(afterBackspace.Text, new Selection(1, 1));

        afterBackspace.Text.ShouldBe("A👩‍💻Z");
        afterBackspace.Selection.ShouldBe(new Selection(6, 6));
        afterDelete.Text.ShouldBe("AZ");
        afterDelete.Selection.ShouldBe(new Selection(1, 1));
    }

    /// <summary>Verifies replacement collapses selection after the inserted grapheme-safe text.</summary>
    [Fact]
    public void Replace_WhenSelectionExists_ReplacesAtomicallyAndCollapsesCaret()
    {
        EditResult result = Edit.Replace(
            "A界Z",
            new Selection(anchor: 2, caret: 1),
            "e\u0301");

        result.Text.ShouldBe("Ae\u0301Z");
        result.Selection.ShouldBe(new Selection(3, 3));
        result.Changed.ShouldBeTrue();
    }

    /// <summary>Verifies maximum length truncates only at complete replacement graphemes.</summary>
    [Fact]
    public void Replace_WhenMaximumWouldBeExceeded_TruncatesAtGraphemeBoundary()
    {
        EditResult result = Edit.Replace(
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

        EditResult next = Edit.MoveNextWord(value, new Selection(0, 0), extend: false);
        EditResult nextAgain = Edit.MoveNextWord(value, next.Selection, extend: false);

        next.Selection.Caret.ShouldBe(6);
        nextAgain.Selection.Caret.ShouldBe(9);
        Edit.MovePreviousWord(value, nextAgain.Selection, extend: false)
            .Selection.Caret.ShouldBe(6);
    }

    /// <summary>Verifies password projection emits one caller-selected Rune per grapheme.</summary>
    [Fact]
    public void ProjectPassword_WhenTextIsComplex_MasksWithoutSourceText()
    {
        Edit.ProjectPassword("Ae\u0301👩‍💻\uD800", new Rune('●')).ShouldBe("●●●●");
        _ = Should.Throw<ArgumentException>(
            () => Edit.ProjectPassword("secret", new Rune('\n')));
    }

    /// <summary>Verifies immutable results are safe caller-owned undo and redo snapshots.</summary>
    [Fact]
    public void Replace_WhenSnapshotsAreRetained_RemainsDeterministicAndIndependent()
    {
        EditResult original = new EditResult("A", new Selection(1, 1), changed: false);
        EditResult edited = Edit.Replace(original.Text, original.Selection, "界");

        original.Text.ShouldBe("A");
        original.Selection.ShouldBe(new Selection(1, 1));
        edited.Text.ShouldBe("A界");
        Edit.Replace(original.Text, original.Selection, "界").ShouldBe(edited);
    }
}
