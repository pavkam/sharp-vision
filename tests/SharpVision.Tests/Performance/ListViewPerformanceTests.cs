// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

/// <summary>Gates ListView item-replacement allocation reuse and detached-item collection.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class ListViewPerformanceTests
{
    /// <summary>Verifies 1,000-item unchanged layout reuse and complete detached-control collection.</summary>
    [Fact]
    public void Items_WhenOneThousandAreReplaced_AllocatesNoUnchangedLayoutAndRetainsNone()
    {
        var list = new UiListView();
        var weak = PopulateAndReplace(list, count: 1_000);
        var engine = new LayoutEngine();
        var size = new Size(200, 60);
        engine.Layout(list, size);

        var allocated = Minimum(() => engine.Layout(list, size), 1_000, out var elapsed);
        ForceCollection();

        allocated.ShouldBe(0);
        weak.Count(reference => reference.TryGetTarget(out _)).ShouldBe(0);
        list.Items.ShouldBeEmpty();
        Report("1,000-item replacement and unchanged layout", elapsed, 1_000);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<ControlBase>[] PopulateAndReplace(UiListView list, int count)
    {
        var weak = new WeakReference<ControlBase>[count];
        var created = 0;
        list.ItemTemplate = item =>
        {
            var control = new ControlText(Convert.ToString(item, CultureInfo.InvariantCulture) ?? string.Empty);
            weak[created++] = new WeakReference<ControlBase>(control);
            return control;
        };
        list.Items = Enumerable.Range(0, count).Select(static value => (object?) value).ToArray();
        list.Items = Array.Empty<object?>();
        return weak;
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

    private static void ForceCollection()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    private static void Report(string scenario, TimeSpan elapsed, int iterations)
    {
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{scenario}: {iterations} iterations in {elapsed.TotalMilliseconds:F3} ms; " +
            $"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.ProcessArchitecture}; " +
            RuntimeInformation.OSDescription);
    }
}
