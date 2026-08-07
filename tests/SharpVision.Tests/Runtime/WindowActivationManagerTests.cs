// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using SharpVision.Runtime;

/// <summary>Verifies application-owned Window activation identity and lifetime.</summary>
public sealed class WindowActivationManagerTests
{
    /// <summary>Verifies activating owned descendants leaves exactly one owning Window active.</summary>
    [Fact]
    public async Task Activate_WhenTargetsChange_SwitchesOneActiveWindowAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var firstChild = new ProbeControl();
            var secondChild = new ProbeControl();
            var first = new Window { Content = firstChild };
            var second = new Window { Content = secondChild };
            var root = new Overlay { Children = { first, second } };
            root.Attach(dispatcher);
            using var manager = new WindowActivationManager(root);

            _ = manager.Activate(firstChild);

            manager.ActiveWindow.ShouldBeSameAs(first);
            first.Active.ShouldBeTrue();
            second.Active.ShouldBeFalse();

            _ = manager.Activate(secondChild);

            manager.ActiveWindow.ShouldBeSameAs(second);
            first.Active.ShouldBeFalse();
            second.Active.ShouldBeTrue();

            _ = manager.Activate(root);

            manager.ActiveWindow.ShouldBeNull();
            first.Active.ShouldBeFalse();
            second.Active.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies activation and deactivation each publish a PropertyChanged notification
    /// for Active, so a data-bound consumer (e.g. highlighting the active window) observes every
    /// transition the manager drives, not just the ones reached through a hypothetical direct
    /// setter.</summary>
    [Fact]
    public async Task Activate_WhenTargetsChange_RaisesIsActivePropertyChangedAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var firstChild = new ProbeControl();
            var secondChild = new ProbeControl();
            var first = new Window { Content = firstChild };
            var second = new Window { Content = secondChild };
            var root = new Overlay { Children = { first, second } };
            root.Attach(dispatcher);
            using var manager = new WindowActivationManager(root);
            var firstRaised = new List<string?>();
            var secondRaised = new List<string?>();
            first.PropertyChanged += (_, args) => firstRaised.Add(args.PropertyName);
            second.PropertyChanged += (_, args) => secondRaised.Add(args.PropertyName);

            _ = manager.Activate(firstChild);

            firstRaised.ShouldContain(nameof(Window.Active));

            firstRaised.Clear();
            _ = manager.Activate(secondChild);

            firstRaised.ShouldContain(nameof(Window.Active));
            secondRaised.ShouldContain(nameof(Window.Active));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies unavailable active Windows cannot remain published by the manager.</summary>
    [Fact]
    public async Task Availability_WhenActiveWindowBecomesUnavailable_ClearsActivationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var hidden = new Window();
            var disabled = new Window();
            var detached = new Window();
            var disposed = new Window();
            var root = new Overlay { Children = { hidden, disabled, detached, disposed } };
            root.Attach(dispatcher);
            using var manager = new WindowActivationManager(root);

            _ = manager.Activate(hidden);
            hidden.Visibility = Visibility.Hidden;
            manager.ActiveWindow.ShouldBeNull();
            hidden.Active.ShouldBeFalse();

            _ = manager.Activate(disabled);
            disabled.Enabled = false;
            manager.ActiveWindow.ShouldBeNull();
            disabled.Active.ShouldBeFalse();

            _ = manager.Activate(detached);
            _ = root.Children.Remove(detached);
            manager.ActiveWindow.ShouldBeNull();
            detached.Active.ShouldBeFalse();

            _ = manager.Activate(disposed);
            disposed.Dispose();
            manager.ActiveWindow.ShouldBeNull();
            disposed.Active.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies losing the active Window restores the most recently active remaining
    /// available Window instead of leaving no Window active.</summary>
    [Fact]
    public async Task Availability_WhenActiveWindowBecomesUnavailable_RestoresMostRecentlyActiveWindowAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var first = new Window();
            var second = new Window();
            var root = new Overlay { Children = { first, second } };
            root.Attach(dispatcher);
            using var manager = new WindowActivationManager(root);

            _ = manager.Activate(first);
            _ = manager.Activate(second);

            second.Visibility = Visibility.Hidden;

            manager.ActiveWindow.ShouldBeSameAs(first);
            first.Active.ShouldBeTrue();
            second.Active.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the fallback walk skips a history entry that is itself unavailable and
    /// restores the next-most-recently active available Window instead.</summary>
    [Fact]
    public async Task Availability_WhenMostRecentFallbackCandidateIsUnavailable_SkipsToNextAvailableInHistoryAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var first = new Window();
            var second = new Window();
            var third = new Window();
            var root = new Overlay { Children = { first, second, third } };
            root.Attach(dispatcher);
            using var manager = new WindowActivationManager(root);

            _ = manager.Activate(first);
            _ = manager.Activate(second);
            _ = manager.Activate(third);

            second.Visibility = Visibility.Hidden;
            third.Visibility = Visibility.Hidden;

            manager.ActiveWindow.ShouldBeSameAs(first);
            first.Active.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies nested Window ancestry selects the Window nearest the target.</summary>
    [Fact]
    public async Task Activate_WhenWindowsAreNested_SelectsNearestWindowAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var target = new ProbeControl();
            var inner = new Window { Content = target };
            var outer = new Window { Content = inner };
            outer.Attach(dispatcher);
            using var manager = new WindowActivationManager(outer);

            _ = manager.Activate(target);

            manager.ActiveWindow.ShouldBeSameAs(inner);
            inner.Active.ShouldBeTrue();
            outer.Active.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies activating a Window raises it above its sibling Windows in Overlay
    /// z-order without disturbing a non-Window sibling.</summary>
    [Fact]
    public async Task Activate_WhenTargetWindowIsBuried_RaisesItAboveSiblingWindowsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var first = new Window();
            var second = new Window();
            var background = new ProbeControl();
            var root = new Overlay { Children = { background, first, second } };
            root.Attach(dispatcher);
            Overlay.SetZIndex(second, 5);
            using var manager = new WindowActivationManager(root);

            _ = manager.Activate(first);

            Overlay.GetZIndex(first).ShouldBe(6);
            Overlay.GetZIndex(second).ShouldBe(5);
            Overlay.GetZIndex(background).ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies re-activating the already-topmost Window leaves z-order unchanged.</summary>
    [Fact]
    public async Task Activate_WhenTargetWindowIsAlreadyTopmost_LeavesZIndexUnchangedAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var first = new Window();
            var second = new Window();
            var root = new Overlay { Children = { first, second } };
            root.Attach(dispatcher);
            using var manager = new WindowActivationManager(root);

            _ = manager.Activate(second);
            var raisedZ = Overlay.GetZIndex(second);

            _ = manager.Activate(second);

            Overlay.GetZIndex(second).ShouldBe(raisedZ);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies restoring the most recently active Window after activation loss also
    /// raises it above its sibling Windows.</summary>
    [Fact]
    public async Task Availability_WhenActiveWindowBecomesUnavailable_RaisesRestoredWindowAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var first = new Window();
            var second = new Window();
            var root = new Overlay { Children = { first, second } };
            root.Attach(dispatcher);
            using var manager = new WindowActivationManager(root);

            _ = manager.Activate(first);
            _ = manager.Activate(second);
            second.Visibility = Visibility.Hidden;

            manager.ActiveWindow.ShouldBeSameAs(first);
            Overlay.GetZIndex(first).ShouldBeGreaterThan(Overlay.GetZIndex(second));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a Window not hosted directly in an Overlay (nested inside another
    /// Window) activates without attempting to raise a z-index.</summary>
    [Fact]
    public async Task Activate_WhenWindowHasNoOverlayParent_DoesNotThrowAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var target = new ProbeControl();
            var inner = new Window { Content = target };
            var outer = new Window { Content = inner };
            outer.Attach(dispatcher);
            using var manager = new WindowActivationManager(outer);

            _ = manager.Activate(target);

            manager.ActiveWindow.ShouldBeSameAs(inner);
        }, TestContext.Current.CancellationToken);
    }
}
