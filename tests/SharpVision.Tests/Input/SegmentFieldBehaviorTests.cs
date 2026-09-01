// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies the shared active-segment navigation, digit-entry buffering, and pointer
/// hit-testing state machine composed into every segmented temporal field control (DateInput,
/// TimeInput, DateTimeInput) - directly against <see cref="SegmentFieldBehavior"/>, mirroring
/// <see cref="PressBehaviorTests"/>'s direct-construction style. Every concrete control wraps this
/// engine unchanged through InputBase's EnableSegmentEditing, so a regression here is a regression
/// for the whole family.</summary>
public sealed class SegmentFieldBehaviorTests
{
    // A two-segment "HH:MM" layout: two editable two-digit segments (max 23 and 59) separated by
    // a literal colon, matching the shape every real segmented field builds.
    private int _hour;
    private int _minute;

    private static SegmentFieldBehavior Create(
        Func<IReadOnlyList<SegmentDescriptor>>? segmentsProvider = null,
        Func<TemporalSegmentKind, int, bool>? applyDigitValue = null,
        Func<TemporalSegmentKind, int, bool>? incrementSegment = null,
        Func<TemporalSegmentKind, bool>? clearSegment = null,
        Action? invalidate = null) =>
        new(
            segmentsProvider ?? (static () => []),
            applyDigitValue ?? (static (_, _) => false),
            incrementSegment ?? (static (_, _) => false),
            clearSegment ?? (static _ => false),
            invalidate ?? (static () => { }));

    private SegmentFieldBehavior CreateHourMinute(List<int>? invalidations = null)
    {
        _hour = 0;
        _minute = 0;

        return new SegmentFieldBehavior(
            Segments,
            (kind, value) =>
            {
                if (kind == TemporalSegmentKind.Hour)
                {
                    var clamped = Math.Clamp(value, 0, 23);
                    if (clamped == _hour)
                    {
                        return false;
                    }

                    _hour = clamped;
                    return true;
                }

                var clampedMinute = Math.Clamp(value, 0, 59);
                if (clampedMinute == _minute)
                {
                    return false;
                }

                _minute = clampedMinute;
                return true;
            },
            (kind, delta) =>
            {
                if (kind == TemporalSegmentKind.Hour)
                {
                    var clamped = Math.Clamp(_hour + delta, 0, 23);
                    if (clamped == _hour)
                    {
                        return false;
                    }

                    _hour = clamped;
                    return true;
                }

                var clampedMinute = Math.Clamp(_minute + delta, 0, 59);
                if (clampedMinute == _minute)
                {
                    return false;
                }

                _minute = clampedMinute;
                return true;
            },
            kind =>
            {
                if (kind == TemporalSegmentKind.Hour)
                {
                    if (_hour == 0)
                    {
                        return false;
                    }

                    _hour = 0;
                    return true;
                }

                if (_minute == 0)
                {
                    return false;
                }

                _minute = 0;
                return true;
            },
            () => invalidations?.Add(1));

        IReadOnlyList<SegmentDescriptor> Segments() =>
        [
            new SegmentDescriptor(_hour.ToString("D2", CultureInfo.InvariantCulture), TemporalSegmentKind.Hour, 2, 23),
            new SegmentDescriptor(":"),
            new SegmentDescriptor(_minute.ToString("D2", CultureInfo.InvariantCulture), TemporalSegmentKind.Minute, 2, 59)
        ];
    }

    #region Construction

    /// <summary>Verifies the constructor rejects every required dependency.</summary>
    [Fact]
    public void Constructor_WhenRequiredArgumentIsNull_ThrowsArgumentNullException()
    {
        static IReadOnlyList<SegmentDescriptor> Segments() => [];
        static bool ApplyDigit(TemporalSegmentKind kind, int value) => false;
        static bool Increment(TemporalSegmentKind kind, int delta) => false;
        static bool Clear(TemporalSegmentKind kind) => false;
        static void Invalidate() { }

        _ = Should.Throw<ArgumentNullException>(() =>
            new SegmentFieldBehavior(null!, ApplyDigit, Increment, Clear, Invalidate));
        _ = Should.Throw<ArgumentNullException>(() =>
            new SegmentFieldBehavior(Segments, null!, Increment, Clear, Invalidate));
        _ = Should.Throw<ArgumentNullException>(() =>
            new SegmentFieldBehavior(Segments, ApplyDigit, null!, Clear, Invalidate));
        _ = Should.Throw<ArgumentNullException>(() =>
            new SegmentFieldBehavior(Segments, ApplyDigit, Increment, null!, Invalidate));
        _ = Should.Throw<ArgumentNullException>(() =>
            new SegmentFieldBehavior(Segments, ApplyDigit, Increment, Clear, null!));
    }

    /// <summary>Verifies a freshly constructed behavior starts at the first segment with no
    /// editable segments reported for an empty layout.</summary>
    [Fact]
    public void Construct_WhenLayoutIsEmpty_HasNoEditableSegments()
    {
        var behavior = Create();

        behavior.HasEditableSegments.ShouldBeFalse();
        behavior.ActiveSegment.ShouldBe(0);
    }

    /// <summary>Verifies a layout with editable segments reports them.</summary>
    [Fact]
    public void HasEditableSegments_WhenLayoutHasEditableSegments_IsTrue()
    {
        var behavior = CreateHourMinute();

        behavior.HasEditableSegments.ShouldBeTrue();
    }

    #endregion

    #region Navigation

    /// <summary>Verifies MoveSegment steps between editable segments only, skipping literals.</summary>
    [Fact]
    public void MoveSegment_WhenMovingForward_SkipsLiteralsAndAdvancesActiveIndex()
    {
        var behavior = CreateHourMinute();

        var moved = behavior.MoveSegment(1, wrap: false);

        moved.ShouldBeTrue();
        behavior.ActiveSegment.ShouldBe(1);
    }

    /// <summary>Verifies MoveSegment without wrap stops at the last editable segment.</summary>
    [Fact]
    public void MoveSegment_WhenAtLastSegmentWithoutWrap_DoesNotMoveAndReturnsFalse()
    {
        var behavior = CreateHourMinute();
        _ = behavior.MoveSegment(1, wrap: false);

        var moved = behavior.MoveSegment(1, wrap: false);

        moved.ShouldBeFalse();
        behavior.ActiveSegment.ShouldBe(1);
    }

    /// <summary>Verifies MoveSegment with wrap returns to the first segment past the last.</summary>
    [Fact]
    public void MoveSegment_WhenAtLastSegmentWithWrap_WrapsToFirstSegment()
    {
        var behavior = CreateHourMinute();
        _ = behavior.MoveSegment(1, wrap: true);

        var moved = behavior.MoveSegment(1, wrap: true);

        moved.ShouldBeTrue();
        behavior.ActiveSegment.ShouldBe(0);
    }

    /// <summary>Verifies MoveSegment backward without wrap stops at the first segment.</summary>
    [Fact]
    public void MoveSegment_WhenAtFirstSegmentWithoutWrap_DoesNotMoveAndReturnsFalse()
    {
        var behavior = CreateHourMinute();

        var moved = behavior.MoveSegment(-1, wrap: false);

        moved.ShouldBeFalse();
        behavior.ActiveSegment.ShouldBe(0);
    }

    /// <summary>Verifies MoveToEdge jumps directly to the first or last editable segment.</summary>
    [Fact]
    public void MoveToEdge_WhenRequestingLast_JumpsDirectlyPastIntermediateSegments()
    {
        var behavior = CreateHourMinute();

        var moved = behavior.MoveToEdge(first: false);

        moved.ShouldBeTrue();
        behavior.ActiveSegment.ShouldBe(1);

        moved = behavior.MoveToEdge(first: true);

        moved.ShouldBeTrue();
        behavior.ActiveSegment.ShouldBe(0);
    }

    /// <summary>Verifies MoveToEdge is a no-op, returning false, when already at the requested edge.</summary>
    [Fact]
    public void MoveToEdge_WhenAlreadyAtRequestedEdge_ReturnsFalse()
    {
        var behavior = CreateHourMinute();

        var moved = behavior.MoveToEdge(first: true);

        moved.ShouldBeFalse();
        behavior.ActiveSegment.ShouldBe(0);
    }

    /// <summary>Verifies every navigation move invalidates rendering exactly once.</summary>
    [Fact]
    public void MoveSegment_WhenActiveSegmentChanges_InvalidatesOnce()
    {
        List<int> invalidations = [];
        var behavior = CreateHourMinute(invalidations);

        _ = behavior.MoveSegment(1, wrap: false);

        invalidations.Count.ShouldBe(1);
    }

    /// <summary>Verifies ActivateFirstSegment resets to the first segment and discards a partial digit.</summary>
    [Fact]
    public void ActivateFirstSegment_WhenActiveSegmentIsNotFirst_ResetsToFirstAndClearsBuffer()
    {
        var behavior = CreateHourMinute();
        _ = behavior.MoveSegment(1, wrap: false);
        _ = behavior.TypeDigit(1);

        behavior.ActivateFirstSegment();

        behavior.ActiveSegment.ShouldBe(0);

        // The buffered "1" from the second segment must not still be live: a fresh digit commits
        // immediately as a single-digit value rather than combining with the discarded buffer.
        _ = behavior.TypeDigit(9);
        _hour.ShouldBe(9);
    }

    /// <summary>Verifies ActivateSegment jumps directly to a known editable-segment index and
    /// discards any partial digit, ignoring an out-of-range index.</summary>
    [Fact]
    public void ActivateSegment_WhenIndexIsValid_MovesAndInvalidates()
    {
        List<int> invalidations = [];
        var behavior = CreateHourMinute(invalidations);

        behavior.ActivateSegment(1);

        behavior.ActiveSegment.ShouldBe(1);
        invalidations.Count.ShouldBe(1);

        behavior.ActivateSegment(5);

        behavior.ActiveSegment.ShouldBe(1);
    }

    /// <summary>Verifies ClampActiveSegment pulls an out-of-range active index back into a
    /// shrunken layout, such as when a control toggles a segment off.</summary>
    [Fact]
    public void ClampActiveSegment_WhenActiveIndexExceedsShrunkenLayout_ClampsToLastValidIndex()
    {
        var count = 2;
        var behavior = Create(segmentsProvider: () => count == 2
            ?
            [
                new SegmentDescriptor("00", TemporalSegmentKind.Hour, 2, 23),
                new SegmentDescriptor("00", TemporalSegmentKind.Minute, 2, 59)
            ]
            : [new SegmentDescriptor("00", TemporalSegmentKind.Hour, 2, 23)]);
        _ = behavior.MoveSegment(1, wrap: false);
        behavior.ActiveSegment.ShouldBe(1);

        count = 1;
        behavior.ClampActiveSegment();

        behavior.ActiveSegment.ShouldBe(0);
    }

    #endregion

    #region Digit entry

    /// <summary>Verifies a first digit above the segment's overflow threshold commits immediately
    /// and advances to the next segment - typing "6" for an hour field commits at once since no
    /// valid two-digit value under 24 starts with 6.</summary>
    [Fact]
    public void TypeDigit_WhenFirstDigitExceedsOverflowThreshold_CommitsImmediatelyAndAdvances()
    {
        var behavior = CreateHourMinute();

        var changed = behavior.TypeDigit(6);

        changed.ShouldBeTrue();
        _hour.ShouldBe(6);
        behavior.ActiveSegment.ShouldBe(1);
    }

    /// <summary>Verifies a first digit at or below the overflow threshold buffers instead of
    /// committing, awaiting a second digit.</summary>
    [Fact]
    public void TypeDigit_WhenFirstDigitIsWithinThreshold_BuffersWithoutAdvancing()
    {
        var behavior = CreateHourMinute();

        var changed = behavior.TypeDigit(1);

        changed.ShouldBeTrue();
        _hour.ShouldBe(1);
        behavior.ActiveSegment.ShouldBe(0);
    }

    /// <summary>Verifies a second digit combines with the buffered first digit, commits, and
    /// advances to the next segment.</summary>
    [Fact]
    public void TypeDigit_WhenSecondDigitCompletesCapacity_CombinesCommitsAndAdvances()
    {
        var behavior = CreateHourMinute();
        _ = behavior.TypeDigit(1);

        var changed = behavior.TypeDigit(8);

        changed.ShouldBeTrue();
        _hour.ShouldBe(18);
        behavior.ActiveSegment.ShouldBe(1);
    }

    /// <summary>Verifies typing the last editable segment's second digit commits without
    /// advancing past the end of the layout.</summary>
    [Fact]
    public void TypeDigit_WhenCommittingLastSegment_DoesNotAdvancePastEnd()
    {
        var behavior = CreateHourMinute();
        _ = behavior.MoveToEdge(first: false);
        _ = behavior.TypeDigit(3);

        var changed = behavior.TypeDigit(0);

        changed.ShouldBeTrue();
        _minute.ShouldBe(30);
        behavior.ActiveSegment.ShouldBe(1);
    }

    /// <summary>Verifies TypeDigit rejects a digit outside 0 through 9.</summary>
    [Fact]
    public void TypeDigit_WhenDigitIsOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var behavior = CreateHourMinute();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => behavior.TypeDigit(-1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => behavior.TypeDigit(10));
    }

    /// <summary>Verifies ResetDigitBuffer discards a partial digit without moving the active
    /// segment or touching the value.</summary>
    [Fact]
    public void ResetDigitBuffer_AfterPartialDigit_DiscardsBufferWithoutCommitting()
    {
        var behavior = CreateHourMinute();
        _ = behavior.TypeDigit(1);

        behavior.ResetDigitBuffer();
        _ = behavior.TypeDigit(9);

        // A fresh single "9" commits immediately (9 exceeds the two-digit overflow threshold for
        // an hour segment), proving the earlier "1" was discarded rather than combined into "19".
        _hour.ShouldBe(9);
        behavior.ActiveSegment.ShouldBe(1);
    }

    /// <summary>Verifies a digit typed against a non-digit-typable segment (zero digit capacity)
    /// is rejected without changing the value or active segment.</summary>
    [Fact]
    public void TypeDigit_WhenActiveSegmentIsNotDigitTypable_ReturnsFalse()
    {
        var behavior = Create(
            segmentsProvider: static () => [new SegmentDescriptor("AM", TemporalSegmentKind.AmPmDesignator, 0, 0)]);

        var changed = behavior.TypeDigit(5);

        changed.ShouldBeFalse();
        behavior.ActiveSegment.ShouldBe(0);
    }

    #endregion

    #region Increment and clear

    /// <summary>Verifies Increment routes a positive or negative delta to the active segment's own
    /// kind and discards any partial digit.</summary>
    [Fact]
    public void Increment_WhenActiveSegmentHasAValue_AppliesDeltaAndClearsBuffer()
    {
        var behavior = CreateHourMinute();
        _ = behavior.TypeDigit(1);

        var changed = behavior.Increment(1);

        changed.ShouldBeTrue();
        _hour.ShouldBe(2);

        // The buffered "1" is gone: a fresh single digit commits immediately at 9, not 91.
        _ = behavior.TypeDigit(9);
        _hour.ShouldBe(9);
    }

    /// <summary>Verifies Increment on an empty layout is a safe no-op.</summary>
    [Fact]
    public void Increment_WhenNoEditableSegmentsExist_ReturnsFalse()
    {
        var behavior = Create();

        var changed = behavior.Increment(1);

        changed.ShouldBeFalse();
    }

    /// <summary>Verifies ClearActiveSegment resets the active segment's value through the supplied
    /// callback and discards any partial digit.</summary>
    [Fact]
    public void ClearActiveSegment_WhenActiveSegmentHasAValue_ResetsAndClearsBuffer()
    {
        var behavior = CreateHourMinute();
        _ = behavior.TypeDigit(1);
        _ = behavior.TypeDigit(8);
        // Committing the second digit auto-advances to the minute segment; move back so the
        // clear below targets the hour segment this test is actually about.
        _ = behavior.MoveSegment(-1, wrap: false);

        var changed = behavior.ClearActiveSegment();

        changed.ShouldBeTrue();
        _hour.ShouldBe(0);
    }

    /// <summary>Verifies ClearActiveSegment on an empty layout is a safe no-op.</summary>
    [Fact]
    public void ClearActiveSegment_WhenNoEditableSegmentsExist_ReturnsFalse()
    {
        var behavior = Create();

        var changed = behavior.ClearActiveSegment();

        changed.ShouldBeFalse();
    }

    #endregion

    #region HandleKey

    /// <summary>Verifies Delete on an already-empty field (nothing left for the supplied clear
    /// callback to change) is still recognized and handled, exactly like every sibling arm of the
    /// HandleKey switch (Backspace included) that gates <c>recognized</c> on
    /// <see cref="SegmentFieldBehavior.HasEditableSegments"/> alone rather than on whether the
    /// commanded mutation actually produced a change. Before the fix, Delete's outcome-dependent
    /// <c>recognized</c> flag let an already-empty field's Delete key bubble to ancestors instead
    /// of being consumed here.</summary>
    [Fact]
    public void HandleKey_WhenDeleteIsPressedOnAlreadyEmptyField_IsHandled()
    {
        var behavior = CreateHourMinute();
        var options = new SegmentFieldKeyOptions(
            resolveStepDelta: static _ => null,
            clearValue: static () => false, // Nothing to clear: the field is already empty.
            handleRecognizedWithoutChange: true); // Matches DateInput's own options.
        var eventArgs = new KeyEventArgs(new Stroke(
            Code.Delete, character: null, nativeCode: 0, Modifiers.None, KeyAction.Press));

        behavior.HandleKey(eventArgs, options);

        eventArgs.IsHandled.ShouldBeTrue();
    }

    /// <summary>Verifies Backspace on an already-empty field is handled the same way, confirming
    /// Delete's fixed behavior now matches its sibling rather than diverging from it.</summary>
    [Fact]
    public void HandleKey_WhenBackspaceIsPressedOnAlreadyEmptyField_IsHandled()
    {
        var behavior = CreateHourMinute();
        var options = new SegmentFieldKeyOptions(
            resolveStepDelta: static _ => null,
            clearValue: static () => false,
            handleRecognizedWithoutChange: true);
        var eventArgs = new KeyEventArgs(new Stroke(
            Code.Backspace, character: null, nativeCode: 0, Modifiers.None, KeyAction.Press));

        behavior.HandleKey(eventArgs, options);

        eventArgs.IsHandled.ShouldBeTrue();
    }

    #endregion

    #region Pointer hit-testing

    /// <summary>Verifies ActivateSegmentAtColumn resolves the editable segment whose rendered
    /// column range contains the given column, skipping literal text, and invalidates.</summary>
    [Fact]
    public void ActivateSegmentAtColumn_WhenColumnFallsInsideSecondSegment_ActivatesIt()
    {
        List<int> invalidations = [];
        var behavior = CreateHourMinute(invalidations);

        // Layout is "00:00": columns 0-1 are the hour, column 2 is the literal colon, columns 3-4
        // are the minute.
        var activated = behavior.ActivateSegmentAtColumn(3);

        activated.ShouldBeTrue();
        behavior.ActiveSegment.ShouldBe(1);
        invalidations.Count.ShouldBe(1);
    }

    /// <summary>Verifies SegmentIndexAtColumn resolves a column landing on a literal separator to
    /// the next editable segment reached while scanning forward, since a literal never updates the
    /// running threshold itself.</summary>
    [Fact]
    public void SegmentIndexAtColumn_WhenColumnFallsOnALiteral_ResolvesToTheFollowingEditableSegment()
    {
        var behavior = CreateHourMinute();

        var index = behavior.SegmentIndexAtColumn(2);

        index.ShouldBe(1);
    }

    /// <summary>Verifies a column past the end of the rendered layout resolves to the last editable segment.</summary>
    [Fact]
    public void SegmentIndexAtColumn_WhenColumnIsPastTheEnd_ResolvesToLastEditableSegment()
    {
        var behavior = CreateHourMinute();

        var index = behavior.SegmentIndexAtColumn(999);

        index.ShouldBe(1);
    }

    /// <summary>Verifies ActivateSegmentAtColumn against an all-literal layout finds nothing and
    /// leaves the active segment untouched.</summary>
    [Fact]
    public void ActivateSegmentAtColumn_WhenLayoutHasNoEditableSegments_ReturnsFalse()
    {
        var behavior = Create(segmentsProvider: static () => [new SegmentDescriptor("literal")]);

        var activated = behavior.ActivateSegmentAtColumn(0);

        activated.ShouldBeFalse();
        behavior.ActiveSegment.ShouldBe(0);
    }

    #endregion
}
