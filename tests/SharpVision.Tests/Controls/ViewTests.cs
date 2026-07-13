// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Threading;

/// <summary>Verifies View builds its content once, after attach, before first layout use.</summary>
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

    /// <summary>Verifies a detached, unmeasured view never builds.</summary>
    [Fact]
    public void Build_WhenViewIsDetached_IsNotCalled()
    {
        ProbeControl content = new();
        CountingView view = new(content);

        view.Measure(new Constraint(20, 6));

        view.BuildCount.ShouldBe(0);
        view.Installed.ShouldBeNull();
    }

    /// <summary>Verifies a view measured once while detached still builds on the first measure
    /// after attach, even when that measure uses the same constraint as the detached measure.</summary>
    [Fact]
    public async Task Build_WhenMeasuredDetachedThenAttachedAndRemeasured_BuildsOnNextMeasureAsync()
    {
        ProbeControl content = new() { Width = Length.Cells(5), Height = Length.Cells(2) };
        CountingView view = new(content);

        view.Measure(new Constraint(20, 6));

        view.BuildCount.ShouldBe(0);
        view.Installed.ShouldBeNull();

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
    public async Task Build_WhenResultIsNull_ThrowsInvalidOperationAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            NullView view = new();
            view.Attach(dispatcher);

            _ = Should.Throw<InvalidOperationException>(() => view.Measure(new Constraint(20, 6)));
        }, TestContext.Current.CancellationToken);
    }

    private sealed class CountingView: View
    {
        private readonly Control _content;

        internal CountingView(Control content) => _content = content;

        internal int BuildCount { get; private set; }

        internal Control? Installed => Content;

        protected override Control Build()
        {
            BuildCount++;
            return _content;
        }
    }

    private sealed class NullView: View
    {
        protected override Control Build() => null!;
    }
}
