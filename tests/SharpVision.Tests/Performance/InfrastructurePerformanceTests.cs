// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

using System.Diagnostics;
using System.Runtime.InteropServices;

using SharpVision.Terminal.Input;


using KeyAction = Terminal.Input.Action;

/// <summary>Gates deterministic UI infrastructure allocations and records timings.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class InfrastructurePerformanceTests
{
    /// <summary>Verifies unchanged box layout allocates no managed memory after warmup.</summary>
    [Fact]
    public void Layout_WhenConstraintAndStateAreUnchanged_AllocatesZeroBytes()
    {
        Engine engine = new();
        ProbeControl control = new(new Size(20, 4));
        Size size = new(80, 24);
        engine.Layout(control, size);

        (long allocated, TimeSpan elapsed) = Measure(() => engine.Layout(control, size), 10_000);

        allocated.ShouldBe(0);
        Report("unchanged layout", elapsed, 50_000);
    }

    /// <summary>Verifies warmed 80×24 semantic control rendering allocates nothing.</summary>
    [Fact]
    public void Render_WhenFrameAndTreeAreReused_AllocatesZeroBytes()
    {
        ProbeControl control = new()
        {
            Bounds = new Rect(0, 0, 80, 24),
            Content = "e\u0301 · 界 · 👩‍💻".AsMemory(),
        };
        using Frame frame = new(new Size(80, 24));
        Render();

        (long allocated, TimeSpan elapsed) = Measure(Render, 1_000);

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
        ProbeContainer root = new();
        ProbeContainer current = root;

        for (int depth = 1; depth < 20; depth++)
        {
            ProbeContainer child = new();
            current.Children.Add(child);
            current = child;
        }

        ProbeControl target = new();
        current.Children.Add(target);
        _ = target.AddHandler(Events.Key, static (_, _) => { });
        KeyEventArgs eventArgs = new(new Stroke(
            Code.Enter,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press));
        Router.Route(target, Events.Key, eventArgs);

        (long allocated, TimeSpan elapsed) = Measure(
            () => Router.Route(target, Events.Key, eventArgs),
            10_000);

        allocated.ShouldBe(0);
        Report("depth-20 routed event", elapsed, 50_000);
    }

    /// <summary>Verifies bounded dispatcher posting stays within its object budget.</summary>
    [Fact]
    public async Task Post_WhenDispatcherIsWarm_StaysWithinAllocationBudgetAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();
        TaskCompletionSource warmed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Post(warmed.SetResult);
        await warmed.Task.WaitAsync(TestContext.Current.CancellationToken);
        const int iterations = 1_000;
        Stopwatch watch = Stopwatch.StartNew();
        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int index = 0; index < iterations; index++)
        {
            dispatcher.Post(static () => { });
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Post(completed.SetResult);
        await completed.Task.WaitAsync(TestContext.Current.CancellationToken);
        watch.Stop();

        allocated.ShouldBeLessThanOrEqualTo(256L * iterations);
        Report("dispatcher post/drain", watch.Elapsed, iterations);
    }

    private static (long Allocated, TimeSpan Elapsed) Measure(System.Action action, int iterations)
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
