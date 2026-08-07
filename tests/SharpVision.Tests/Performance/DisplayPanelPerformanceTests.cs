// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;


/// <summary>Gates allocation reuse for representative and large display trees.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class DisplayPanelPerformanceTests
{
    /// <summary>Verifies representative viewport trees reuse layout and render storage.</summary>
    [Theory]
    [InlineData(80, 24)]
    [InlineData(200, 60)]
    public void Render_WhenDisplayTreeIsWarm_AllocatesNoManagedMemory(int width, int height)
    {
        var root = Representative();
        var size = new Size(width, height);
        var engine = new LayoutEngine();
        using Frame frame = new(size);

        Run();

        var allocated = Minimum(Run, iterations: 500, out var elapsed);

        allocated.ShouldBe(0);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"display {width}x{height}: 500 iterations in {elapsed.TotalMilliseconds:F3} ms");

        return;

        void Run()
        {
            engine.Layout(root, size);
            frame.Clear();
            root.Invalidate(Invalidation.Render);
            root.Render(frame.Canvas);
        }
    }

    /// <summary>Verifies a 1,000-child Grid/Stack tree reuses unchanged layout storage.</summary>
    [Fact]
    public void Layout_WhenTreeHasOneThousandChildren_AllocatesNoManagedMemoryAfterWarmup()
    {
        var grid = new Grid();
        grid.Rows.Add(Track.Star(1));
        grid.Columns.Add(Track.Star(1));
        var stack = new Stack();
        grid.Children.Add(stack);

        for (var index = 0; index < 1_000; index++)
        {
            stack.Children.Add(new ControlText((index % 10).ToString(CultureInfo.InvariantCulture)));
        }

        var engine = new LayoutEngine();
        var size = new Size(200, 60);
        engine.Layout(grid, size);

        var allocated = Minimum(() => engine.Layout(grid, size), 1_000, out var elapsed);

        allocated.ShouldBe(0);
        stack.Children.Count.ShouldBe(1_000);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"1,000-child layout: 1,000 iterations in {elapsed.TotalMilliseconds:F3} ms");
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

    private static Grid Representative()
    {
        var root = new Grid();
        root.Columns.Add(Track.Cells(24));
        root.Columns.Add(Track.Star(1));
        var navigation = new Stack { Spacing = 1 };
        Grid.SetColumn(navigation, 0);
        var content = new Overlay();
        Grid.SetColumn(content, 1);
        root.Children.Add(navigation);
        root.Children.Add(content);

        for (var index = 0; index < 12; index++)
        {
            navigation.Children.Add(new Dock
            {
                Border = AppearanceTestValues.Border(BorderSide.All),
                Children = { new ControlText($"Item {index}") }
            });
        }

        content.Children.Add(new ControlText("e\u0301 · 界 · 👩‍💻") { Overflow = Overflow.Wrap });
        return root;
    }
}
