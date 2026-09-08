// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves the shared nullable bounded value state, commit, event, and null-value seeding
/// contract <see cref="TemporalInputBase{TValue}"/> owns, independent of any shipped field's own
/// richer calendar or clock policy, through <see cref="YearMonthInputProbe"/>.</summary>
public sealed class TemporalInputBaseTests
{
    /// <summary>Verifies value when set outside range clamps.</summary>
    [Fact]
    public void Value_WhenSetOutsideRange_Clamps()
    {
        // Arrange
        using var probe = new YearMonthInputProbe { Minimum = new DateOnly(2020, 1, 1), Maximum = new DateOnly(2025, 12, 1) };

        // Act
        probe.Value = new DateOnly(2030, 1, 1);

        // Assert
        probe.Value.ShouldBe(new DateOnly(2025, 12, 1));
    }

    /// <summary>Verifies value changed when committed reports previous and current.</summary>
    [Fact]
    public void ValueChanged_WhenCommitted_ReportsPreviousAndCurrent()
    {
        // Arrange
        using var probe = new YearMonthInputProbe { Value = new DateOnly(2026, 1, 1) };
        TemporalValueChangedEventArgs<DateOnly>? observed = null;
        probe.ValueChanged += (_, eventArgs) => observed = eventArgs;

        // Act
        probe.Value = new DateOnly(2026, 2, 1);

        // Assert
        var raised = observed.ShouldNotBeNull();
        raised.Previous.ShouldBe(new DateOnly(2026, 1, 1));
        raised.Current.ShouldBe(new DateOnly(2026, 2, 1));
    }

    /// <summary>Verifies an Up press that reaches a cleared (null) value seeds it through
    /// <see cref="YearMonthInputProbe.ResolveClockSeed"/> - <see cref="YearMonthInputProbe.Seed"/> -
    /// rather than leaving the increment a silent no-op.</summary>
    [Fact]
    public async Task Increment_WhenValueIsNull_SeedsThroughDerivedSeedAsync()
    {
        // Arrange
        using var probe = new YearMonthInputProbe();
        probe.Value = null;
        await using var surface = await ComponentSurface.MountAsync(probe, new Size(12, 3), TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert
        probe.Value.ShouldBe(YearMonthInputProbe.Seed);
    }
}
