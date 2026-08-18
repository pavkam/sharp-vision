// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;


/// <summary>Gates interactive control allocation reuse for a representative warmed tree.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class InteractivePerformanceTests
{
    /// <summary>Verifies warmed interactive layout/render reuse at representative terminal sizes.</summary>
    [Theory]
    [InlineData(80, 24)]
    [InlineData(200, 60)]
    public void Render_WhenInteractiveTreeIsWarm_AllocatesNoManagedMemory(int width, int height)
    {
        var root = Representative();
        var size = new Size(width, height);
        var engine = new LayoutEngine();
        using Frame frame = new(size);
        Render();

        var allocated = Minimum(Render, iterations: 200, out var elapsed);

        allocated.ShouldBe(0);
        Report($"interactive {width}x{height}", elapsed, 200);
        return;

        void Render()
        {
            engine.Layout(root, size);
            frame.Clear();
            root.Invalidate(Invalidation.Render);
            root.Render(frame.Canvas);
        }
    }

    private static Grid Representative()
    {
        var root = new Grid();
        root.Columns.Add(Track.Cells(24));
        root.Columns.Add(Track.Star(1));
        var list = new UiListView
        {
            Items = Enumerable.Range(0, 20).Select(static value => (object?) $"Item {value}").ToArray()
        };
        Grid.SetColumn(list, 0);
        var content = new Stack();
        Grid.SetColumn(content, 1);
        content.Children.Add(new TextInput { Text = "e\u0301 · 界 · 👩‍💻" });
        content.Children.Add(new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Maximum = 100,
            Value = 40,
            ViewportSize = 20
        });
        content.Children.Add(
            new Stack { AutoScroll = true, Children = { new ControlText("wide 界 and emoji 👩‍💻 content") } });
        root.Children.Add(list);
        root.Children.Add(content);
        return root;
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

    private static void Report(string scenario, TimeSpan elapsed, int iterations)
    {
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{scenario}: {iterations} iterations in {elapsed.TotalMilliseconds:F3} ms; " +
            $"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.ProcessArchitecture}; " +
            RuntimeInformation.OSDescription);
    }
}
