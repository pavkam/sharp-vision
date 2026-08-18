// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

/// <summary>Gates Button's affix seam at zero cost when unused, using deterministic warm-path
/// allocation checks instead of a wall-clock timing budget, which measures the host machine as
/// much as the product and is inherently flaky under CI load or coverage instrumentation.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class ButtonPerformanceTests
{
    /// <summary>Verifies a warm measure/arrange/render cycle allocates no managed memory when
    /// StartAffix and EndAffix are both null - MeasureAffixes, DeflateForAffixes, and RenderAffixes
    /// run unconditionally every pass, so any per-call allocation in the null path would show up
    /// here as steady-state growth instead of a one-time warmup cost.</summary>
    [Fact]
    public void Render_WhenBothAffixesAreNull_AllocatesNoManagedMemory()
    {
        var button = new Button("Save") { Width = Length.Cells(10), Height = Length.Cells(3) };
        var engine = new LayoutEngine();
        var size = new Size(10, 3);
        using Frame frame = new(size);

        Run();

        var allocated = Minimum(Run, iterations: 500, out var elapsed);

        allocated.ShouldBe(0);
        button.StartAffix.ShouldBeNull();
        button.EndAffix.ShouldBeNull();
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Button render with no affixes: 500 iterations in {elapsed.TotalMilliseconds:F3} ms");
        return;

        void Run()
        {
            engine.Layout(button, size);
            frame.Clear();
            button.Invalidate(Invalidation.Render);
            button.Render(frame.Canvas);
        }
    }

    private static long Minimum(Action action, int iterations, out TimeSpan elapsed)
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
        elapsed = watch.Elapsed;
        return minimum;
    }
}
