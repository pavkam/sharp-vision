// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies the shared AM/PM designator toggle, pointer-driven segment activation, and
/// digit/AM-PM keystroke classification composed into TimeInput and DateTimeInput - directly
/// against <see cref="TemporalSegmentClassification"/>, mirroring <see
/// cref="SegmentFieldBehaviorTests"/>'s direct-construction style. Both concrete controls now call
/// this shared type unchanged, so a regression here is a regression for both.</summary>
public sealed class TemporalSegmentClassificationTests
{
    // A three-segment "hh:mm tt" layout mirroring a real 12-hour TimeInput/DateTimeInput
    // BuildSegments result: an editable Hour, a literal separator, an editable Minute, a literal
    // space, and an editable zero-digit-capacity AmPmDesignator.
    private static IReadOnlyList<SegmentDescriptor> SegmentsWithAmPm(int hour, bool isPm) =>
    [
        new SegmentDescriptor(hour.ToString("D2", CultureInfo.InvariantCulture), TemporalSegmentKind.Hour, 2, 12),
        new SegmentDescriptor(":"),
        new SegmentDescriptor("00", TemporalSegmentKind.Minute, 2, 59),
        new SegmentDescriptor(" "),
        new SegmentDescriptor(isPm ? "PM" : "AM", TemporalSegmentKind.AmPmDesignator, 0, 0)
    ];

    private static IReadOnlyList<SegmentDescriptor> SegmentsWithoutAmPm() =>
    [
        new SegmentDescriptor("14", TemporalSegmentKind.Hour, 2, 23),
        new SegmentDescriptor(":"),
        new SegmentDescriptor("30", TemporalSegmentKind.Minute, 2, 59)
    ];

    #region FindEditableIndex

    /// <summary>Verifies the constructor-free lookup rejects a null provider.</summary>
    [Fact]
    public void FindEditableIndex_WhenProviderIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() =>
            TemporalSegmentClassification.FindEditableIndex(null!, TemporalSegmentKind.AmPmDesignator));

    /// <summary>Verifies the editable-segment index counts only editable segments, skipping
    /// literals, and lands on the AmPmDesignator's own position (index 2: Hour, Minute, then
    /// AmPmDesignator).</summary>
    [Fact]
    public void FindEditableIndex_WhenLayoutHasAmPmDesignator_ReturnsItsEditableIndex()
    {
        var index = TemporalSegmentClassification.FindEditableIndex(
            () => SegmentsWithAmPm(9, isPm: false),
            TemporalSegmentKind.AmPmDesignator);

        index.ShouldBe(2);
    }

    /// <summary>Verifies a kind absent from the layout reports -1.</summary>
    [Fact]
    public void FindEditableIndex_WhenLayoutHasNoMatchingKind_ReturnsNegativeOne()
    {
        var index = TemporalSegmentClassification.FindEditableIndex(
            SegmentsWithoutAmPm,
            TemporalSegmentKind.AmPmDesignator);

        index.ShouldBe(-1);
    }

    #endregion

    #region HasAmPmDesignator

    /// <summary>Verifies a 12-hour layout with a designator segment reports true.</summary>
    [Fact]
    public void HasAmPmDesignator_WhenLayoutIncludesDesignator_ReturnsTrue() =>
        TemporalSegmentClassification.HasAmPmDesignator(() => SegmentsWithAmPm(9, isPm: false)).ShouldBeTrue();

    /// <summary>Verifies a 24-hour layout without a designator segment reports false.</summary>
    [Fact]
    public void HasAmPmDesignator_WhenLayoutExcludesDesignator_ReturnsFalse() =>
        TemporalSegmentClassification.HasAmPmDesignator(SegmentsWithoutAmPm).ShouldBeFalse();

    #endregion

    #region ToggleAmPm

    /// <summary>Verifies the constructor-free toggle rejects every required argument.</summary>
    [Fact]
    public void ToggleAmPm_WhenRequiredArgumentIsNull_ThrowsArgumentNullException()
    {
        var segments = Behavior(SegmentsWithAmPm(9, isPm: false), out _);

        _ = Should.Throw<ArgumentNullException>(() =>
            TemporalSegmentClassification.ToggleAmPm(null!, () => true, segments));
        _ = Should.Throw<ArgumentNullException>(() =>
            TemporalSegmentClassification.ToggleAmPm(() => SegmentsWithAmPm(9, isPm: false), null!, segments));
        _ = Should.Throw<ArgumentNullException>(() =>
            TemporalSegmentClassification.ToggleAmPm(() => SegmentsWithAmPm(9, isPm: false), () => true, null!));
    }

    /// <summary>Verifies a control with no current value never toggles, regardless of layout.</summary>
    [Fact]
    public void ToggleAmPm_WhenControlHasNoValue_ReturnsFalseWithoutIncrementing()
    {
        var incremented = false;
        var segments = Behavior(
            SegmentsWithAmPm(9, isPm: false),
            out _,
            onIncrement: () => incremented = true);

        var changed = TemporalSegmentClassification.ToggleAmPm(
            () => SegmentsWithAmPm(9, isPm: false),
            () => false,
            segments);

        changed.ShouldBeFalse();
        incremented.ShouldBeFalse();
    }

    /// <summary>Verifies a 24-hour layout with no designator segment never toggles.</summary>
    [Fact]
    public void ToggleAmPm_WhenLayoutHasNoDesignator_ReturnsFalse()
    {
        var segments = Behavior(SegmentsWithoutAmPm(), out _);

        var changed = TemporalSegmentClassification.ToggleAmPm(SegmentsWithoutAmPm, () => true, segments);

        changed.ShouldBeFalse();
    }

    /// <summary>Verifies a 12-hour layout with a value activates the designator segment and
    /// advances it one step, the same sequence TimeInput/DateTimeInput's own ToggleAmPm bodies
    /// used to perform inline before the extraction.</summary>
    [Fact]
    public void ToggleAmPm_WhenLayoutHasDesignatorAndControlHasValue_ActivatesAndIncrementsDesignator()
    {
        var segments = Behavior(SegmentsWithAmPm(9, isPm: false), out var activeSegmentBefore);

        var changed = TemporalSegmentClassification.ToggleAmPm(
            () => SegmentsWithAmPm(9, isPm: false),
            () => true,
            segments);

        changed.ShouldBeTrue();
        segments.ActiveSegment.ShouldBe(2, "the AmPmDesignator's own editable-segment index");
        activeSegmentBefore.ShouldBe(0, "the freshly constructed behavior starts on the first segment");
    }

    #endregion

    #region IsDigit / IsAmPmToggle / To24Hour

    /// <summary>Pins every ASCII decimal digit as a digit and every neighboring codepoint as not.</summary>
    [Theory]
    [InlineData('0', true)]
    [InlineData('5', true)]
    [InlineData('9', true)]
    [InlineData('/', false)]
    [InlineData(':', false)]
    [InlineData('a', false)]
    public void IsDigit_WhenGivenCharacter_ClassifiesAsciiDigitsOnly(char character, bool expected) =>
        TemporalSegmentClassification.IsDigit(new Rune(character)).ShouldBe(expected);

    /// <summary>Pins the fixed "a"/"p" (either case) AM/PM toggle shortcut, independent of any
    /// culture's own localized designator text.</summary>
    [Theory]
    [InlineData('a', true)]
    [InlineData('A', true)]
    [InlineData('p', true)]
    [InlineData('P', true)]
    [InlineData('m', false)]
    [InlineData('1', false)]
    public void IsAmPmToggle_WhenGivenCharacter_ClassifiesFixedAmPmShortcutOnly(char character, bool expected) =>
        TemporalSegmentClassification.IsAmPmToggle(new Rune(character)).ShouldBe(expected);

    /// <summary>Pins the 1-12/AM-PM to 0-23 conversion table, including both midnight and noon's
    /// non-linear 12-hour wraparound.</summary>
    [Theory]
    [InlineData(12, false, 0)]
    [InlineData(1, false, 1)]
    [InlineData(11, false, 11)]
    [InlineData(12, true, 12)]
    [InlineData(1, true, 13)]
    [InlineData(11, true, 23)]
    public void To24Hour_WhenGivenHour12AndPmFlag_ConvertsToExpected24HourValue(
        int hour12,
        bool isPm,
        int expected24Hour) =>
        TemporalSegmentClassification.To24Hour(hour12, isPm).ShouldBe(expected24Hour);

    #endregion

    #region HandlePointer

    /// <summary>Verifies the stateful segment pointer handler rejects every required argument.</summary>
    [Fact]
    public void HandlePointer_WhenRequiredArgumentIsNull_ThrowsArgumentNullException()
    {
        var segments = Behavior(SegmentsWithoutAmPm(), out _);
        var press = new PointerEventArgs(Press(new Point(0, 0)));

        _ = Should.Throw<ArgumentNullException>(() =>
            segments.HandlePointer(
                null!, new Rect(0, 0, 5, 1), Ambiguous.Narrow, isFocused: false, () => true, () => true));
        _ = Should.Throw<ArgumentNullException>(() =>
            segments.HandlePointer(
                press, new Rect(0, 0, 5, 1), Ambiguous.Narrow, isFocused: false, null!, () => true));
        _ = Should.Throw<ArgumentNullException>(() =>
            segments.HandlePointer(
                press, new Rect(0, 0, 5, 1), Ambiguous.Narrow, isFocused: false, () => true, null!));
    }

    /// <summary>Verifies a primary press inside the segment box activates the segment under the
    /// press column and requests focus when not already focused.</summary>
    [Fact]
    public void HandlePointer_WhenPrimaryPressLandsInsideBox_ActivatesSegmentAndRequestsFocus()
    {
        var segments = Behavior(SegmentsWithoutAmPm(), out _);
        var requested = false;
        var eventArgs = new PointerEventArgs(Press(new Point(3, 0)));

        segments.HandlePointer(
            eventArgs,
            new Rect(0, 0, 5, 1),
            Ambiguous.Narrow,
            isFocused: false,
            () =>
            {
                requested = true;
                return true;
            },
            () => true);

        eventArgs.IsHandled.ShouldBeTrue();
        requested.ShouldBeTrue();
        segments.ActiveSegment.ShouldBe(1, "column 3 lands on the Minute segment (Hour occupies columns 0-1)");
    }

    /// <summary>Verifies an already-focused control does not redundantly request focus again.</summary>
    [Fact]
    public void HandlePointer_WhenAlreadyFocused_DoesNotRequestFocusAgain()
    {
        var segments = Behavior(SegmentsWithoutAmPm(), out _);
        var requested = false;
        var eventArgs = new PointerEventArgs(Press(new Point(0, 0)));

        segments.HandlePointer(
            eventArgs,
            new Rect(0, 0, 5, 1),
            Ambiguous.Narrow,
            isFocused: true,
            () =>
            {
                requested = true;
                return true;
            },
            () => true);

        requested.ShouldBeFalse();
    }

    /// <summary>Verifies a press outside the segment box - such as over an affix or the drop-down
    /// indicator's own reserved columns - neither activates a segment nor marks the event
    /// handled.</summary>
    [Fact]
    public void HandlePointer_WhenPressLandsOutsideBox_DoesNothing()
    {
        var segments = Behavior(SegmentsWithoutAmPm(), out var activeBefore);
        var eventArgs = new PointerEventArgs(Press(new Point(9, 0)));

        segments.HandlePointer(
            eventArgs,
            new Rect(0, 0, 5, 1),
            Ambiguous.Narrow,
            isFocused: false,
            () => true,
            () => true);

        eventArgs.IsHandled.ShouldBeFalse();
        segments.ActiveSegment.ShouldBe(activeBefore);
    }

    /// <summary>Verifies a non-primary button press is ignored.</summary>
    [Fact]
    public void HandlePointer_WhenPressIsNotPrimaryButton_DoesNothing()
    {
        var segments = Behavior(SegmentsWithoutAmPm(), out _);
        var pointer = new Pointer(
            new Point(3, 0),
            pixels: null,
            Buttons.Secondary,
            PointerAction.Press,
            wheelX: 0,
            wheelY: 0,
            Modifiers.None,
            isMotion: false,
            isCellPositionInferred: false);
        var eventArgs = new PointerEventArgs(pointer);

        segments.HandlePointer(
            eventArgs,
            new Rect(0, 0, 5, 1),
            Ambiguous.Narrow,
            isFocused: false,
            () => true,
            () => true);

        eventArgs.IsHandled.ShouldBeFalse();
    }

    /// <summary>Verifies a pointer action other than Press - a move, for instance - is ignored.</summary>
    [Fact]
    public void HandlePointer_WhenActionIsNotPress_DoesNothing()
    {
        var segments = Behavior(SegmentsWithoutAmPm(), out _);
        var pointer = new Pointer(
            new Point(3, 0),
            pixels: null,
            Buttons.None,
            PointerAction.Move,
            wheelX: 0,
            wheelY: 0,
            Modifiers.None,
            isMotion: true,
            isCellPositionInferred: false);
        var eventArgs = new PointerEventArgs(pointer, clickCount: 0);

        segments.HandlePointer(
            eventArgs,
            new Rect(0, 0, 5, 1),
            Ambiguous.Narrow,
            isFocused: false,
            () => true,
            () => true);

        eventArgs.IsHandled.ShouldBeFalse();
    }

    #endregion

    #region Helpers

    /// <summary>Constructs a plain <see cref="SegmentFieldBehavior"/> over a fixed layout, wired
    /// the same way <see cref="SegmentFieldBehaviorTests"/> constructs one directly, for use as
    /// the shared classification methods' own <c>segments</c> argument.</summary>
    private static SegmentFieldBehavior Behavior(
        IReadOnlyList<SegmentDescriptor> layout,
        out int activeSegmentAtConstruction,
        Action? onIncrement = null)
    {
        var behavior = new SegmentFieldBehavior(
            () => layout,
            static (_, _) => false,
            (_, _) =>
            {
                onIncrement?.Invoke();
                return true;
            },
            static _ => false,
            static () => { });
        activeSegmentAtConstruction = behavior.ActiveSegment;
        return behavior;
    }

    private static Pointer Press(Point cells) => new(
        cells,
        pixels: null,
        Buttons.Primary,
        PointerAction.Press,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: false,
        isCellPositionInferred: false);

    #endregion
}
