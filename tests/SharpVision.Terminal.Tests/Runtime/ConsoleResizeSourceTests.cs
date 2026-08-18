// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using SharpVision.Terminal.Tests.Capabilities;

/// <summary>Verifies portable console cell-size polling distinguishes a measurement failure from
/// a genuine suspend.</summary>
public sealed class ConsoleResizeSourceTests
{
    /// <summary>Verifies a transient measurement failure is never published as a resize, and that
    /// polling recovers and reports the real dimensions once measurement succeeds again.</summary>
    [Fact]
    public async Task ReadAsync_WhenMeasurementFailsThenRecovers_NeverPublishesTheFailureAsync()
    {
        var clock = new ManualTimeProvider();
        var calls = 0;
        await using var source = new ConsoleResizeSource(TimeSpan.FromMilliseconds(10), clock, () =>
        {
            calls++;

            return calls switch
            {
                1 => null,
                2 => null,
                _ => new Size(80, 24)
            };
        });

        var reading = source.ReadAsync(TestContext.Current.CancellationToken).AsTask();
        clock.Advance(TimeSpan.FromMilliseconds(10));
        clock.Advance(TimeSpan.FromMilliseconds(10));
        var dimensions = await reading;

        dimensions.Cells.ShouldBe(new Size(80, 24));
        dimensions.Suspended.ShouldBeFalse();
        calls.ShouldBe(3);
    }

    /// <summary>Verifies a genuine 0x0 measurement — the console API succeeding but reporting no
    /// cells, as happens while the terminal is actually suspended — is still published normally,
    /// so only the failure path (a thrown exception) is suppressed, not a real suspend.</summary>
    [Fact]
    public async Task ReadAsync_WhenConsoleGenuinelyReportsZeroCells_PublishesTheSuspendAsync()
    {
        await using var source = new ConsoleResizeSource(
            TimeSpan.FromMilliseconds(10),
            timeProvider: null,
            readCells: () => new Size(0, 0));

        var dimensions = await source.ReadAsync(TestContext.Current.CancellationToken);

        dimensions.Cells.ShouldBe(new Size(0, 0));
        dimensions.Suspended.ShouldBeTrue();
    }

    /// <summary>Verifies TryReadCurrent reports failure rather than a synthesized 0x0 dimension
    /// when the measurement boundary fails.</summary>
    [Fact]
    public async Task TryReadCurrent_WhenMeasurementFails_ReturnsFalseAsync()
    {
        await using var source = new ConsoleResizeSource(
            TimeSpan.FromMilliseconds(10),
            timeProvider: null,
            readCells: () => null);

        var result = source.TryReadCurrent(out var value);

        result.ShouldBeFalse();
        value.ShouldBe(default);
    }

    /// <summary>Verifies TryReadCurrent still reports success for a genuine 0x0 measurement.</summary>
    [Fact]
    public async Task TryReadCurrent_WhenConsoleGenuinelyReportsZeroCells_ReturnsTrueAsync()
    {
        await using var source = new ConsoleResizeSource(
            TimeSpan.FromMilliseconds(10),
            timeProvider: null,
            readCells: () => new Size(0, 0));

        var result = source.TryReadCurrent(out var value);

        result.ShouldBeTrue();
        value.Cells.ShouldBe(new Size(0, 0));
    }

    /// <summary>Verifies a TryReadCurrent snapshot is remembered as the last published size, so an
    /// immediately following ReadAsync does not treat the unchanged size as a fresh resize and
    /// republish a duplicate event right after startup's initial snapshot.</summary>
    [Fact]
    public async Task ReadAsync_WhenCalledAfterTryReadCurrentReportsSameSize_DoesNotRepublishAsync()
    {
        var clock = new ManualTimeProvider();
        var size = new Size(80, 24);
        await using var source = new ConsoleResizeSource(
            TimeSpan.FromMilliseconds(10),
            clock,
            () => size);

        var seeded = source.TryReadCurrent(out var seededValue);
        seeded.ShouldBeTrue();
        seededValue.Cells.ShouldBe(size);

        var reading = source.ReadAsync(TestContext.Current.CancellationToken).AsTask();
        clock.Advance(TimeSpan.FromMilliseconds(10));

        // The unchanged size must not resolve ReadAsync immediately; only a later genuine change
        // should. Advance the clock once more with a different size to prove the loop is still
        // polling normally rather than having (incorrectly) already resolved from stale state.
        size = new Size(100, 30);
        clock.Advance(TimeSpan.FromMilliseconds(10));
        var dimensions = await reading;

        dimensions.Cells.ShouldBe(new Size(100, 30));
    }
}
