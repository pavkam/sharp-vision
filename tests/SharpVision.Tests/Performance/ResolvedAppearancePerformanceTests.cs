// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

/// <summary>Gates the per-control resolved-appearance cache footprint and warmed rendering cost.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class ResolvedAppearancePerformanceTests
{
    /// <summary>Verifies resolving one visual state allocates a small inline cache rather than the
    /// full 512-slot combinatorial VisualState space — the prior dense array allocated roughly
    /// 148 KB (296 bytes per Nullable&lt;ResolvedAppearance&gt; slot times 512) on the very first
    /// resolution, regardless of how many states a control ever actually reaches.</summary>
    [Fact]
    public void GetActualFace_WhenFirstResolved_AllocatesFarLessThanTheFullStateSpace()
    {
        var control = new ProbeControl();
        control.SetTheme(ThemeCatalog.Dark);

        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = control.ActualFace;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBeLessThan(4_096);
    }

    /// <summary>Verifies warmed 80×24 semantic control rendering allocates nothing.</summary>
    [Fact]
    public void Render_WhenFrameAndTreeAreReused_AllocatesZeroBytes()
    {
        var control = new ProbeControl
        {
            Bounds = new Rect(0, 0, 80, 24),
            Content = "e\u0301 · 界 · 👩‍💻".AsMemory()
        };
        using Frame frame = new(new Size(80, 24));
        Render();

        var (allocated, elapsed) = Measure(Render, 1_000);

        allocated.ShouldBe(0);
        Report("80x24 control rendering", elapsed, 5_000);
        return;

        void Render()
        {
            frame.Clear();
            control.Invalidate(Invalidation.Render);
            control.Render(frame.Canvas);
        }
    }

    private static (long Allocated, TimeSpan Elapsed) Measure(Action action, int iterations)
    {
        for (var index = 0; index < iterations; index++)
        {
            action();
        }

        var minimum = long.MaxValue;
        var watch = Stopwatch.StartNew();

        for (var sample = 0; sample < 5; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var index = 0; index < iterations; index++)
            {
                action();
            }

            minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        watch.Stop();
        return (minimum, watch.Elapsed);
    }

    private static void Report(string scenario, TimeSpan elapsed, int iterations)
    {
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{scenario}: {iterations} measured iterations in {elapsed.TotalMilliseconds:F3} ms; " +
            $"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.ProcessArchitecture}; " +
            RuntimeInformation.OSDescription);
    }
}
