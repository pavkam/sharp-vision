// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

/// <summary>Verifies warmed write-scoped foreground drawing allocates no managed memory per render.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class TerminalCanvasPerformanceTests
{
    private static readonly Action<TerminalCanvas> _allocationDraw = DrawAllocationCell;
    private static readonly Func<Point, Color> _allocationSelector = SelectAllocationForeground;

    /// <summary>Verifies warmed write-scoped foreground drawing allocates no managed memory per render.</summary>
    [Fact]
    public void DrawWithForeground_WhenCallbacksAreCached_AllocatesNoManagedMemory()
    {
        using Frame frame = new(new Size(1, 1));
        var canvas = frame.Canvas;

        for (var index = 0; index < 32; index++)
        {
            Render();
        }

        var minimum = long.MaxValue;

        for (var sample = 0; sample < 5; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var index = 0; index < 128; index++)
            {
                Render();
            }

            minimum = Math.Min(
                minimum,
                GC.GetAllocatedBytesForCurrentThread() - before);
        }

        minimum.ShouldBe(0);
        return;

        void Render()
        {
            frame.Clear();
            canvas.DrawWithForeground(canvas.Bounds, _allocationDraw, _allocationSelector);
        }
    }

    private static void DrawAllocationCell(TerminalCanvas canvas) =>
        canvas.DrawRune(new Rune('A'), default);

    private static Color SelectAllocationForeground(Point _) => ReferenceColors.Get(7);
}
