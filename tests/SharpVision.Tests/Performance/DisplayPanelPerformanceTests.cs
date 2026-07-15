// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

using System.Diagnostics;
using System.Globalization;



using ControlText = SharpVision.Controls.Text;

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
        Grid root = Representative();
        Size size = new(width, height);
        Engine engine = new();
        using Frame frame = new(size);
        Run();

        long allocated = Minimum(Run, iterations: 500, out TimeSpan elapsed);

        allocated.ShouldBe(0);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"display {width}x{height}: 500 iterations in {elapsed.TotalMilliseconds:F3} ms");

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
        Grid grid = new();
        grid.Rows.Add(Track.Star(1));
        grid.Columns.Add(Track.Star(1));
        Stack stack = new();
        grid.Children.Add(stack);

        for (int index = 0; index < 1_000; index++)
        {
            stack.Children.Add(new ControlText((index % 10).ToString(CultureInfo.InvariantCulture)));
        }

        Engine engine = new();
        Size size = new(200, 60);
        engine.Layout(grid, size);

        long allocated = Minimum(() => engine.Layout(grid, size), 1_000, out TimeSpan elapsed);

        allocated.ShouldBe(0);
        stack.Children.Count.ShouldBe(1_000);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"1,000-child layout: 1,000 iterations in {elapsed.TotalMilliseconds:F3} ms");
    }

    private static long Minimum(Action action, int iterations, out TimeSpan elapsed)
    {
        for (int index = 0; index < iterations; index++)
        {
            action();
        }

        long minimum = long.MaxValue;
        Stopwatch watch = Stopwatch.StartNew();

        for (int sample = 0; sample < 5; sample++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int index = 0; index < iterations; index++)
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
        Grid root = new();
        root.Columns.Add(Track.Cells(24));
        root.Columns.Add(Track.Star(1));
        Stack navigation = new() { Spacing = 1 };
        Grid.SetColumn(navigation, 0);
        Overlay content = new();
        Grid.SetColumn(content, 1);
        root.Children.Add(navigation);
        root.Children.Add(content);

        for (int index = 0; index < 12; index++)
        {
            navigation.Children.Add(new Dock
            {
                BorderThickness = new Thickness(1),
                Children = { new ControlText($"Item {index}") },
            });
        }

        content.Children.Add(new ControlText("e\u0301 · 界 · 👩‍💻")
        {
            Wrapping = SharpVision.Text.Wrapping.Word,
        });
        return root;
    }
}
