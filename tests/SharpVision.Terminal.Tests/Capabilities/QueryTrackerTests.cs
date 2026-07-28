// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;

/// <summary>
/// Verifies bounded query registration, correlation, and deadlines.
/// </summary>
public sealed class QueryTrackerTests
{
    /// <summary>Gets every uncorrelated standard startup response family.</summary>
    public static TheoryData<QueryKind> StandardFamilies { get; } =
    [
        QueryKind.PrimaryAttributes,
        QueryKind.SecondaryAttributes,
        QueryKind.PrivateMode,
        QueryKind.ForegroundColor,
        QueryKind.BackgroundColor,
        QueryKind.Keyboard,
        QueryKind.PaletteColor,
        QueryKind.WindowPixels,
        QueryKind.CellPixels,
        QueryKind.WindowCells,
        QueryKind.ModifyOtherKeys
    ];

    /// <summary>Verifies status identity, failure, duplicate, and late classification are tracker-owned.</summary>
    [Fact]
    public void Match_WhenStatusIdentityVaries_PreservesExactActiveRequest()
    {
        var clock = new ManualTimeProvider();
        var tracker = new QueryTracker(Limits.Default, clock);
        tracker.TryRegister(StatusName.ModifyOtherKeys, out _).ShouldBeTrue();
        XtermDecrqss.TryParse("1"u8, "$"u8, (byte) 'r', "0m"u8, out var wrong).ShouldBeTrue();
        XtermDecrqss.TryParse("0"u8, "$"u8, (byte) 'r', [], out var failed).ShouldBeTrue();
        XtermDecrqss.TryParse("1"u8, "$"u8, (byte) 'r', ">4;2m"u8, out var matched).ShouldBeTrue();

        tracker.Match(in wrong).ShouldBe(QueryMatch.Unknown);
        tracker.Match(in failed).ShouldBe(QueryMatch.Unknown);
        tracker.ActiveCount.ShouldBe(1);
        tracker.Match(in matched).ShouldBe(QueryMatch.Matched);
        tracker.Match(in matched).ShouldBe(QueryMatch.Duplicate);

        clock.Advance(Limits.Default.QueryTimeout);
        tracker.TryRegister(StatusName.ModifyOtherKeys, out _).ShouldBeTrue();
        clock.Advance(Limits.Default.QueryTimeout);
        tracker.Match(in matched).ShouldBe(QueryMatch.Late);
    }

    /// <summary>Verifies capability identity, failure, duplicate, and late classification are tracker-owned.</summary>
    [Fact]
    public void Match_WhenCapabilityIdentityVaries_PreservesExactActiveRequest()
    {
        var clock = new ManualTimeProvider();
        var tracker = new QueryTracker(Limits.Default, clock);
        tracker.TryRegister(CapabilityName.DirectColor, out _).ShouldBeTrue();
        var wrong = Capability("6B63757531=1B5B41"u8);
        var failed = Capability([], valid: false);
        var matched = Capability("524742=3234"u8);

        tracker.Match(wrong).ShouldBe(QueryMatch.Unknown);
        tracker.Match(failed).ShouldBe(QueryMatch.Unknown);
        tracker.ActiveCount.ShouldBe(1);
        tracker.Match(matched).ShouldBe(QueryMatch.Matched);
        tracker.Match(matched).ShouldBe(QueryMatch.Duplicate);

        clock.Advance(Limits.Default.QueryTimeout);
        tracker.TryRegister(CapabilityName.DirectColor, out _).ShouldBeTrue();
        clock.Advance(Limits.Default.QueryTimeout);
        tracker.Match(matched).ShouldBe(QueryMatch.Late);
    }

    /// <summary>Verifies DCS families cannot bypass typed selector registration.</summary>
    [Fact]
    public void TryRegister_WhenDcsIdentityIsMissing_RejectsGenericRegistration()
    {
        var tracker = new QueryTracker();

        _ = Should.Throw<ArgumentException>(() =>
            tracker.TryRegister(QueryKind.StatusString, null, out _));
        _ = Should.Throw<ArgumentException>(() =>
            tracker.TryRegister(QueryKind.CapabilityString, null, out _));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            tracker.TryRegister(StatusName.Unknown, out _));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            tracker.TryRegister((CapabilityName) 999, out _));
        tracker.ActiveCount.ShouldBe(0);
    }

    /// <summary>
    /// Verifies only one active uncorrelated query per response family.
    /// </summary>
    [Fact]
    public void TryRegister_WhenUncorrelatedKindIsActive_RejectsDuplicate()
    {
        var tracker = new QueryTracker();

        tracker.TryRegister(QueryKind.PrimaryAttributes, id: null, out _).ShouldBeTrue();
        tracker.TryRegister(QueryKind.PrimaryAttributes, id: null, out _).ShouldBeFalse();

        tracker.ActiveCount.ShouldBe(1);
        tracker.LastDiagnostic!.Value.Code.ShouldBe(DiagnosticCode.QueryLimit);
    }

    /// <summary>
    /// Verifies unique Kitty IDs correlate independently.
    /// </summary>
    [Fact]
    public void Match_WhenKittyIdsAreUnique_MatchesCorrectQuery()
    {
        var tracker = new QueryTracker();
        _ = tracker.TryRegister(QueryKind.KittyClipboard, "one", out _);
        _ = tracker.TryRegister(QueryKind.KittyClipboard, "two", out _);

        tracker.Match(QueryKind.KittyClipboard, "two").ShouldBe(QueryMatch.Matched);

        tracker.ActiveCount.ShouldBe(1);
    }

    /// <summary>
    /// Verifies maximum total concurrency is finite.
    /// </summary>
    [Fact]
    public void TryRegister_WhenConcurrencyLimitIsReached_RejectsQuery()
    {
        var limits = Limits.Default with { MaxConcurrentQueries = 1 };
        var tracker = new QueryTracker(limits);
        _ = tracker.TryRegister(QueryKind.PrimaryAttributes, null, out _);

        tracker.TryRegister(QueryKind.CursorPosition, null, out _).ShouldBeFalse();

        tracker.LastDiagnostic!.Value.Code.ShouldBe(DiagnosticCode.QueryLimit);
    }

    /// <summary>
    /// Verifies duplicate, cancelled, and timed-out responses are distinguished.
    /// </summary>
    [Fact]
    public void Match_WhenQueryIsNoLongerActive_ReturnsDuplicateOrLate()
    {
        var clock = new ManualTimeProvider();
        var limits = Limits.Default with { QueryTimeout = TimeSpan.FromSeconds(1) };
        var tracker = new QueryTracker(limits, clock);
        _ = tracker.TryRegister(QueryKind.PrimaryAttributes, null, out var completed);
        _ = tracker.TryRegister(QueryKind.CursorPosition, null, out var cancelled);
        _ = tracker.TryRegister(QueryKind.PrivateMode, null, out _);

        tracker.Match(QueryKind.PrimaryAttributes).ShouldBe(QueryMatch.Matched);
        tracker.Match(QueryKind.PrimaryAttributes).ShouldBe(QueryMatch.Duplicate);
        tracker.Cancel(cancelled).ShouldBeTrue();
        tracker.Match(QueryKind.CursorPosition).ShouldBe(QueryMatch.Late);
        clock.Advance(TimeSpan.FromSeconds(1));
        tracker.Expire().ShouldBe(1);
        tracker.Match(QueryKind.PrivateMode).ShouldBe(QueryMatch.Late);

        completed.Value.ShouldBeGreaterThan(0);
        tracker.LastDiagnostic!.Value.Code.ShouldBe(DiagnosticCode.LateResponse);
    }

    /// <summary>Verifies an owning batch can retire every family at one shared deadline.</summary>
    [Fact]
    public void ExpireAll_WhenIndividualDeadlinesRemain_RetiresWholeBatch()
    {
        // Arrange
        var tracker = new QueryTracker();
        _ = tracker.TryRegister(QueryKind.PrimaryAttributes, null, out _);
        _ = tracker.TryRegister(QueryKind.BackgroundColor, null, out _);

        // Act
        var expired = tracker.ExpireAll();

        // Assert
        expired.ShouldBe(2);
        tracker.ActiveCount.ShouldBe(0);
        tracker.Match(QueryKind.PrimaryAttributes).ShouldBe(QueryMatch.Late);
        tracker.Match(QueryKind.BackgroundColor).ShouldBe(QueryMatch.Late);
    }

    /// <summary>Verifies exact family deadlines accept strictly before and reject at or after.</summary>
    /// <param name="kind">The uncorrelated response family.</param>
    [Theory]
    [MemberData(nameof(StandardFamilies))]
    public void Match_WhenExactDeadlineBoundaryVaries_AcceptsOnlyStrictlyBefore(QueryKind kind)
    {
        // Arrange / Act / Assert: strictly before.
        var beforeClock = new ManualTimeProvider();
        var before = new QueryTracker(timeProvider: beforeClock);
        var beforeDeadline = beforeClock.Current + TimeSpan.FromSeconds(1);
        before.TryRegister(kind, null, beforeDeadline, out _).ShouldBeTrue();
        beforeClock.AdvanceTo(beforeDeadline - TimeSpan.FromTicks(1));
        before.Match(kind).ShouldBe(QueryMatch.Matched);

        // Arrange / Act / Assert: exactly at.
        var atClock = new ManualTimeProvider();
        var at = new QueryTracker(timeProvider: atClock);
        var atDeadline = atClock.Current + TimeSpan.FromSeconds(1);
        at.TryRegister(kind, null, atDeadline, out _).ShouldBeTrue();
        atClock.AdvanceTo(atDeadline);
        at.Match(kind).ShouldBe(QueryMatch.Late);

        // Arrange / Act / Assert: after.
        var afterClock = new ManualTimeProvider();
        var after = new QueryTracker(timeProvider: afterClock);
        var afterDeadline = afterClock.Current + TimeSpan.FromSeconds(1);
        after.TryRegister(kind, null, afterDeadline, out _).ShouldBeTrue();
        afterClock.AdvanceTo(afterDeadline + TimeSpan.FromTicks(1));
        after.Match(kind).ShouldBe(QueryMatch.Late);
    }

    /// <summary>Verifies sequential registration cannot skew one caller-owned batch deadline.</summary>
    [Fact]
    public void TryRegister_WhenClockMovesBetweenFamilies_UsesOneExactDeadline()
    {
        // Arrange
        var clock = new ManualTimeProvider { AdvanceOnRead = TimeSpan.FromMilliseconds(1) };
        var tracker = new QueryTracker(timeProvider: clock);
        var deadline = clock.Current + TimeSpan.FromSeconds(1);

        // Act
        foreach (var kind in StandardFamilies)
        {
            tracker.TryRegister(kind, null, deadline, out _).ShouldBeTrue();
        }

        clock.AdvanceTo(deadline);

        // Assert
        foreach (var kind in StandardFamilies)
        {
            tracker.Match(kind).ShouldBe(QueryMatch.Late);
        }
    }

    private static CapabilityResponse Capability(ReadOnlySpan<byte> payload, bool valid = true)
    {
        XtermGetCap.TryParse(
            valid ? "1"u8 : "0"u8,
            "+"u8,
            (byte) 'r',
            payload,
            Limits.Default,
            out var response).ShouldBeTrue();
        return response!;
    }
}
