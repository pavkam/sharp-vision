// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

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
            first.IsActive.ShouldBeTrue();
            second.IsActive.ShouldBeFalse();

            _ = manager.Activate(secondChild);

            manager.ActiveWindow.ShouldBeSameAs(second);
            first.IsActive.ShouldBeFalse();
            second.IsActive.ShouldBeTrue();

            _ = manager.Activate(root);

            manager.ActiveWindow.ShouldBeNull();
            first.IsActive.ShouldBeFalse();
            second.IsActive.ShouldBeFalse();
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
            hidden.IsActive.ShouldBeFalse();

            _ = manager.Activate(disabled);
            disabled.IsEnabled = false;
            manager.ActiveWindow.ShouldBeNull();
            disabled.IsActive.ShouldBeFalse();

            _ = manager.Activate(detached);
            _ = root.Children.Remove(detached);
            manager.ActiveWindow.ShouldBeNull();
            detached.IsActive.ShouldBeFalse();

            _ = manager.Activate(disposed);
            disposed.Dispose();
            manager.ActiveWindow.ShouldBeNull();
            disposed.IsActive.ShouldBeFalse();
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
            inner.IsActive.ShouldBeTrue();
            outer.IsActive.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }
}
