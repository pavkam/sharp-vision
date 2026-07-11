using SharpVision.Terminal.Unicode;

using Shouldly;

namespace SharpVision.Terminal.Tests.Unicode;

/// <summary>
/// Verifies whole-span measurement counters and allocation behavior.
/// </summary>
public sealed class MeasurementTests
{
    /// <summary>
    /// Verifies policy validation itself remains allocation-free after warm-up.
    /// </summary>
    [Fact]
    public void Measure_WhenEmptyAndWarm_AllocatesNoManagedBytes()
    {
        for (var index = 0; index < 100; index++)
        {
            _ = Width.Measure([], Ambiguous.Narrow);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 10_000; index++)
        {
            _ = Width.Measure([], Ambiguous.Narrow);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBe(0);
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
