using SharpVision.Input;
using SharpVision.Layout;
using SharpVision.Tests.Support;
using SharpVision.Threading;

using Shouldly;

namespace SharpVision.Tests.Input;

/// <summary>Verifies transactional focus, navigation, and invalid-state cleanup.</summary>
public sealed class FocusTests
{
    /// <summary>Verifies commit happens before lost and gained callbacks.</summary>
    [Fact]
    public async Task Focus_WhenTargetIsEligible_CommitsBeforeNotificationsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var order = new List<string>();
            var root = new RecordingControl("root", order);
            var first = new ProbeControl { CanFocus = true };
            var second = new ProbeControl { CanFocus = true };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(first).ShouldBeTrue();
            order.Clear();
            manager.Changing += (_, eventArgs) =>
            {
                eventArgs.Previous.ShouldBeSameAs(first);
                eventArgs.Next.ShouldBeSameAs(second);
                order.Add("preview");
            };
            manager.Lost += (_, eventArgs) =>
            {
                manager.Focused.ShouldBeSameAs(second);
                first.IsFocused.ShouldBeFalse();
                second.IsFocused.ShouldBeTrue();
                eventArgs.Previous.ShouldBeSameAs(first);
                order.Add("lost");
            };
            manager.Gained += (_, eventArgs) =>
            {
                manager.Focused.ShouldBeSameAs(second);
                eventArgs.Current.ShouldBeSameAs(second);
                order.Add("gained");
            };

            manager.Focus(second).ShouldBeTrue();

            order.ShouldBe(["preview", "lost", "gained"]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies preview cancellation leaves the complete old state intact.</summary>
    [Fact]
    public async Task Focus_WhenPreviewCancels_PreservesPreviousFocusAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var first = new ProbeControl { CanFocus = true };
            var second = new ProbeControl { CanFocus = true };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(first).ShouldBeTrue();
            manager.Changing += (_, eventArgs) => eventArgs.Cancel = true;

            manager.Focus(second).ShouldBeFalse();

            manager.Focused.ShouldBeSameAs(first);
            first.IsFocused.ShouldBeTrue();
            second.IsFocused.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies tab order uses index then tree order and wraps both directions.</summary>
    [Fact]
    public async Task MoveNext_WhenTreeHasFocusableControls_OrdersAndWrapsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var first = new ProbeControl { CanFocus = true, TabIndex = 1 };
            var second = new ProbeControl { CanFocus = true, TabIndex = 0 };
            var third = new ProbeControl { CanFocus = true, TabIndex = 1 };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Children.Add(third);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(second);
            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(first);
            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(third);
            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(second);
            manager.MoveNext(reverse: true).ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(third);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies membership and eligibility reject invalid explicit targets.</summary>
    [Fact]
    public async Task Focus_WhenTargetIsForeignOrIneligible_RejectsWithoutMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var hidden = new ProbeControl
            {
                CanFocus = true,
                Visibility = Visibility.Hidden,
            };
            var foreign = new ProbeControl { CanFocus = true };
            root.Children.Add(hidden);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);

            manager.Focus(hidden).ShouldBeFalse();
            _ = Should.Throw<ArgumentException>(() => manager.Focus(foreign));
            manager.Focused.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disable, hide, detach, and preview mutation clear or reject safely.</summary>
    [Fact]
    public async Task Focus_WhenTreeMutates_ReleasesInvalidReferencesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var child = new ProbeControl { CanFocus = true };
            var replacement = new ProbeControl { CanFocus = true };
            root.Children.Add(child);
            root.Children.Add(replacement);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(child).ShouldBeTrue();

            root.IsEnabled = false;
            manager.Focused.ShouldBeNull();
            child.IsFocused.ShouldBeFalse();
            root.IsEnabled = true;
            manager.Focus(child).ShouldBeTrue();
            child.Visibility = Visibility.Hidden;
            manager.Focused.ShouldBeNull();
            child.Visibility = Visibility.Visible;
            manager.Focus(child).ShouldBeTrue();
            _ = root.Children.Remove(child);
            manager.Focused.ShouldBeNull();

            manager.Changing += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Next, replacement))
                {
                    _ = root.Children.Remove(replacement);
                }
            };
            manager.Focus(replacement).ShouldBeFalse();
            manager.Focused.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }
}
