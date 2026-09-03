// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies the value-type contracts and the pattern-walking edge cases shared by every
/// segmented temporal field: <see cref="SegmentDescriptor"/> and <see cref="PatternSegment"/>
/// validation and equality, <see cref="TemporalPatternSegmenter"/>'s handling of percent-prefixed,
/// backslash-escaped, and quoted runs, and <see cref="TemporalValueState{T}"/>'s bound ordering.</summary>
public sealed class TemporalSegmentTests
{
    private static readonly IReadOnlyDictionary<char, TemporalSegmentKind> _dateKinds =
        new Dictionary<char, TemporalSegmentKind>
        {
            ['M'] = TemporalSegmentKind.Month,
            ['d'] = TemporalSegmentKind.Day,
            ['y'] = TemporalSegmentKind.Year
        };

    private static readonly IReadOnlyDictionary<char, TemporalSegmentKind> _timeKinds =
        new Dictionary<char, TemporalSegmentKind>
        {
            ['H'] = TemporalSegmentKind.Hour,
            ['m'] = TemporalSegmentKind.Minute
        };

    #region SegmentDescriptor

    /// <summary>Verifies an editable descriptor rejects every digit capacity other than 0, 2, or 4.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(-2)]
    public void SegmentDescriptor_WhenDigitCapacityIsUnsupported_ThrowsArgumentOutOfRange(int capacity)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new SegmentDescriptor("12", TemporalSegmentKind.Month, capacity, 12));

        exception.ParamName.ShouldBe("digitCapacity");
        exception.ActualValue.ShouldBe(capacity);
    }

    /// <summary>Verifies a literal descriptor is neither editable nor digit-typable and never
    /// auto-commits a first digit.</summary>
    [Fact]
    public void SegmentDescriptor_WhenLiteral_IsInertForEditing()
    {
        var literal = new SegmentDescriptor("/");

        literal.Kind.ShouldBeNull();
        literal.IsEditable.ShouldBeFalse();
        literal.IsDigitTypable.ShouldBeFalse();
        literal.DigitCapacity.ShouldBe(0);
        literal.FirstDigitOverflowThreshold.ShouldBe(-1);
        _ = Should.Throw<ArgumentNullException>(() => new SegmentDescriptor(null!));
        _ = Should.Throw<ArgumentNullException>(() => new SegmentDescriptor(null!, TemporalSegmentKind.Day, 2, 31));
    }

    /// <summary>Verifies the first-digit auto-commit threshold derives from the segment's capacity
    /// and maximum: a two-digit segment commits any first digit above <c>MaxValue / 10</c>, a
    /// four-digit year never commits on its first digit, and a designator never accepts digits.</summary>
    [Theory]
    [InlineData(2, 12, 1)]
    [InlineData(2, 31, 3)]
    [InlineData(2, 23, 2)]
    [InlineData(2, 59, 5)]
    [InlineData(4, 9999, 9)]
    [InlineData(0, 0, -1)]
    public void SegmentDescriptor_FirstDigitOverflowThreshold_DerivesFromCapacityAndMaximum(
        int capacity,
        int maxValue,
        int expected)
    {
        var descriptor = new SegmentDescriptor("x", TemporalSegmentKind.Hour, capacity, maxValue);

        descriptor.FirstDigitOverflowThreshold.ShouldBe(expected);
        descriptor.IsEditable.ShouldBeTrue();
        descriptor.IsDigitTypable.ShouldBe(capacity > 0);
    }

    /// <summary>Verifies descriptor equality compares text, kind, capacity, and maximum together.</summary>
    [Fact]
    public void SegmentDescriptor_Equality_ComparesEveryMember()
    {
        var left = new SegmentDescriptor("03", TemporalSegmentKind.Month, 2, 12);
        var same = new SegmentDescriptor("03", TemporalSegmentKind.Month, 2, 12);
        var otherText = new SegmentDescriptor("04", TemporalSegmentKind.Month, 2, 12);
        var otherKind = new SegmentDescriptor("03", TemporalSegmentKind.Day, 2, 12);
        var otherMaximum = new SegmentDescriptor("03", TemporalSegmentKind.Month, 2, 31);

        left.Equals(same).ShouldBeTrue();
        (left == same).ShouldBeTrue();
        (left != same).ShouldBeFalse();
        left.GetHashCode().ShouldBe(same.GetHashCode());
        left.Equals((object) same).ShouldBeTrue();
        left.Equals("03").ShouldBeFalse();
        (left != otherText).ShouldBeTrue();
        (left == otherKind).ShouldBeFalse();
        left.Equals(otherMaximum).ShouldBeFalse();
    }

    #endregion

    #region PatternSegment

    /// <summary>Verifies an editable run needs a positive length and a literal run needs text.</summary>
    [Fact]
    public void PatternSegment_WhenConstructedWithInvalidArguments_Throws()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new PatternSegment(TemporalSegmentKind.Day, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new PatternSegment(TemporalSegmentKind.Day, -1));
        _ = Should.Throw<ArgumentNullException>(() => new PatternSegment(null!));
    }

    /// <summary>Verifies pattern-segment equality compares literal text, kind, and run length.</summary>
    [Fact]
    public void PatternSegment_Equality_ComparesEveryMember()
    {
        var run = new PatternSegment(TemporalSegmentKind.Year, 4);
        var sameRun = new PatternSegment(TemporalSegmentKind.Year, 4);
        var shorterRun = new PatternSegment(TemporalSegmentKind.Year, 2);
        var literal = new PatternSegment("/");
        var sameLiteral = new PatternSegment("/");

        (run == sameRun).ShouldBeTrue();
        run.GetHashCode().ShouldBe(sameRun.GetHashCode());
        (run != shorterRun).ShouldBeTrue();
        run.Equals((object) sameRun).ShouldBeTrue();
        run.Equals((object) literal).ShouldBeFalse();
        run.Equals(4).ShouldBeFalse();
        (literal == sameLiteral).ShouldBeTrue();
        literal.Equals(run).ShouldBeFalse();
        literal.Kind.ShouldBeNull();
        literal.RunLength.ShouldBe(0);
        run.LiteralText.ShouldBe(string.Empty);
    }

    #endregion

    #region TemporalPatternSegmenter.ParseTokens

    /// <summary>Verifies a percent-prefixed single-letter token parses as a one-letter editable run
    /// instead of being swallowed as a literal percent sign.</summary>
    [Fact]
    public void ParseTokens_WhenTokensArePercentPrefixed_ProducesSingleLetterRuns()
    {
        var tokens = TemporalPatternSegmenter.ParseTokens("%M/%d/yyyy", _dateKinds, CultureInfo.InvariantCulture);

        tokens.ShouldBe(
        [
            new PatternSegment(TemporalSegmentKind.Month, 1),
            new PatternSegment("/"),
            new PatternSegment(TemporalSegmentKind.Day, 1),
            new PatternSegment("/"),
            new PatternSegment(TemporalSegmentKind.Year, 4)
        ]);
    }

    /// <summary>Verifies a trailing percent sign with nothing after it ends the walk without
    /// producing a stray literal.</summary>
    [Fact]
    public void ParseTokens_WhenPercentIsTrailing_DropsIt()
    {
        var tokens = TemporalPatternSegmenter.ParseTokens("dd%", _dateKinds, CultureInfo.InvariantCulture);

        tokens.ShouldBe([new PatternSegment(TemporalSegmentKind.Day, 2)]);
    }

    /// <summary>Verifies a backslash-escaped separator stays a literal slash while an unquoted
    /// slash resolves to the culture's date separator.</summary>
    [Fact]
    public void ParseTokens_WhenSeparatorIsEscaped_KeepsLiteralInsteadOfCultureSeparator()
    {
        var german = new CultureInfo("de-DE");

        var escaped = TemporalPatternSegmenter.ParseTokens(@"dd\/MM", _dateKinds, german);
        var unquoted = TemporalPatternSegmenter.ParseTokens("dd/MM", _dateKinds, german);
        var time = TemporalPatternSegmenter.ParseTokens("HH:mm", _timeKinds, new CultureInfo("fi-FI"));

        escaped[1].ShouldBe(new PatternSegment("/"));
        unquoted[1].ShouldBe(new PatternSegment("."));
        time[1].ShouldBe(new PatternSegment("."));
    }

    /// <summary>Verifies a backslash inside a quoted literal escapes the next character, so an
    /// embedded quote survives, and adjacent literal runs merge into one.</summary>
    [Fact]
    public void ParseTokens_WhenQuotedLiteralEscapesQuote_KeepsQuoteAndMergesAdjacentLiterals()
    {
        var tokens = TemporalPatternSegmenter.ParseTokens(@"'It\'s' x dd", _dateKinds, CultureInfo.InvariantCulture);

        tokens.ShouldBe(
        [
            new PatternSegment("It's x "),
            new PatternSegment(TemporalSegmentKind.Day, 2)
        ]);
    }

    /// <summary>Verifies an empty quoted literal produces no segment and an unterminated quote
    /// consumes the rest of the pattern as literal text.</summary>
    [Fact]
    public void ParseTokens_WhenQuotedLiteralIsEmptyOrUnterminated_HandlesBothWithoutThrowing()
    {
        var empty = TemporalPatternSegmenter.ParseTokens("''dd", _dateKinds, CultureInfo.InvariantCulture);
        var unterminated = TemporalPatternSegmenter.ParseTokens("dd'rest", _dateKinds, CultureInfo.InvariantCulture);

        empty.ShouldBe([new PatternSegment(TemporalSegmentKind.Day, 2)]);
        unterminated.ShouldBe(
        [
            new PatternSegment(TemporalSegmentKind.Day, 2),
            new PatternSegment("rest")
        ]);
    }

    /// <summary>Verifies every null argument is rejected before parsing begins.</summary>
    [Fact]
    public void ParseTokens_WhenArgumentIsNull_ThrowsArgumentNullException()
    {
        _ = Should.Throw<ArgumentNullException>(
            () => TemporalPatternSegmenter.ParseTokens(null!, _dateKinds, CultureInfo.InvariantCulture));
        _ = Should.Throw<ArgumentNullException>(
            () => TemporalPatternSegmenter.ParseTokens("dd", null!, CultureInfo.InvariantCulture));
        _ = Should.Throw<ArgumentNullException>(
            () => TemporalPatternSegmenter.ParseTokens("dd", _dateKinds, null!));
    }

    #endregion

    #region TemporalPatternSegmenter.FormatSegments

    /// <summary>Verifies percent-prefixed runs format unpadded, one segment per token, with the
    /// literal separators kept in place.</summary>
    [Fact]
    public void FormatSegments_WhenTokensArePercentPrefixed_FormatsUnpaddedSegments()
    {
        const string pattern = "%M/%d/yyyy";
        var date = new DateOnly(2026, 3, 5);
        var tokens = TemporalPatternSegmenter.ParseTokens(pattern, _dateKinds, CultureInfo.InvariantCulture);

        var text = TemporalPatternSegmenter.FormatSegments(
            pattern,
            tokens,
            _dateKinds,
            format => date.ToString(format, CultureInfo.InvariantCulture));

        text.ShouldBe(["3", "/", "5", "/", "2026"]);
    }

    /// <summary>Verifies a lone percent-prefixed token formats as its unpadded value.</summary>
    [Fact]
    public void FormatSegments_WhenPatternIsOnePercentPrefixedToken_FormatsIt()
    {
        var date = new DateOnly(2026, 3, 5);
        var tokens = TemporalPatternSegmenter.ParseTokens("%d", _dateKinds, CultureInfo.InvariantCulture);

        var text = TemporalPatternSegmenter.FormatSegments(
            "%d",
            tokens,
            _dateKinds,
            format => date.ToString(format, CultureInfo.InvariantCulture));

        text.ShouldBe(["5"]);
    }

    /// <summary>Verifies escaped separators and escaped quotes inside quoted literals survive the
    /// marked-pattern rewrite, so the rendered literal matches the parsed literal exactly. The
    /// quoted-escape case formats through DateTime: DateOnly and TimeOnly's own format validators
    /// do not recognize a backslash inside quotes, so only DateTimeInput can render that shape.</summary>
    [Fact]
    public void FormatSegments_WhenPatternUsesEscapes_PreservesLiteralText()
    {
        var date = new DateOnly(2026, 3, 5);
        var dateTime = new DateTime(2026, 3, 5, 14, 30, 0);
        var german = new CultureInfo("de-DE");
        const string escaped = @"dd\/MM";
        const string quoted = @"'It\'s' dd";

        var escapedText = TemporalPatternSegmenter.FormatSegments(
            escaped,
            TemporalPatternSegmenter.ParseTokens(escaped, _dateKinds, german),
            _dateKinds,
            format => date.ToString(format, german));
        var quotedText = TemporalPatternSegmenter.FormatSegments(
            quoted,
            TemporalPatternSegmenter.ParseTokens(quoted, _dateKinds, CultureInfo.InvariantCulture),
            _dateKinds,
            format => dateTime.ToString(format, CultureInfo.InvariantCulture));

        escapedText.ShouldBe(["05", "/", "03"]);
        quotedText.ShouldBe(["It's ", "05"]);
    }

    /// <summary>Verifies every null argument is rejected before formatting begins.</summary>
    [Fact]
    public void FormatSegments_WhenArgumentIsNull_ThrowsArgumentNullException()
    {
        var tokens = TemporalPatternSegmenter.ParseTokens("dd", _dateKinds, CultureInfo.InvariantCulture);

        _ = Should.Throw<ArgumentNullException>(
            () => TemporalPatternSegmenter.FormatSegments(null!, tokens, _dateKinds, static f => f));
        _ = Should.Throw<ArgumentNullException>(
            () => TemporalPatternSegmenter.FormatSegments("dd", null!, _dateKinds, static f => f));
        _ = Should.Throw<ArgumentNullException>(
            () => TemporalPatternSegmenter.FormatSegments("dd", tokens, null!, static f => f));
        _ = Should.Throw<ArgumentNullException>(
            () => TemporalPatternSegmenter.FormatSegments("dd", tokens, _dateKinds, null!));
    }

    #endregion

    #region TemporalSegmentClassification.SelectAmPm

    /// <summary>Verifies the "a"/"p" shortcut classifier maps both cases of each letter and
    /// rejects every other character.</summary>
    [Theory]
    [InlineData('a', true, false)]
    [InlineData('A', true, false)]
    [InlineData('p', true, true)]
    [InlineData('P', true, true)]
    [InlineData('m', false, false)]
    [InlineData('5', false, false)]
    public void TryGetAmPmSelection_WhenGivenCharacter_ClassifiesShortcut(char character, bool expected, bool expectedPm)
    {
        TemporalSegmentClassification.TryGetAmPmSelection(new Rune(character), out var selectPm).ShouldBe(expected);

        if (expected)
        {
            selectPm.ShouldBe(expectedPm);
        }
    }

    /// <summary>Verifies selecting the half of the day the value is already in activates the
    /// designator segment without incrementing it, while selecting the other half increments once;
    /// both count as an applied selection, because the highlight moved either way.</summary>
    [Theory]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public void SelectAmPm_WhenHalfOfDayIsRequested_IncrementsOnlyWhenItDiffers(
        bool isPm,
        bool selectPm,
        bool expectedIncrement)
    {
        var increments = 0;
        SegmentDescriptor[] layout =
        [
            new SegmentDescriptor("09", TemporalSegmentKind.Hour, 2, 12),
            new SegmentDescriptor(":"),
            new SegmentDescriptor("30", TemporalSegmentKind.Minute, 2, 59),
            new SegmentDescriptor(" "),
            new SegmentDescriptor("AM", TemporalSegmentKind.AmPmDesignator, 0, 0)
        ];
        var segments = new SegmentFieldBehavior(
            () => layout,
            static (_, _) => false,
            (_, _) =>
            {
                increments++;
                return true;
            },
            static _ => false,
            static () => { });

        var applied = TemporalSegmentClassification.SelectAmPm(
            () => layout,
            static () => true,
            () => isPm,
            segments,
            selectPm);

        applied.ShouldBeTrue();
        increments.ShouldBe(expectedIncrement ? 1 : 0);
        segments.ActiveSegment.ShouldBe(2);
    }

    /// <summary>Verifies the designator text falls back to the invariant AM/PM only when the
    /// culture produced nothing, and otherwise keeps the culture's own text unchanged.</summary>
    [Theory]
    [InlineData("", false, "AM")]
    [InlineData("", true, "PM")]
    [InlineData("午後", true, "午後")]
    [InlineData("a.m.", false, "a.m.")]
    public void ResolveDesignatorText_WhenFormattedTextIsEmpty_FallsBackToInvariant(string formatted, bool isPm, string expected) =>
        TemporalSegmentClassification.ResolveDesignatorText(formatted, isPm).ShouldBe(expected);

    /// <summary>Verifies selection is refused without a value or without a designator segment.</summary>
    [Fact]
    public void SelectAmPm_WhenValueOrDesignatorIsMissing_ReturnsFalseWithoutActivating()
    {
        SegmentDescriptor[] designatorLayout =
        [
            new SegmentDescriptor("09", TemporalSegmentKind.Hour, 2, 12),
            new SegmentDescriptor("AM", TemporalSegmentKind.AmPmDesignator, 0, 0)
        ];
        SegmentDescriptor[] plainLayout = [new SegmentDescriptor("09", TemporalSegmentKind.Hour, 2, 23)];
        var withDesignator = new SegmentFieldBehavior(
            () => designatorLayout,
            static (_, _) => false,
            static (_, _) => true,
            static _ => false,
            static () => { });
        var withoutDesignator = new SegmentFieldBehavior(
            () => plainLayout,
            static (_, _) => false,
            static (_, _) => true,
            static _ => false,
            static () => { });

        TemporalSegmentClassification.SelectAmPm(
            () => designatorLayout, static () => false, static () => false, withDesignator, selectPm: true).ShouldBeFalse();
        TemporalSegmentClassification.SelectAmPm(
            () => plainLayout, static () => true, static () => false, withoutDesignator, selectPm: true).ShouldBeFalse();
        withDesignator.ActiveSegment.ShouldBe(0);
        _ = Should.Throw<ArgumentNullException>(() => TemporalSegmentClassification.SelectAmPm(
            null!, static () => true, static () => false, withDesignator, selectPm: true));
        _ = Should.Throw<ArgumentNullException>(() => TemporalSegmentClassification.SelectAmPm(
            () => designatorLayout, null!, static () => false, withDesignator, selectPm: true));
        _ = Should.Throw<ArgumentNullException>(() => TemporalSegmentClassification.SelectAmPm(
            () => designatorLayout, static () => true, null!, withDesignator, selectPm: true));
        _ = Should.Throw<ArgumentNullException>(() => TemporalSegmentClassification.SelectAmPm(
            () => designatorLayout, static () => true, static () => false, null!, selectPm: true));
    }

    #endregion

    #region TemporalValueState

    /// <summary>Verifies the state rejects a lower bound above the upper bound before it captures
    /// any callback.</summary>
    [Fact]
    public void TemporalValueState_WhenMinimumExceedsMaximum_ThrowsArgumentException()
    {
        var owner = new ProbeControl();

        var exception = Should.Throw<ArgumentException>(() => new TemporalValueState<DateOnly>(
            new DateOnly(2026, 3, 16),
            new DateOnly(2026, 3, 15),
            owner,
            static () => { },
            static (_, _) => { },
            static () => new DateOnly(2026, 3, 15),
            static (ref _, _, _) => { }));

        exception.ParamName.ShouldBe("minimum");
    }

    #endregion
}
