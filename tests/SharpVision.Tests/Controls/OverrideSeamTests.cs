// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies the WPF-named layout override seams are the extension points.</summary>
public sealed class OverrideSeamTests
{
    /// <summary>Verifies a control's MeasureOverride result flows into DesiredSize.</summary>
    [Fact]
    public void MeasureOverride_WhenControlReportsContent_DrivesDesiredSize()
    {
        var control = new FixedContent();

        control.Measure(new Constraint(20, 6));

        control.DesiredSize.ShouldBe(new Size(7, 3));
    }

    /// <summary>Verifies OnRenderAdornment runs after a control's own OnRenderContent, giving a
    /// third party a seam to paint over its own subtree instead of only beneath it - the seam
    /// RenderOverlay already provides internally, now exposed protected (see #189).</summary>
    [Fact]
    public void OnRenderAdornment_WhenControlRenders_RunsAfterOnRenderContent()
    {
        var control = new ProbeControl(new Size(4, 1));
        var order = new List<string>();
        control.Rendering = _ => order.Add("content");
        control.RenderingAdornment = _ => order.Add("adornment");
        new LayoutEngine().Layout(control, new Size(4, 1));
        using Frame frame = new(new Size(4, 1));

        control.Render(frame.Canvas);

        order.ShouldBe(["content", "adornment"]);
        control.AdornmentRenderCalls.ShouldBe(1);
    }

    /// <summary>Verifies OnRenderAdornment runs after a container's owned children render, so an
    /// adornment can paint over content the subtree already committed (see #189).</summary>
    [Fact]
    public void OnRenderAdornment_WhenContainerHasChildren_RunsAfterChildRendering()
    {
        var parent = new ProbeContainer();
        var child = new ProbeControl(new Size(2, 1));
        parent.Children.Add(child);
        var order = new List<string>();
        child.Rendering = _ => order.Add("child");
        parent.RenderingAdornment = _ => order.Add("adornment");
        new LayoutEngine().Layout(parent, new Size(4, 1));
        using Frame frame = new(new Size(4, 1));

        parent.Render(frame.Canvas);

        order.ShouldBe(["child", "adornment"]);
    }

    /// <summary>Verifies lifecycle hooks observe the already-committed attachment state.</summary>
    [Fact]
    public async Task Lifecycle_WhenRootAttachesAndDetaches_PublishesCommittedStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var control = new ProbeControl();

            control.Attach(dispatcher);
            control.Detach();

            control.AttachedCalls.ShouldBe(1);
            control.AttachedStateWasCommitted.ShouldBeTrue();
            control.DetachedCalls.ShouldBe(1);
            control.DetachedStateWasCommitted.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a throwing disposal hook cannot prevent terminal cleanup.</summary>
    [Fact]
    public void Dispose_WhenDisposingHookThrows_CompletesCleanupAndRunsHookOnce()
    {
        var control = new ProbeControl { ThrowOnDisposing = true };

        _ = Should.Throw<InvalidOperationException>(control.Dispose);
        control.Dispose();

        control.IsDisposed.ShouldBeTrue();
        control.DisposingCalls.ShouldBe(1);
    }

    /// <summary>Verifies property-kernel arguments are rejected before backing state changes.</summary>
    [Fact]
    public void SetProperty_WhenArgumentsAreInvalid_RejectsBeforeMutation()
    {
        var control = new ProbeControl();
        var notifications = 0;
        control.PropertyChanged += (_, _) => notifications++;

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            control.SetKernelValue(1, (InvalidationImpact) 99));
        _ = Should.Throw<ArgumentNullException>(() =>
            control.SetKernelValue(2, InvalidationImpact.Render, null));

        control.KernelValue.ShouldBe(0);
        notifications.ShouldBe(0);
    }

    /// <summary>Verifies notification and invalidation seams reject unknown impacts.</summary>
    [Fact]
    public void InvalidationKernel_WhenImpactIsUnknown_RejectsWithoutNotification()
    {
        var control = new ProbeControl();
        var notifications = 0;
        control.PropertyChanged += (_, _) => notifications++;

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            control.NotifyKernelProperty(nameof(ProbeControl.KernelValue), (InvalidationImpact) 99));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            control.InvalidateKernel((InvalidationImpact) 99));

        notifications.ShouldBe(0);
    }

    /// <summary>Verifies child layout seams accept only direct ownership and defined axis flags.</summary>
    [Fact]
    public void ChildLayout_WhenCandidateIsNotDirectOrAxesAreUnknown_RejectsBeforeTransaction()
    {
        var owner = new ProbeContainer();
        var child = new ProbeControl(new Size(3, 2));
        var foreign = new ProbeControl(new Size(5, 4));
        owner.Children.Add(child);

        owner.MeasureOwned(child, new Constraint(10, 5)).ShouldBe(new Size(3, 2));
        _ = Should.Throw<ArgumentNullException>(() =>
            owner.MeasureOwned(null!, new Constraint(10, 5)));
        _ = Should.Throw<ArgumentException>(() =>
            owner.MeasureOwned(foreign, new Constraint(10, 5)));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            owner.ArrangeOwned(foreign, new Rect(0, 0, 5, 4), (ResolvedAxes) 8));
        _ = Should.Throw<ArgumentException>(() =>
            owner.ArrangeOwned(foreign, new Rect(0, 0, 5, 4), ResolvedAxes.Both));

        owner.ArrangeOwned(child, new Rect(1, 1, 3, 2), ResolvedAxes.Both);
        child.Bounds.ShouldBe(new Rect(1, 1, 3, 2));
        foreign.DesiredSize.ShouldBe(default);
        foreign.Bounds.ShouldBe(default);
    }
}
