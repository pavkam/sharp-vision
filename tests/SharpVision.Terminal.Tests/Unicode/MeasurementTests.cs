// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Unicode;



/// <summary>
/// Verifies whole-span measurement counters and allocation behavior.
/// </summary>
public sealed class MeasurementTests
{
    /// <summary>Verifies explicit Unicode-value constructors reject impossible ranges and counts.</summary>
    [Fact]
    public void Constructor_WhenUnicodeValueIsInvalid_ThrowsDocumentedException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new Grapheme(offset: -1, length: 1, hasInvalidData: false));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new Grapheme(offset: 0, length: 0, hasInvalidData: false));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new Measurement(cells: -1, graphemes: 0, controls: 0));
    }

    /// <summary>
    /// Verifies policy validation itself remains allocation-free after warm-up.
    /// </summary>
    [Fact]
    public void Measure_WhenEmptyAndWarm_AllocatesNoManagedBytes()
    {
        for (var index = 0; index < 10_000; index++)
        {
            _ = Width.Measure([], Ambiguous.Narrow);
        }

        var minimum = long.MaxValue;

        // Sample multiple windows so one-time tiered-PGO bookkeeping cannot
        // become product allocation data.
        for (var sample = 0; sample < 5; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var index = 0; index < 10_000; index++)
            {
                _ = Width.Measure([], Ambiguous.Narrow);
            }

            minimum = Math.Min(
                minimum,
                GC.GetAllocatedBytesForCurrentThread() - before);
        }

        minimum.ShouldBe(0);
    }

    /// <summary>
    /// Verifies contextual controls are counted but consume no printable cells.
    /// </summary>
    [Fact]
    public void Measure_WhenTextContainsControls_ReportsThemSeparately()
    {
        var result = Width.Measure("\t\r\n".AsSpan(), Ambiguous.Narrow);

        result.Cells.ShouldBe(0);
        result.Graphemes.ShouldBe(2);
        result.Controls.ShouldBe(2);
    }

    /// <summary>
    /// Verifies warmed mixed measurement allocates no managed objects.
    /// </summary>
    [Fact]
    public void Measure_WhenWarm_AllocatesNoManagedBytes()
    {
        var value = "ASCII e\u0301 · 界 👩🏽‍💻 🇵🇹";

        for (var index = 0; index < 10_000; index++)
        {
            _ = Width.Measure(value.AsSpan(), Ambiguous.Narrow);
        }

        var minimum = long.MaxValue;

        // Sample multiple windows so a one-time tiered-PGO bookkeeping
        // allocation from another concurrently run test cannot become data.
        for (var sample = 0; sample < 5; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var index = 0; index < 10_000; index++)
            {
                _ = Width.Measure(value.AsSpan(), Ambiguous.Narrow);
            }

            minimum = Math.Min(
                minimum,
                GC.GetAllocatedBytesForCurrentThread() - before);
        }

        minimum.ShouldBe(0);
    }
}
