// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

/// <summary>Gates Router.Route allocation reuse for deep ancestries and repeated nested scroll dispatch.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class RouterPerformanceTests
{
    /// <summary>Verifies warmed depth-20 routed dispatch allocates no managed memory.</summary>
    [Fact]
    public void Route_WhenAncestryIsDeepAndStable_AllocatesZeroBytes()
    {
        var root = new ProbeContainer();
        var current = root;

        for (var depth = 1; depth < 20; depth++)
        {
            var child = new ProbeContainer();
            current.Children.Add(child);
            current = child;
        }

        var target = new ProbeControl();
        current.Children.Add(target);
        _ = target.AddHandler(Events.Key, static (_, _) => { });
        var eventArgs = new KeyEventArgs(new Stroke(
            Code.Enter,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press));
        _ = Router.Route(target, Events.Key, eventArgs);

        var (allocated, elapsed) = Measure(
            () => Router.Route(target, Events.Key, eventArgs),
            10_000);

        allocated.ShouldBe(0);
        Report("depth-20 routed event", elapsed, 50_000);
    }

    /// <summary>Verifies repeated nested wheel routing stays within a finite command budget.</summary>
    [Fact]
    public void Dispatch_WhenNestedScrollCommandsRepeat_HasBoundedManagedAllocation()
    {
        var leaf = new ControlText(string.Join('\n', Enumerable.Range(0, 20))) { Width = Length.Cells(5) };
        var inner = Hidden(leaf);
        inner.Width = Length.Cells(5);
        inner.Height = Length.Cells(8);
        var outer = Hidden(inner);
        new LayoutEngine().Layout(outer, new Size(5, 4));
        var down = new PointerEventArgs(Pointer(default, PointerAction.Wheel, wheelY: -20));
        var up = new PointerEventArgs(Pointer(default, PointerAction.Wheel, wheelY: 20));
        var descending = true;
        Dispatch();

        var allocated = Minimum(Dispatch, iterations: 1_000, out var elapsed);

        allocated.ShouldBeLessThanOrEqualTo(256L * 1_000);
        Report("1,000 nested scroll commands", elapsed, 1_000);
        return;

        void Dispatch()
        {
            _ = Router.Route(leaf, Events.Pointer, descending ? down : up);
            descending = !descending;
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

    private static Stack Hidden(ControlBase content) => new()
    {
        AutoScroll = true,
        ScrollBars = ScrollBars.Both,
        ShowScrollBars = ShowScrollBars.Never,
        Children = { content }
    };

    private static Pointer Pointer(Point cells, PointerAction action, int wheelY = 0) => new(
        cells,
        pixels: null,
        Buttons.Primary,
        action,
        wheelX: 0,
        wheelY,
        Modifiers.None,
        isMotion: action == PointerAction.Move,
        isCellPositionInferred: false);

    private static void Report(string scenario, TimeSpan elapsed, int iterations)
    {
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{scenario}: {iterations} measured iterations in {elapsed.TotalMilliseconds:F3} ms; " +
            $"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.ProcessArchitecture}; " +
            RuntimeInformation.OSDescription);
    }
}
