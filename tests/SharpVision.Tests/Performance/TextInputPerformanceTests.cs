// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

using SharpVision.Terminal.Input;

/// <summary>Gates the cost of TextInput's caret navigation against document size using deterministic
/// cache-identity checks and binary-search iteration counts instead of a wall-clock timing budget,
/// which measures the host machine as much as the product and is inherently flaky under CI load or
/// coverage instrumentation.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class TextInputPerformanceTests
{
    /// <summary>Verifies repeated Left navigation reuses the same cached boundary array instead of
    /// rebuilding it on every keystroke. MoveCaretPrevious's cached binary-search boundary lookup,
    /// and EnsureCaretVisible's matching cached position lookup, replaced
    /// Edit.MovePreviousUnchecked's and Position's O(n) full-prefix rescans that ran on every
    /// keystroke - a deterministic array-identity check, not a wall-clock threshold, since the
    /// aim is to avoid noisy ordinary-CI timing gates.</summary>
    [Fact]
    public void Left_WhenPressedRepeatedly_ReusesTheSameCachedBoundaryArrayInstead()
    {
        var control = new TextInput { Text = new string('a', 5_000) };
        Key(control, Code.End, Modifiers.None);
        Key(control, Code.Left, Modifiers.None);
        var cache = control.BoundaryOffsets.ShouldNotBeNull();

        for (var index = 0; index < 4_999; index++)
        {
            Key(control, Code.Left, Modifiers.None);
        }

        control.CaretIndex.ShouldBe(0);
        ReferenceEquals(control.BoundaryOffsets, cache).ShouldBeTrue();
    }

    /// <summary>Verifies walking a large non-word-wrap multi-row document upward reuses the same
    /// cached boundary array across every keystroke, exactly as horizontal navigation does. Each Up
    /// press previously scanned every row from the document start to find the target row's column -
    /// the same O(document)-per-navigation-event cost the boundary cache eliminated for horizontal
    /// navigation, summed over every row crossed while holding the key. Repeats the press
    /// only enough times to prove reuse across calls, not all the way to the document start - the
    /// cache-identity property does not depend on how many rows remain, and every real keystroke
    /// still pays ordinary routing and invalidation overhead unrelated to this specific fix, which a
    /// full 8,000-press walk would needlessly make this test minutes slower to run for no additional
    /// coverage.</summary>
    [Fact]
    public void Up_WhenWalkingUpwardThroughALargeMultiRowDocument_ReusesTheSameCachedBoundaryArray()
    {
        var text = string.Concat(Enumerable.Repeat("x\n", 8_000)) + "x";
        var control = new TextInput { AcceptsReturn = true, Text = text };
        Key(control, Code.End, Modifiers.None);
        Key(control, Code.Up, Modifiers.None);
        var cache = control.BoundaryOffsets.ShouldNotBeNull();
        var rowAfterFirstPress = control.CaretIndex;

        for (var index = 0; index < 19; index++)
        {
            Key(control, Code.Up, Modifiers.None);
        }

        control.CaretIndex.ShouldBeLessThan(rowAfterFirstPress);
        ReferenceEquals(control.BoundaryOffsets, cache).ShouldBeTrue();
    }

    /// <summary>Verifies the row lookup <see cref="TextInput.LowerBoundByRow(int[], int, out int)"/>
    /// performs during every navigation event stays logarithmic in the cached row count instead of
    /// scanning linearly from the document start - the actual algorithmic property the boundary
    /// cache alone does not guarantee (a valid, reused cache could still be searched linearly). A
    /// document of 8,000 rows binary-searched linearly would take up to 8,000 comparisons; this
    /// asserts an order of magnitude fewer, comfortably inside <c>ceil(log2(8000)) + 1 = 14</c>.
    /// </summary>
    [Fact]
    public void LowerBoundByRow_WhenSearchingALargeRowArray_PerformsLogarithmicComparisons()
    {
        var rows = Enumerable.Range(0, 8_000).ToArray();

        var index = TextInput.LowerBoundByRow(rows, 7_999, out var iterations);

        index.ShouldBe(7_999);
        iterations.ShouldBeLessThanOrEqualTo(14);
    }

    // Word-wrap vertical navigation's VisualLineIndexAt binary search (replacing a reverse linear
    // scan) has no dedicated regression test here: proving its iteration count directly
    // would require either a wall-clock budget or a mutable instance counter on TextInput solely
    // for test observability, and neither belongs in this control. The fix's correctness is
    // covered by ordinary TextInputTests coverage; its complexity bound is documented on
    // VisualLineIndexAt itself and enforced by code review.

    /// <summary>Verifies repeated public TextInput replacement stays within a finite per-edit budget.</summary>
    [Fact]
    public void Text_WhenRepeatedlyEdited_HasBoundedManagedAllocation()
    {
        var input = new TextInput { UndoLimit = 32 };
        var toggle = false;
        Edit();

        var allocated = Minimum(Edit, iterations: 1_000, out var elapsed);

        allocated.ShouldBeLessThanOrEqualTo(1_024L * 1_000);
        input.CanUndo.ShouldBeTrue();
        Report("1,000 text replacements", elapsed, 1_000);
        return;

        void Edit()
        {
            input.Text = toggle ? "e\u0301" : "界";
            toggle = !toggle;
        }
    }

    /// <summary>Verifies a warm measure/arrange/render cycle allocates no managed memory when
    /// StartAffix and EndAffix are both null - MeasureAffixes, DeflateForAffixes, and RenderAffixes
    /// run unconditionally every ArrangeChrome/OnRenderContent pass, so any per-call allocation in
    /// the null path would show up here as steady-state growth instead of a one-time warmup cost.</summary>
    [Fact]
    public void Render_WhenBothAffixesAreNull_AllocatesNoManagedMemory()
    {
        var control = new TextInput { Text = "Hello", Width = Length.Cells(10), Height = Length.Cells(3) };
        var engine = new LayoutEngine();
        var size = new Size(10, 3);
        using Frame frame = new(size);

        Run();

        var allocated = Minimum(Run, iterations: 500, out var elapsed);

        allocated.ShouldBe(0);
        control.StartAffix.ShouldBeNull();
        control.EndAffix.ShouldBeNull();
        Report("500 TextInput measure/arrange/render cycles with no affixes", elapsed, 500);
        return;

        void Run()
        {
            engine.Layout(control, size);
            frame.Clear();
            control.Invalidate(Invalidation.Render);
            control.Render(frame.Canvas);
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

    private static void Report(string scenario, TimeSpan elapsed, int iterations)
    {
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{scenario}: {iterations} iterations in {elapsed.TotalMilliseconds:F3} ms; " +
            $"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.ProcessArchitecture}; " +
            RuntimeInformation.OSDescription);
    }

    private static void Key(TextInput control, Code code, Modifiers modifiers) =>
        Router.Route(
            control,
            Events.Key,
            new KeyEventArgs(new Stroke(code, character: null, nativeCode: 0, modifiers, KeyAction.Press)));
}
