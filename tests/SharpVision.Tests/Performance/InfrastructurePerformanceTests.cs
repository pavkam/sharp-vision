namespace SharpVision.Tests.Performance;

using System.Diagnostics;
using System.Runtime.InteropServices;

using SharpVision.Controls;
using SharpVision.Input;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;
using SharpVision.Terminal.Rendering;
using SharpVision.Tests.Support;
using SharpVision.Threading;

using Shouldly;

using KeyAction = Terminal.Input.Action;

/// <summary>Gates deterministic UI infrastructure allocations and records timings.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class InfrastructurePerformanceTests
{
    /// <summary>Verifies unchanged box layout allocates no managed memory after warmup.</summary>
    [Fact]
    public void Layout_WhenConstraintAndStateAreUnchanged_AllocatesZeroBytes()
    {
        var engine = new Engine();
        var control = new ProbeControl(new Size(20, 4));
        var size = new Size(80, 24);
        engine.Layout(control, size);

        var (allocated, elapsed) = Measure(() => engine.Layout(control, size), 10_000);

        allocated.ShouldBe(0);
        Report("unchanged layout", elapsed, 50_000);
    }

    /// <summary>Verifies warmed 80×24 semantic control rendering allocates nothing.</summary>
    [Fact]
    public void Render_WhenFrameAndTreeAreReused_AllocatesZeroBytes()
    {
        var control = new ProbeControl
        {
            Bounds = new Rect(0, 0, 80, 24),
            Content = "e\u0301 · 界 · 👩‍💻".AsMemory(),
        };
        using var frame = new Frame(new Size(80, 24));
        Render();

        var (allocated, elapsed) = Measure(Render, 1_000);

        allocated.ShouldBe(0);
        Report("80x24 control rendering", elapsed, 5_000);

        void Render()
        {
            frame.Clear();
            control.Invalidate(Invalidation.Render);
            control.Render(frame.Canvas);
        }
    }

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
        Router.Route(target, Events.Key, eventArgs);

        var (allocated, elapsed) = Measure(
            () => Router.Route(target, Events.Key, eventArgs),
            10_000);

        allocated.ShouldBe(0);
        Report("depth-20 routed event", elapsed, 50_000);
    }

    /// <summary>Verifies bounded dispatcher posting stays within its object budget.</summary>
    [Fact]
    public async Task Post_WhenDispatcherIsWarm_StaysWithinAllocationBudgetAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var warmed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Post(warmed.SetResult);
        await warmed.Task.WaitAsync(TestContext.Current.CancellationToken);
        const int iterations = 1_000;
        var watch = Stopwatch.StartNew();
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < iterations; index++)
        {
            dispatcher.Post(static () => { });
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Post(completed.SetResult);
        await completed.Task.WaitAsync(TestContext.Current.CancellationToken);
        watch.Stop();

        allocated.ShouldBeLessThanOrEqualTo(256L * iterations);
        Report("dispatcher post/drain", watch.Elapsed, iterations);
    }

    private static (long Allocated, TimeSpan Elapsed) Measure(System.Action action, int iterations)
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
