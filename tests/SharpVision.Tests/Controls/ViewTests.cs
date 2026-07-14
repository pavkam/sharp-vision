// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Threading;

/// <summary>Verifies View builds its content once, on its first measure, whether attached or not.</summary>
public sealed class ViewTests
{
    /// <summary>Verifies Build runs once on the first measure after attach and installs its result.</summary>
    [Fact]
    public async Task Build_WhenViewMeasuredAfterAttach_RunsOnceAndInstallsContentAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            ProbeControl content = new() { Width = Length.Cells(5), Height = Length.Cells(2) };
            CountingView view = new(content);
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
        ProbeControl content = new() { Width = Length.Cells(5), Height = Length.Cells(2) };
        CountingView view = new(content);

        view.Measure(new Constraint(20, 6));

        view.BuildCount.ShouldBe(1);
        view.Installed.ShouldBeSameAs(content);
    }

    /// <summary>Verifies a view measured once while detached is not rebuilt by a later measure after
    /// attach, even when that measure uses the same constraint as the detached measure.</summary>
    [Fact]
    public async Task Build_WhenMeasuredDetachedThenAttachedAndRemeasured_BuildsOnlyOnceAsync()
    {
        ProbeControl content = new() { Width = Length.Cells(5), Height = Length.Cells(2) };
        CountingView view = new(content);

        view.Measure(new Constraint(20, 6));

        view.BuildCount.ShouldBe(1);
        view.Installed.ShouldBeSameAs(content);

        await using Dispatcher dispatcher = Dispatcher.Start();

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
        NullView view = new();

        _ = Should.Throw<InvalidOperationException>(() => view.Measure(new Constraint(20, 6)));
    }
}
