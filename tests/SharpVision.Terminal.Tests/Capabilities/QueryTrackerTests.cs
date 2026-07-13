namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Protocols;

using Shouldly;

/// <summary>
/// Verifies bounded query registration, correlation, and deadlines.
/// </summary>
public sealed class QueryTrackerTests
{
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

}
