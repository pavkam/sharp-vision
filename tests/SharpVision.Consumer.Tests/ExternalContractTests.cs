// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Consumer.Tests;

/// <summary>Verifies third-party controls need only the documented public and protected surface.</summary>
public sealed class ExternalContractTests
{
    /// <summary>Verifies protected property mutation publishes once and suppresses equivalent assignments.</summary>
    [Fact]
    public void Value_WhenChanged_PublishesOneObservableChange()
    {
        var gauge = new Gauge();
        var changed = new List<string?>();
        gauge.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        gauge.Value = 42;
        gauge.Value = 42;

        gauge.Value.ShouldBe(42);
        changed.ShouldBe([nameof(Gauge.Value)]);
    }

    /// <summary>Verifies an external container can measure and arrange its direct children through protected transactions.</summary>
    [Fact]
    public async Task Layout_WhenExternalContainerOwnsLeaves_MeasuresAndArrangesThroughKernelAsync()
    {
        await using var terminal = new ConsumerTerminal();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var first = new Gauge() { Value = 7 };
        var second = new Gauge() { Value = 100 };
        var panel = new FlowPanel();
        panel.Children.Add(first);
        panel.Children.Add(second);
        await using var application = new Application(
            panel,
            terminal,
            terminal,
            TerminalOptions.Minimal);

        await application.StartAsync(TestContext.Current.CancellationToken);

        first.LastMeasuredPolicy.ShouldBeSameAs(application.CellPolicy);
        second.LastMeasuredPolicy.ShouldBeSameAs(application.CellPolicy);
        first.Bounds.X.ShouldBe(0);
        second.Bounds.X.ShouldBe(first.Bounds.Right);
        first.Bounds.Height.ShouldBe(panel.Bounds.Height);
        second.Bounds.Height.ShouldBe(panel.Bounds.Height);
        first.RenderCount.ShouldBeGreaterThan(0);
        second.RenderCount.ShouldBeGreaterThan(0);

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies intrinsic border geometry applies to an externally authored container without custom plumbing.</summary>
    [Fact]
    public async Task Layout_WhenExternalContainerHasBorder_InsetsOwnedLeavesAsync()
    {
        await using var terminal = new ConsumerTerminal();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var first = new Gauge() { Value = 7 };
        var second = new Gauge() { Value = 100 };
        var panel = new FlowPanel()
        {
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        panel.Children.Add(first);
        panel.Children.Add(second);
        await using var application = new Application(
            panel,
            terminal,
            terminal,
            TerminalOptions.Minimal);

        await application.StartAsync(TestContext.Current.CancellationToken);

        first.Bounds.X.ShouldBe(panel.Bounds.X + 1);
        first.Bounds.Y.ShouldBe(panel.Bounds.Y + 1);
        second.Bounds.Right.ShouldBeLessThanOrEqualTo(panel.Bounds.Right - 1);
        second.Bounds.Bottom.ShouldBeLessThanOrEqualTo(panel.Bounds.Bottom - 1);

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an external unclipped Container renders and hit-tests a child outside its own bounds.</summary>
    [Fact]
    public async Task HitTest_WhenExternalContainerDoesNotClip_ReachesOutsideChildAsync()
    {
        await using var terminal = new ConsumerTerminal();
        terminal.QueueResize(new Dimensions(new Size(6, 2)));
        var child = new Gauge { Value = 7 };
        var panel = new OverflowPanel
        {
            Width = Length.Cells(2),
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        panel.Children.Add(child);
        await using var application = new Application(
            panel,
            terminal,
            terminal,
            TerminalOptions.Minimal);

        await application.StartAsync(TestContext.Current.CancellationToken);

        var outside = new Point(panel.Bounds.Right, panel.Bounds.Y);
        panel.Bounds.Contains(outside).ShouldBeFalse();
        child.Bounds.Contains(outside).ShouldBeTrue();
        child.RenderCount.ShouldBeGreaterThan(0);
        panel.HitTest(outside).ShouldBeSameAs(child);
        panel.HitTest(new Point(outside.X + 1, outside.Y)).ShouldBeNull();

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies focus, capture, visual invalidation, and implicit cancellation stay behind protected helpers.</summary>
    [Fact]
    public async Task Capture_WhenControlBecomesDisabled_ClearsOwnershipBeforeCancellationHookAsync()
    {
        await using var terminal = new ConsumerTerminal();
        terminal.QueueResize(new Dimensions(new Size(8, 3)));
        var probe = new InteractiveProbe();
        await using var application = new Application(
            probe,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        probe.TryFocus().ShouldBeFalse();
        probe.TryCapture().ShouldBeFalse();
        await application.StartAsync(TestContext.Current.CancellationToken);

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                probe.AttachedCount.ShouldBe(1);
                probe.TryFocus().ShouldBeTrue();
                probe.TryCapture().ShouldBeTrue();
                probe.HasCapture.ShouldBeTrue();
                probe.ReleaseCapture();
                probe.HasCapture.ShouldBeFalse();
                probe.TryCapture().ShouldBeTrue();
                probe.RefreshLayout();
                probe.RefreshVisualState();

                probe.IsEnabled = false;

                probe.HasCapture.ShouldBeFalse();
                probe.HadCaptureDuringCancellation.ShouldBeFalse();
                probe.CaptureCancellationCount.ShouldBe(1);
                probe.LastCaptureCancellation.ShouldBe(ReleaseReason.Disabled);
                application.Focus.Focused.ShouldBeNull();
            },
            TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);
        probe.DisposingCount.ShouldBe(1);
    }

    /// <summary>Verifies an external cancellation hook cannot recapture while its control detaches.</summary>
    [Fact]
    public async Task Capture_WhenExternalHookRecapturesDuringDetach_RejectsRequestAsync()
    {
        await using var terminal = new ConsumerTerminal();
        terminal.QueueResize(new Dimensions(new Size(8, 3)));
        var probe = new InteractiveProbe() { RecaptureDuringCancellation = true };
        var root = new FlowPanel();
        root.Children.Add(probe);
        await using var application = new Application(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                probe.TryCapture().ShouldBeTrue();

                _ = root.Children.Remove(probe);

                probe.RecaptureDuringCancellationResult.ShouldBe(false);
                probe.HasCapture.ShouldBeFalse();
                probe.Parent.ShouldBeNull();
            },
            TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the root attachment hook observes every application-owned runtime context.</summary>
    [Fact]
    public async Task OnAttached_WhenApplicationStarts_ObservesCompleteRuntimeContextAsync()
    {
        await using var terminal = new ConsumerTerminal();
        terminal.QueueResize(new Dimensions(new Size(8, 3)));
        var root = new InteractiveProbe()
        {
            Foreground = ThemeColors.Accent,
        };
        await using var application = new Application(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        application.Theme.TryGetColor(ColorRole.Accent, out var expectedAccent).ShouldBeTrue();

        await application.StartAsync(TestContext.Current.CancellationToken);

        root.AttachedDispatcher.ShouldBeSameAs(application.Dispatcher);
        root.AttachedCellPolicy.ShouldBeSameAs(application.CellPolicy);
        (root.AttachedForeground == expectedAccent).ShouldBeTrue(
            "The attachment hook must resolve the active application theme.");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies application-owned focus and capture managers exist before root attachment publishes.</summary>
    [Fact]
    public async Task OnAttached_WhenApplicationStarts_CanAcquireFocusAndCaptureAsync()
    {
        await using var terminal = new ConsumerTerminal();
        terminal.QueueResize(new Dimensions(new Size(8, 3)));
        var root = new InteractiveProbe() { RequestOwnershipOnAttach = true };
        await using var application = new Application(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);

        await application.StartAsync(TestContext.Current.CancellationToken);

        root.AttachmentFocusResult.ShouldBe(true);
        root.AttachmentCaptureResult.ShouldBe(true);
        application.Focus.Focused.ShouldBeSameAs(root);
        application.Capture.Captured.ShouldBeSameAs(root);

        await application.Dispatcher.InvokeAsync(
            root.ReleaseCapture,
            TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a detached control cannot become an application root while another control owns it.</summary>
    [Fact]
    public async Task Application_WhenDetachedRootIsAlreadyOwned_RejectsRootAsync()
    {
        await using var terminal = new ConsumerTerminal();
        var owner = new FlowPanel();
        var root = new InteractiveProbe();
        owner.Children.Add(root);
        Application? application = null;

        try
        {
            root.Dispatcher.ShouldBeNull();
            root.Parent.ShouldBeSameAs(owner);

            var exception = Should.Throw<ArgumentException>(() =>
                application = new Application(
                    root,
                    terminal,
                    terminal,
                    TerminalOptions.Minimal));

            exception.ParamName.ShouldBe("root");
            root.Parent.ShouldBeSameAs(owner);
        }
        finally
        {
            if (application is not null)
            {
                await application.DisposeAsync();
            }

            owner.Dispose();
        }
    }

    /// <summary>Verifies an external override observes committed old and new ownership state.</summary>
    [Fact]
    public void OnParentChanged_WhenOwnershipCommits_ObservesPublishedParent()
    {
        var owner = new FlowPanel();
        var child = new InteractiveProbe();

        owner.Children.Add(child);
        _ = owner.Children.Remove(child);

        child.ParentChanges.Count.ShouldBe(2);
        var (attachedPrevious, attachedCurrent, attachedObservedParent) = child.ParentChanges[0];
        attachedPrevious.ShouldBeNull();
        attachedCurrent.ShouldBeSameAs(owner);
        attachedObservedParent.ShouldBeSameAs(owner);
        var (detachedPrevious, detachedCurrent, detachedObservedParent) = child.ParentChanges[1];
        detachedPrevious.ShouldBeSameAs(owner);
        detachedCurrent.ShouldBeNull();
        detachedObservedParent.ShouldBeNull();

        child.Dispose();
        owner.Dispose();
    }
}
