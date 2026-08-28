// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies the shared Calendar ownership and synchronization engine used by
/// date-bearing temporal inputs.</summary>
public sealed class CalendarDropDownCoordinatorTests
{
    /// <summary>Verifies a failed owner projection cannot strand the depth-counted
    /// programmatic-synchronization guard.</summary>
    [Fact]
    public void SyncValue_WhenDateProjectionThrows_RestoresSynchronizationDepth()
    {
        var fail = false;
        using var coordinator = Create(
            extractDate: value => fail
                ? throw new InvalidOperationException("projection failure")
                : DateOnly.FromDateTime(value));
        fail = true;

        _ = Should.Throw<InvalidOperationException>(() =>
            coordinator.SyncValue(new DateTime(2026, 8, 28)));

        coordinator.IsSynchronizing.ShouldBeFalse();
    }

    /// <summary>Verifies accepting a Calendar date delegates combination exactly once and
    /// preserves the current DateTime's time, Kind, and sub-second ticks.</summary>
    [Fact]
    public void AcceptSession_WhenCurrentValueHasPrecision_CombinesAndCommitsOnce()
    {
        var current = new DateTime(2026, 8, 28, 12, 34, 56, 789, DateTimeKind.Utc).AddTicks(4321);
        DateTime? committed = null;
        var commits = 0;
        using var coordinator = Create(
            getValue: () => current,
            setValue: value =>
            {
                commits++;
                committed = value;
            });
        coordinator.Calendar.Selection = new DateInterval(
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 1));

        coordinator.AcceptSession();

        commits.ShouldBe(1);
        _ = committed.ShouldNotBeNull();
        committed.Value.Date.ShouldBe(new DateTime(2026, 9, 1));
        committed.Value.TimeOfDay.ShouldBe(current.TimeOfDay);
        committed.Value.Kind.ShouldBe(DateTimeKind.Utc);
    }

    private static CalendarDropDownCoordinator<DateTime> Create(
        Func<DateTime?>? getValue = null,
        Action<DateTime?>? setValue = null,
        Func<DateTime, DateOnly>? extractDate = null) =>
        new(
            CultureInfo.InvariantCulture,
            static () => { },
            getValue ?? (static () => new DateTime(2026, 8, 28, 12, 0, 0)),
            setValue ?? (static _ => { }),
            extractDate ?? (static value => DateOnly.FromDateTime(value)),
            static (date, current) => date.ToDateTime(
                TimeOnly.FromTimeSpan(current?.TimeOfDay ?? TimeSpan.Zero),
                current?.Kind ?? DateTimeKind.Unspecified),
            static () => DateOnly.MinValue,
            static () => DateOnly.MaxValue,
            static () => 0,
            static () => 0,
            static () => false,
            static () => { },
            static () => { });
}
