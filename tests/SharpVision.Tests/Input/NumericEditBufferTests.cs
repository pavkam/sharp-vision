// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies the shared transient text-and-selection buffer, partial-state validation, and
/// commit parsing composed into every buffer-then-commit numeric field control (currently
/// NumberInput) - directly against <see cref="NumericEditBuffer"/>, mirroring
/// <see cref="SegmentFieldBehaviorTests"/>'s direct-construction style.</summary>
public sealed class NumericEditBufferTests
{
    private static NumericEditBuffer Create(NumberFormatInfo? format = null, bool integerOnly = false)
    {
        var buffer = new NumericEditBuffer();
        buffer.Configure(format ?? NumberFormatInfo.InvariantInfo, integerOnly);
        return buffer;
    }

    #region Partial-state validation

    /// <summary>Verifies insert when result is valid partial state accepts it.</summary>
    [Theory]
    [InlineData("-")]
    [InlineData("12,")]
    [InlineData("-12")]
    [InlineData("1,234")]
    [InlineData("1,234.56")]
    [InlineData(".5")]
    [InlineData("+12")]
    public void Insert_WhenResultIsValidPartialState_AcceptsIt(string text)
    {
        // Arrange
        var buffer = Create();

        // Act
        var accepted = buffer.Insert(text);

        // Assert
        accepted.ShouldBeTrue();
        buffer.Text.ShouldBe(text);
    }

    /// <summary>Verifies inserting empty text into an already-empty buffer is a genuine no-op,
    /// since nothing actually changes - the empty string is still a valid partial state on its own,
    /// proven separately through <see cref="TryCommit_WhenTextIsEmpty_Fails"/> and
    /// <see cref="Backspace_WhenBufferIsEmpty_ReturnsFalse"/>.</summary>
    [Fact]
    public void Insert_WhenEmptyTextIsInsertedIntoAnEmptyBuffer_ReturnsFalse()
    {
        // Arrange
        var buffer = Create();

        // Act
        var accepted = buffer.Insert(string.Empty);

        // Assert
        accepted.ShouldBeFalse();
        buffer.IsEmpty.ShouldBeTrue();
    }

    /// <summary>Verifies insert when second decimal separator is typed rejects whole result.</summary>
    [Fact]
    public void Insert_WhenSecondDecimalSeparatorIsTyped_RejectsWholeResult()
    {
        // Arrange
        var buffer = Create();
        _ = buffer.Insert("1.5");

        // Act
        var accepted = buffer.Insert(".");

        // Assert
        accepted.ShouldBeFalse();
        buffer.Text.ShouldBe("1.5");
    }

    /// <summary>Verifies insert when sign is misplaced rejects.</summary>
    [Fact]
    public void Insert_WhenSignIsMisplaced_Rejects()
    {
        // Arrange
        var buffer = Create();

        // Act
        var accepted = buffer.Insert("1-2");

        // Assert
        accepted.ShouldBeFalse();
        buffer.IsEmpty.ShouldBeTrue();
    }

    /// <summary>Verifies insert when group separator precedes any digit rejects.</summary>
    [Fact]
    public void Insert_WhenGroupSeparatorPrecedesAnyDigit_Rejects()
    {
        // Arrange
        var buffer = Create();

        // Act
        var accepted = buffer.Insert(",5");

        // Assert
        accepted.ShouldBeFalse();
    }

    /// <summary>Verifies insert when two consecutive group separators are typed rejects.</summary>
    [Fact]
    public void Insert_WhenTwoConsecutiveGroupSeparatorsAreTyped_Rejects()
    {
        // Arrange
        var buffer = Create();
        _ = buffer.Insert("1,");

        // Act
        var accepted = buffer.Insert(",");

        // Assert
        accepted.ShouldBeFalse();
        buffer.Text.ShouldBe("1,");
    }

    /// <summary>Verifies insert when decimal separator is typed and integer only rejects.</summary>
    [Fact]
    public void Insert_WhenDecimalSeparatorIsTypedAndIntegerOnly_Rejects()
    {
        // Arrange
        var buffer = Create(integerOnly: true);
        _ = buffer.Insert("12");

        // Act
        var accepted = buffer.Insert(".");

        // Assert
        accepted.ShouldBeFalse();
        buffer.Text.ShouldBe("12");
    }

    /// <summary>Verifies insert when ascii minus is typed under culture with unicode minus sign accepts.</summary>
    [Fact]
    public void Insert_WhenAsciiMinusIsTypedUnderCultureWithUnicodeMinusSign_Accepts()
    {
        // Arrange - a NegativeSign of U+2212 (unreachable from an ordinary keyboard) must not
        // block the ASCII hyphen-minus a user actually types.
        var format = (NumberFormatInfo) NumberFormatInfo.InvariantInfo.Clone();
        format.NegativeSign = "−";
        var buffer = Create(format);

        // Act
        var accepted = buffer.Insert("-12");

        // Assert
        accepted.ShouldBeTrue();
        buffer.Text.ShouldBe("-12");
    }

    /// <summary>Verifies insert when whole paste is invalid rejects without partially applying it.</summary>
    [Fact]
    public void Insert_WhenWholePasteIsInvalid_RejectsWithoutPartiallyApplyingIt()
    {
        // Arrange
        var buffer = Create();
        _ = buffer.Insert("12");

        // Act
        var accepted = buffer.Insert("3.4.5");

        // Assert
        accepted.ShouldBeFalse();
        buffer.Text.ShouldBe("12");
    }

    /// <summary>Verifies insert when paste is valid replaces selection in full.</summary>
    [Fact]
    public void Insert_WhenPasteIsValid_ReplacesSelectionInFull()
    {
        // Arrange
        var buffer = Create();
        _ = buffer.Insert("1,234.5");

        // Act
        var accepted = buffer.Insert("6");

        // Assert
        accepted.ShouldBeTrue();
        buffer.Text.ShouldBe("1,234.56");
    }

    #endregion

    #region Commit

    /// <summary>Verifies try commit when text is parseable parses stripped and normalized value.</summary>
    [Theory]
    [InlineData("12", 12)]
    [InlineData("-12", -12)]
    [InlineData("1,234", 1234)]
    [InlineData("1,234.56", 1234.56)]
    public void TryCommit_WhenTextIsParseable_ParsesStrippedAndNormalizedValue(string text, decimal expected)
    {
        // Arrange
        var buffer = Create();
        _ = buffer.Insert(text);

        // Act
        var committed = buffer.TryCommit(out var value);

        // Assert
        committed.ShouldBeTrue();
        value.ShouldBe(expected);
    }

    /// <summary>Verifies try commit when text is empty fails.</summary>
    [Fact]
    public void TryCommit_WhenTextIsEmpty_Fails()
    {
        // Arrange
        var buffer = Create();

        // Act
        var committed = buffer.TryCommit(out var value);

        // Assert
        committed.ShouldBeFalse();
        value.ShouldBe(0m);
    }

    /// <summary>Verifies try commit when text is bare sign fails.</summary>
    [Fact]
    public void TryCommit_WhenTextIsBareSign_Fails()
    {
        // Arrange
        var buffer = Create();
        _ = buffer.Insert("-");

        // Act
        var committed = buffer.TryCommit(out _);

        // Assert
        committed.ShouldBeFalse();
    }

    /// <summary>Verifies try commit when magnitude exceeds decimal range fails without throwing.</summary>
    [Fact]
    public void TryCommit_WhenMagnitudeExceedsDecimalRange_FailsWithoutThrowing()
    {
        // Arrange - one digit past decimal.MaxValue's magnitude.
        var buffer = Create();
        var overflowing = decimal.MaxValue.ToString("F0", CultureInfo.InvariantCulture) + "9";
        _ = buffer.Insert(overflowing);

        // Act
        var committed = Should.NotThrow(() => buffer.TryCommit(out _));

        // Assert
        committed.ShouldBeFalse();
    }

    /// <summary>Verifies try commit when ascii minus was typed under unicode minus sign culture normalizes at commit.</summary>
    [Fact]
    public void TryCommit_WhenAsciiMinusWasTypedUnderUnicodeMinusSignCulture_NormalizesAtCommit()
    {
        // Arrange
        var format = (NumberFormatInfo) NumberFormatInfo.InvariantInfo.Clone();
        format.NegativeSign = "−";
        var buffer = Create(format);
        _ = buffer.Insert("-5");

        // Act
        var committed = buffer.TryCommit(out var value);

        // Assert
        committed.ShouldBeTrue();
        value.ShouldBe(-5m);
    }

    #endregion

    #region Culture separators

    /// <summary>Verifies insert when culture is german and text uses de style separators accepts and commits correctly.</summary>
    [Fact]
    public void Insert_WhenCultureIsGermanAndTextUsesDeStyleSeparators_AcceptsAndCommitsCorrectly()
    {
        // Arrange - de-DE: '.' groups, ',' is the decimal separator.
        var format = new CultureInfo("de-DE").NumberFormat;
        var buffer = Create(format);

        // Act
        var accepted = buffer.Insert("1.234,56");
        var committed = buffer.TryCommit(out var value);

        // Assert
        accepted.ShouldBeTrue();
        committed.ShouldBeTrue();
        value.ShouldBe(1234.56m);
    }

    /// <summary>Verifies insert when culture is swedish and grouping uses no break space accepts and commits correctly.</summary>
    [Fact]
    public void Insert_WhenCultureIsSwedishAndGroupingUsesNoBreakSpace_AcceptsAndCommitsCorrectly()
    {
        // Arrange - sv-SE groups with U+00A0 (no-break space).
        var format = new CultureInfo("sv-SE").NumberFormat;
        var buffer = Create(format);
        var grouped = $"1{format.NumberGroupSeparator}234";

        // Act
        var accepted = buffer.Insert(grouped);
        var committed = buffer.TryCommit(out var value);

        // Assert
        format.NumberGroupSeparator.ShouldBe(" ");
        accepted.ShouldBeTrue();
        committed.ShouldBeTrue();
        value.ShouldBe(1234m);
    }

    /// <summary>Verifies insert when culture is french and grouping uses narrow no break space accepts and commits correctly.</summary>
    [Fact]
    public void Insert_WhenCultureIsFrenchAndGroupingUsesNarrowNoBreakSpace_AcceptsAndCommitsCorrectly()
    {
        // Arrange - fr-FR groups with U+202F (narrow no-break space), a distinct codepoint from
        // sv-SE's U+00A0.
        var format = new CultureInfo("fr-FR").NumberFormat;
        var buffer = Create(format);
        var grouped = $"1{format.NumberGroupSeparator}234";

        // Act
        var accepted = buffer.Insert(grouped);
        var committed = buffer.TryCommit(out var value);

        // Assert
        format.NumberGroupSeparator.ShouldBe(" ");
        accepted.ShouldBeTrue();
        committed.ShouldBeTrue();
        value.ShouldBe(1234m);
    }

    #endregion

    #region IsEditing operations

    /// <summary>Verifies backspace when caret follows text removes preceding grapheme.</summary>
    [Fact]
    public void Backspace_WhenCaretFollowsText_RemovesPrecedingGrapheme()
    {
        // Arrange
        var buffer = Create();
        _ = buffer.Insert("12");

        // Act
        var changed = buffer.Backspace();

        // Assert
        changed.ShouldBeTrue();
        buffer.Text.ShouldBe("1");
    }

    /// <summary>Verifies backspace when buffer is empty returns false.</summary>
    [Fact]
    public void Backspace_WhenBufferIsEmpty_ReturnsFalse()
    {
        // Arrange
        var buffer = Create();

        // Act and assert
        buffer.Backspace().ShouldBeFalse();
    }

    /// <summary>Verifies delete when caret precedes text removes following grapheme.</summary>
    [Fact]
    public void Delete_WhenCaretPrecedesText_RemovesFollowingGrapheme()
    {
        // Arrange
        var buffer = Create();
        buffer.Load("12");
        buffer.SetCaret(0);

        // Act
        var changed = buffer.Delete();

        // Assert
        changed.ShouldBeTrue();
        buffer.Text.ShouldBe("2");
    }

    /// <summary>Verifies move previous when caret is at start returns false.</summary>
    [Fact]
    public void MovePrevious_WhenCaretIsAtStart_ReturnsFalse()
    {
        // Arrange
        var buffer = Create();
        buffer.Load("12");
        buffer.SetCaret(0);

        // Act and assert
        buffer.MovePrevious(extend: false).ShouldBeFalse();
    }

    /// <summary>Verifies move next then insert when caret moved left inserts at the new caret position.</summary>
    [Fact]
    public void MoveNextThenInsert_WhenCaretMovedLeft_InsertsAtTheNewCaretPosition()
    {
        // Arrange
        var buffer = Create();
        buffer.Load("12");
        buffer.SetCaret(0);

        // Act
        _ = buffer.MoveNext(extend: false);
        _ = buffer.Insert("9");

        // Assert
        buffer.Text.ShouldBe("192");
    }

    /// <summary>Verifies set caret when index exceeds length clamps.</summary>
    [Fact]
    public void SetCaret_WhenIndexExceedsLength_Clamps()
    {
        // Arrange
        var buffer = Create();
        buffer.Load("12");

        // Act
        buffer.SetCaret(99);
        var changed = buffer.Insert("3");

        // Assert
        buffer.Text.ShouldBe("123");
        changed.ShouldBeTrue();
    }

    /// <summary>Verifies index at column when column falls within a grapheme returns that graphemes offset.</summary>
    [Fact]
    public void IndexAtColumn_WhenColumnFallsWithinAGrapheme_ReturnsThatGraphemesOffset()
    {
        // Arrange
        var buffer = Create();
        buffer.Load("123");

        // Act and assert
        buffer.IndexAtColumn(0, Ambiguous.Narrow).ShouldBe(0);
        buffer.IndexAtColumn(1, Ambiguous.Narrow).ShouldBe(1);
        buffer.IndexAtColumn(99, Ambiguous.Narrow).ShouldBe(3);
    }

    /// <summary>Verifies load when called places caret at end.</summary>
    [Fact]
    public void Load_WhenCalled_PlacesCaretAtEnd()
    {
        // Arrange
        var buffer = Create();

        // Act
        buffer.Load("42");

        // Assert
        buffer.Selection.Caret.ShouldBe(2);
    }

    #endregion
}
