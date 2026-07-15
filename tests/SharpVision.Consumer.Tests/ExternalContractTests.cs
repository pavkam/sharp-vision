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
}
