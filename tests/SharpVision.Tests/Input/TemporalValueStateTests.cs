// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies the shared nullable, bounded, lazily seeded value model used by every
/// segmented temporal input.</summary>
public sealed class TemporalValueStateTests
{
    /// <summary>Verifies the dispatcher-selected seed is requested once and clamped by the
    /// current range without publishing a synthetic value transition.</summary>
    [Fact]
    public void EnsureSeeded_WhenReadRepeatedly_UsesClockOnceAndClampsWithoutEvent()
    {
        var seedReads = 0;
        List<(DateOnly? Previous, DateOnly? Current)> changes = [];
        var state = new TemporalValueState<DateOnly>(
            DateOnly.MinValue,
            DateOnly.MaxValue,
            static () => { },
            static (_, _) => { },
            () =>
            {
                seedReads++;
                return new DateOnly(2026, 8, 28);
            },
            (previous, current) => changes.Add((previous, current)));

        _ = state.SetMinimum(new DateOnly(2026, 9, 1));

        var first = state.EnsureSeeded();
        var second = state.EnsureSeeded();

        first.ShouldBe(new DateOnly(2026, 9, 1));
        second.ShouldBe(first);
        seedReads.ShouldBe(1);
        changes.ShouldBeEmpty();
    }

    /// <summary>Verifies all three temporal value types share nullable admission, inclusive
    /// clamping, and bound repair without losing DateTime kind or sub-second precision.</summary>
    [Fact]
    public void SetValue_WhenTemporalKindsDiffer_PreservesTypedPrecisionAndSharedRangePolicy()
    {
        var time = CreateTimeState();
        var date = CreateDateState();
        var dateTime = CreateDateTimeState();
        var precise = new DateTime(2026, 8, 28, 12, 34, 56, 789, DateTimeKind.Local).AddTicks(4321);

        _ = time.SetMinimum(new TimeOnly(10, 0));
        _ = date.SetMaximum(new DateOnly(2026, 8, 20));
        _ = dateTime.SetValue(precise);

        _ = time.SetValue(new TimeOnly(9, 0));
        _ = date.SetValue(new DateOnly(2026, 8, 28));

        time.Value.ShouldBe(new TimeOnly(10, 0));
        date.Value.ShouldBe(new DateOnly(2026, 8, 20));
        dateTime.Value.ShouldBe(precise);
        dateTime.Value!.Value.Kind.ShouldBe(DateTimeKind.Local);
        dateTime.Value.Value.Ticks.ShouldBe(precise.Ticks);
    }

    /// <summary>Verifies a reentrant property observer supersedes an outer commit and suppresses
    /// its obsolete typed event.</summary>
    [Fact]
    public void SetValue_WhenNotificationCommitsNewerValue_RaisesOnlyCurrentEvent()
    {
        TemporalValueState<DateOnly>? state = null;
        var outer = new DateOnly(2026, 8, 20);
        var nested = new DateOnly(2026, 8, 21);
        List<(DateOnly? Previous, DateOnly? Current)> changes = [];
        state = new TemporalValueState<DateOnly>(
            DateOnly.MinValue,
            DateOnly.MaxValue,
            static () => { },
            (name, impact) =>
            {
                _ = impact;

                if (name == "Value" && state!.Value == outer)
                {
                    _ = state.SetValue(nested);
                }
            },
            static () => new DateOnly(2026, 8, 1),
            (previous, current) => changes.Add((previous, current)));

        _ = state.SetValue(outer);

        state.Value.ShouldBe(nested);
        changes.ShouldBe([(outer, nested)]);
    }

    /// <summary>Verifies a reentrant observer that restores null admission prevents an obsolete
    /// null repair after the outer property publication.</summary>
    [Fact]
    public void SetAllowNull_WhenNotificationRestoresTrue_PreservesNull()
    {
        TemporalValueState<TimeOnly>? state = null;
        state = new TemporalValueState<TimeOnly>(
            TimeOnly.MinValue,
            TimeOnly.MaxValue,
            static () => { },
            (name, impact) =>
            {
                _ = impact;

                if (name == "AllowNull" && !state!.AllowNull)
                {
                    _ = state.SetAllowNull(true);
                }
            },
            static () => new TimeOnly(12, 0),
            static (_, _) => { });
        _ = state.SetValue(null);

        _ = state.SetAllowNull(false);

        state.AllowNull.ShouldBeTrue();
        state.Value.ShouldBeNull();
    }

    /// <summary>Verifies tightening either endpoint repairs the live value and exposes monotonically
    /// increasing versions for popup-session staleness checks.</summary>
    [Fact]
    public void SetBounds_WhenEndpointsTighten_RepairsValueAndAdvancesVersions()
    {
        var state = CreateDateState();
        _ = state.SetValue(new DateOnly(2026, 8, 20));
        var valueVersion = state.ValueVersion;
        var boundsVersion = state.BoundsVersion;

        _ = state.SetMinimum(new DateOnly(2026, 8, 21));

        state.Value.ShouldBe(new DateOnly(2026, 8, 21));
        state.ValueVersion.ShouldBeGreaterThan(valueVersion);
        state.BoundsVersion.ShouldBeGreaterThan(boundsVersion);
    }

    private static TemporalValueState<TimeOnly> CreateTimeState() => new(
        TimeOnly.MinValue,
        TimeOnly.MaxValue,
        static () => { },
        static (_, _) => { },
        static () => new TimeOnly(12, 0),
        static (_, _) => { });

    private static TemporalValueState<DateOnly> CreateDateState() => new(
        DateOnly.MinValue,
        DateOnly.MaxValue,
        static () => { },
        static (_, _) => { },
        static () => new DateOnly(2026, 8, 28),
        static (_, _) => { });

    private static TemporalValueState<DateTime> CreateDateTimeState() => new(
        DateTime.MinValue,
        DateTime.MaxValue,
        static () => { },
        static (_, _) => { },
        static () => new DateTime(2026, 8, 28, 12, 0, 0),
        static (_, _) => { });
}
