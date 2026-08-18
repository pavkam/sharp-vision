// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

/// <summary>Gates captured ScrollBar thumb-drag allocation cost.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class ScrollBarPerformanceTests
{
    /// <summary>Verifies captured ScrollBar motion remains within a finite routed-event budget.</summary>
    [Fact]
    public async Task Dispatch_WhenThumbIsDragged_HasBoundedManagedAllocationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var bar = new ScrollBar
            {
                Bounds = new Rect(0, 0, 102, 1),
                Orientation = Orientation.Horizontal,
                Maximum = 10_000
            };
            bar.Attach(dispatcher);
            using PointerManager capture = new(bar);
            _ = capture.Dispatch(Pointer(new Point(1, 0), PointerAction.Press));
            var position = 2;
            Drag();

            var allocated = Minimum(Drag, iterations: 1_000, out var elapsed);

            allocated.ShouldBeLessThanOrEqualTo(768L * 1_000);
            bar.Value.ShouldBeInRange(0, bar.Maximum);
            _ = capture.Dispatch(Pointer(new Point(position, 0), PointerAction.Release));
            Report("1,000 captured thumb moves", elapsed, 1_000);
            return;

            void Drag()
            {
                position = position == 100 ? 2 : position + 1;
                _ = capture.Dispatch(Pointer(new Point(position, 0), PointerAction.Move));
            }
        }, TestContext.Current.CancellationToken);
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
            $"{scenario}: {iterations} iterations in {elapsed.TotalMilliseconds:F3} ms; " +
            $"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.ProcessArchitecture}; " +
            RuntimeInformation.OSDescription);
    }
}
