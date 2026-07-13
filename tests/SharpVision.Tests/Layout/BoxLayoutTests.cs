// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Layout;

using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Tests.Support;
using SharpVision.Threading;

using Shouldly;

/// <summary>Verifies deterministic border-box measure and arrange behavior.</summary>
public sealed class BoxLayoutTests
{
    /// <summary>Verifies percentages use the slot while margin remains external.</summary>
    [Fact]
    public void Layout_WhenLengthIsPercent_ResolvesBorderBoxAndContent()
    {
        ProbeControl control = new ProbeControl(new Size(7, 3))
        {
            Width = Length.Percent(50),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(1),
            Padding = new Thickness(1),
        };

        new Engine().Layout(control, new Size(20, 10));

        control.DesiredSize.ShouldBe(new Size(10, 5));
        control.Bounds.ShouldBe(new Rect(1, 1, 10, 5));
        control.MeasureConstraints.ShouldBe([new Constraint(8, 6)]);
        control.ArrangeBounds.ShouldBe([new Rect(2, 2, 8, 3)]);
    }

    /// <summary>Verifies fixed and star lengths resolve independently by axis.</summary>
    [Fact]
    public void Layout_WhenLengthsAreFixedAndStar_UsesRequestedAndRemainingSlot()
    {
        ProbeControl control = new ProbeControl(new Size(1, 1))
        {
            Width = Length.Cells(6),
            Height = Length.Star(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(1),
        };

        new Engine().Layout(control, new Size(20, 10));

        control.DesiredSize.ShouldBe(new Size(6, 8));
        control.Bounds.ShouldBe(new Rect(1, 1, 6, 8));
    }

    /// <summary>Verifies deferred lengths remain intrinsic on unbounded axes.</summary>
    [Fact]
    public void Measure_WhenPercentAndStarAreUnbounded_UsesIntrinsicContent()
    {
        ProbeControl control = new ProbeControl(new Size(7, 3))
        {
            Width = Length.Percent(50),
            Height = Length.Star(1),
            Padding = new Thickness(1),
        };

        control.Measure(new Constraint(null, null));

        control.DesiredSize.ShouldBe(new Size(9, 5));
        control.MeasureConstraints.ShouldBe([new Constraint(null, null)]);
    }

    /// <summary>Verifies minimums, maximums, and tiny slots never create negative boxes.</summary>
    [Fact]
    public void Layout_WhenConstraintsAndEdgesExceedSlot_SaturatesInsideAvailableArea()
    {
        ProbeControl control = new ProbeControl(new Size(50, 50))
        {
            MinWidth = 8,
            MaxWidth = 12,
            MinHeight = 7,
            MaxHeight = 11,
            Margin = new Thickness(3),
            Padding = new Thickness(4),
        };

        new Engine().Layout(control, new Size(5, 4));

        control.Bounds.ShouldBe(new Rect(3, 3, 0, 0));
        control.ArrangeBounds.ShouldBe([new Rect(3, 3, 0, 0)]);
    }

    /// <summary>Verifies every non-stretch alignment uses deterministic integer edges.</summary>
    /// <param name="horizontal">The horizontal placement.</param>
    /// <param name="vertical">The vertical placement.</param>
    /// <param name="x">The expected x coordinate.</param>
    /// <param name="y">The expected y coordinate.</param>
    [Theory]
    [InlineData(HorizontalAlignment.Left, VerticalAlignment.Top, 0, 0)]
    [InlineData(HorizontalAlignment.Center, VerticalAlignment.Center, 3, 2)]
    [InlineData(HorizontalAlignment.Right, VerticalAlignment.Bottom, 7, 5)]
    public void Layout_WhenAligned_PlacesAtExpectedEdge(
        HorizontalAlignment horizontal,
        VerticalAlignment vertical,
        int x,
        int y)
    {
        ProbeControl control = new ProbeControl(new Size(3, 2))
        {
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical,
        };

        new Engine().Layout(control, new Size(10, 7));

        control.Bounds.ShouldBe(new Rect(x, y, 3, 2));
    }

    /// <summary>Verifies stretch fills only automatic dimensions.</summary>
    [Fact]
    public void Layout_WhenStretchHasExplicitWidth_PreservesExplicitWidth()
    {
        ProbeControl control = new ProbeControl(new Size(2, 2))
        {
            Width = Length.Cells(4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        new Engine().Layout(control, new Size(10, 6));

        control.Bounds.ShouldBe(new Rect(0, 0, 4, 6));
    }

    /// <summary>Verifies collapsed controls bypass extension points and clear geometry.</summary>
    [Fact]
    public void Layout_WhenCollapsed_ClearsGeometryWithoutCallingCore()
    {
        ProbeControl control = new ProbeControl(new Size(4, 2));
        Engine engine = new Engine();
        engine.Layout(control, new Size(10, 6));
        control.Visibility = Visibility.Collapsed;

        engine.Layout(control, new Size(10, 6));

        control.DesiredSize.ShouldBe(default);
        control.Bounds.ShouldBe(default);
        control.MeasureConstraints.Count.ShouldBe(1);
        control.ArrangeBounds.Count.ShouldBe(1);
    }

    /// <summary>Verifies unchanged layout is cached while resize remeasures.</summary>
    [Fact]
    public void Layout_WhenConstraintIsUnchanged_CachesUntilResize()
    {
        ProbeControl control = new ProbeControl(new Size(4, 2));
        Engine engine = new Engine();

        engine.Layout(control, new Size(10, 6));
        engine.Layout(control, new Size(10, 6));
        engine.Layout(control, new Size(11, 6));

        control.MeasureConstraints.Count.ShouldBe(2);
        control.ArrangeBounds.Count.ShouldBe(2);
    }

    /// <summary>Verifies invalidation raised during a phase remains pending.</summary>
    [Fact]
    public void Layout_WhenCoreInvalidates_DoesNotReenterAndLeavesLaterPassPending()
    {
        ProbeControl control = new ProbeControl(new Size(4, 2))
        {
            Measuring = current => current.Width = Length.Cells(5),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Arranging = current => current.HorizontalAlignment = HorizontalAlignment.Left,
        };

        new Engine().Layout(control, new Size(10, 6));

        control.MeasureConstraints.Count.ShouldBe(1);
        control.ArrangeBounds.Count.ShouldBe(1);
        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies nested layout is rejected without corrupting the outer transaction.</summary>
    [Fact]
    public void Layout_WhenReentered_ThrowsInvalidOperationException()
    {
        ProbeControl control = new ProbeControl(new Size(4, 2));
        Engine engine = new Engine();
        control.Measuring = current => engine.Layout(current, new Size(5, 5));

        _ = Should.Throw<InvalidOperationException>(() =>
            engine.Layout(control, new Size(10, 6)));
    }

    /// <summary>Verifies a same-constraint recursive measure cannot hide behind the cache.</summary>
    [Fact]
    public void Measure_WhenReenteredAfterCachedPass_ThrowsInvalidOperationException()
    {
        ProbeControl control = new ProbeControl(new Size(4, 2));
        Engine engine = new Engine();
        Constraint constraint = new Constraint(10, 6);
        engine.Layout(control, new Size(10, 6));
        control.Width = Length.Cells(5);
        control.Measuring = current => current.Measure(constraint);

        _ = Should.Throw<InvalidOperationException>(() =>
            engine.Layout(control, new Size(10, 6)));

        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies attached layout is dispatcher-affine.</summary>
    [Fact]
    public async Task Layout_WhenAttachedOffThread_ThrowsBeforeCoreAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();
        ProbeControl control = new ProbeControl(new Size(4, 2));
        await dispatcher.InvokeAsync(
            () => control.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() =>
            new Engine().Layout(control, new Size(10, 6)));

        control.MeasureConstraints.ShouldBeEmpty();
    }

    /// <summary>Verifies fixed-seed valid inputs always remain deterministic and contained.</summary>
    [Fact]
    public void Layout_WhenInputsAreRandomized_ProducesContainedDeterministicBounds()
    {
        const int caseCount = 10_000;
        List<(Rect Bounds, Rect Available)> first = GenerateBounds(seed: 0x5A17, caseCount);
        List<(Rect Bounds, Rect Available)> second = GenerateBounds(seed: 0x5A17, caseCount);

        second.ShouldBe(first);

        for (var index = 0; index < caseCount; index++)
        {
            (Rect bounds, Rect available) = first[index];
            bounds.Width.ShouldBeGreaterThanOrEqualTo(0);
            bounds.Height.ShouldBeGreaterThanOrEqualTo(0);
            bounds.X.ShouldBeGreaterThanOrEqualTo(available.X);
            bounds.Y.ShouldBeGreaterThanOrEqualTo(available.Y);
            bounds.Right.ShouldBeLessThanOrEqualTo(available.Right);
            bounds.Bottom.ShouldBeLessThanOrEqualTo(available.Bottom);
        }
    }

    private static List<(Rect Bounds, Rect Available)> GenerateBounds(int seed, int count)
    {
        Random random = new Random(seed);
        List<(Rect Bounds, Rect Available)> result = new List<(Rect Bounds, Rect Available)>(count);
        Engine engine = new Engine();

        for (var index = 0; index < count; index++)
        {
            Size size = new Size(random.Next(0, 121), random.Next(0, 61));
            Thickness margin = new Thickness(
                random.Next(0, 8),
                random.Next(0, 5),
                random.Next(0, 8),
                random.Next(0, 5));
            ProbeControl control = new ProbeControl(new Size(random.Next(0, 30), random.Next(0, 15)))
            {
                Width = NextLength(random),
                Height = NextLength(random),
                Margin = margin,
                Padding = new Thickness(random.Next(0, 5)),
                HorizontalAlignment = (HorizontalAlignment) random.Next(0, 4),
                VerticalAlignment = (VerticalAlignment) random.Next(0, 4),
            };

            engine.Layout(control, size);
            result.Add((control.Bounds, margin.Deflate(new Rect(0, 0, size.Width, size.Height))));
        }

        return result;
    }

    private static Length NextLength(Random random) => random.Next(0, 4) switch
    {
        0 => Length.Auto,
        1 => Length.Cells(random.Next(0, 80)),
        2 => Length.Percent(random.NextDouble() * 100),
        _ => Length.Star(random.NextDouble() + 0.01),
    };
}
