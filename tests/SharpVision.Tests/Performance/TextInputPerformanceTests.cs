// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

using SharpVision.Terminal.Input;

/// <summary>Gates the cost of TextInput's Left-arrow caret navigation against document size.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class TextInputPerformanceTests
{
    /// <summary>Verifies repeated Left navigation reuses the same cached boundary array instead of
    /// rebuilding it on every keystroke. MoveCaretPrevious's cached binary-search boundary lookup,
    /// and EnsureCaretVisible's matching cached position lookup, replaced
    /// Edit.MovePreviousUnchecked's and Position's O(n) full-prefix rescans that ran on every
    /// keystroke - a deterministic array-identity check, not a wall-clock threshold, since the
    /// issue's own guidance is to avoid noisy ordinary-CI timing gates (see #42).</summary>
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

    /// <summary>Verifies walking an entire large document backward one grapheme at a time completes
    /// under a generous absolute budget. The old O(n) per-keystroke rescans measured 21.43s for
    /// 8,000 graphemes per the issue's own evidence table; this budget stays below that for a
    /// document of the same size, without pinning to a precise ratio that measurement noise at
    /// small scales made unreliable to calibrate across CI hardware (see #42).</summary>
    [Fact]
    public void Left_WhenWalkingBackwardThroughALargeDocument_CompletesWellUnderTheOldMeasuredCost()
    {
        var control = new TextInput { Text = new string('a', 8_000) };
        Key(control, Code.End, Modifiers.None);
        var watch = Stopwatch.StartNew();

        for (var index = 0; index < 8_000; index++)
        {
            Key(control, Code.Left, Modifiers.None);
        }

        watch.Stop();
        watch.Elapsed.TotalSeconds.ShouldBeLessThan(18);
    }

    private static void Key(TextInput control, Code code, Modifiers modifiers) =>
        Router.Route(
            control,
            Events.Key,
            new KeyEventArgs(new Stroke(code, character: null, nativeCode: 0, modifiers, KeyAction.Press)));
}
