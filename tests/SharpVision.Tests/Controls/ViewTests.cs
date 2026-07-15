// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;


/// <summary>Verifies View builds its content once, on its first measure, whether attached or not.</summary>
public sealed class ViewTests
{
    /// <summary>Verifies Build runs once on the first measure after attach and installs its result.</summary>
    [Fact]
    public async Task Build_WhenViewMeasuredAfterAttach_RunsOnceAndInstallsContentAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var content = new ProbeControl() { Width = Length.Cells(5), Height = Length.Cells(2) };
            var view = new CountingView(content);
            view.Attach(dispatcher);

            view.Measure(new Constraint(20, 6));
            view.Measure(new Constraint(20, 6));

            view.BuildCount.ShouldBe(1);
            view.Installed.ShouldBeSameAs(content);
            view.DesiredSize.ShouldBe(new Size(5, 2));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a detached view builds on its first measure, with no attachment required.</summary>
    [Fact]
    public void Build_WhenMeasuredWhileDetached_BuildsOnFirstMeasure()
    {
        var content = new ProbeControl() { Width = Length.Cells(5), Height = Length.Cells(2) };
        var view = new CountingView(content);

        view.Measure(new Constraint(20, 6));

        view.BuildCount.ShouldBe(1);
        view.Installed.ShouldBeSameAs(content);
    }

    /// <summary>Verifies a view measured once while detached is not rebuilt by a later measure after
    /// attach, even when that measure uses the same constraint as the detached measure.</summary>
    [Fact]
    public async Task Build_WhenMeasuredDetachedThenAttachedAndRemeasured_BuildsOnlyOnceAsync()
    {
        var content = new ProbeControl() { Width = Length.Cells(5), Height = Length.Cells(2) };
        var view = new CountingView(content);

        view.Measure(new Constraint(20, 6));

        view.BuildCount.ShouldBe(1);
        view.Installed.ShouldBeSameAs(content);

        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            view.Attach(dispatcher);
            view.Measure(new Constraint(20, 6));

            view.BuildCount.ShouldBe(1);
            view.Installed.ShouldBeSameAs(content);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a null Build result is rejected.</summary>
    [Fact]
    public void Build_WhenResultIsNull_ThrowsInvalidOperation()
    {
        var view = new NullView();

        _ = Should.Throw<InvalidOperationException>(() => view.Measure(new Constraint(20, 6)));
    }

    /// <summary>Verifies the view's desired size equals the built child's desired size plus its
    /// margin, and that arranging the view into a box larger than the child's desired size
    /// stretches the child to fill the view's content rectangle.</summary>
    [Fact]
    public void Measure_ThenArrangeLargerThanChild_ChildFillsContentBounds()
    {
        var content = new ProbeControl(new Size(5, 2));
        var view = new CountingView(content)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        view.Measure(new Constraint(20, 6));

        view.DesiredSize.ShouldBe(new Size(5, 2));

        view.Arrange(new Rect(0, 0, 20, 6));

        content.Bounds.ShouldBe(new Rect(0, 0, 20, 6));
    }

    /// <summary>Verifies an exception from Build propagates out of Measure and leaves the view
    /// measure-dirty, so a subsequent measure re-attempts Build rather than caching a result.</summary>
    [Fact]
    public void Measure_WhenBuildThrows_PropagatesAndLeavesMeasureInvalidated()
    {
        var view = new ThrowingView();

        _ = Should.Throw<InvalidOperationException>(() => view.Measure(new Constraint(20, 6)));
        _ = Should.Throw<InvalidOperationException>(() => view.Measure(new Constraint(20, 6)));
    }
}
