// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies central ownership slots, transactional publication, and disposal.</summary>
public sealed class OwnedControlRegistryTests
{
    /// <summary>Verifies slot impact is pending before the committed slot notification runs.</summary>
    [Fact]
    public void Add_WhenSlotPublishesChange_InvalidatesBeforeNotification()
    {
        var owner = new ProbeOwnedControl();
        new Engine().Layout(owner, new Size(4, 1));
        (owner.Pending & Invalidation.Measure).ShouldBe(Invalidation.None);
        var pendingDuringNotification = Invalidation.None;
        owner.PrimaryChanging = control =>
            pendingDuringNotification = control.Pending & Invalidation.Measure;

        owner.AddPrimary(new ProbeControl());

        pendingDuringNotification.ShouldBe(Invalidation.Measure);
    }

    /// <summary>Verifies same-role slots remain independent and traverse in registration order.</summary>
    [Fact]
    public void Slots_WhenOwnerRegistersSameRoleTwice_RemainDistinctAndOrdered()
    {
        var owner = new ProbeOwnedControl(primaryCapacity: 2);
        var first = new ProbeControl();
        var second = new ProbeControl();
        var part = new ProbeControl();

        owner.AddPrimary(first);
        owner.AddPrimary(second);
        owner.AddSecondary(part);

        owner.PrimaryCount.ShouldBe(2);
        owner.SecondaryCount.ShouldBe(1);
        owner.GetOwnedOrder().ShouldBe([first, second, part]);
        first.Parent.ShouldBeSameAs(owner);
        part.Parent.ShouldBeSameAs(owner);
        owner.PrimaryOptions.Role.ShouldBe(OwnedControlRole.FrameworkPart);
        owner.PrimaryOptions.PartKey.ShouldBe("primary");
        owner.PrimaryOptions.ParticipatesInNavigation.ShouldBeFalse();
    }

    /// <summary>Verifies complete batch validation preserves the original tree for every invalid candidate.</summary>
    [Fact]
    public async Task ReplaceAll_WhenAnyCandidateIsInvalid_PreservesCompleteOldTreeAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeOwnedControl(primaryCapacity: 2);
        var other = new ProbeOwnedControl();
        var first = new ProbeControl();
        var second = new ProbeControl();
        var crossOwned = new ProbeControl();
        var disposed = new ProbeControl();
        var attached = new ProbeControl();
        owner.ReplaceAllPrimary([first, second]);
        other.AddPrimary(crossOwned);
        disposed.Dispose();
        await dispatcher.InvokeAsync(
            () => attached.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<ArgumentNullException>(
            () => owner.ReplaceAllPrimary([new ProbeControl(), null!]));
        _ = Should.Throw<ArgumentException>(
            () => owner.ReplaceAllPrimary([crossOwned]));
        _ = Should.Throw<ObjectDisposedException>(
            () => owner.ReplaceAllPrimary([disposed]));
        _ = Should.Throw<ArgumentException>(
            () => owner.ReplaceAllPrimary([attached]));
        var duplicate = new ProbeControl();
        _ = Should.Throw<ArgumentException>(
            () => owner.ReplaceAllPrimary([duplicate, duplicate]));
        _ = Should.Throw<InvalidOperationException>(
            () => owner.ReplaceAllPrimary([new ProbeControl(), new ProbeControl(), new ProbeControl()]));

        owner.PrimaryCount.ShouldBe(2);
        owner.PrimaryAt(0).ShouldBeSameAs(first);
        owner.PrimaryAt(1).ShouldBeSameAs(second);
        first.Parent.ShouldBeSameAs(owner);
        second.Parent.ShouldBeSameAs(owner);
        crossOwned.Parent.ShouldBeSameAs(other);
    }

    /// <summary>Verifies cycles, indexes, and per-slot capacity reject before ownership mutation.</summary>
    [Fact]
    public void Mutation_WhenCandidateOrIndexIsInvalid_PreservesCompleteOldTree()
    {
        var root = new ProbeOwnedControl(primaryCapacity: 1);
        var branch = new ProbeOwnedControl();
        var child = new ProbeControl();
        root.AddPrimary(branch);
        branch.AddPrimary(child);

        _ = Should.Throw<ArgumentException>(() => branch.AddSecondary(root));
        _ = Should.Throw<InvalidOperationException>(() => root.AddPrimary(new ProbeControl()));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => branch.InsertPrimary(5, new ProbeControl()));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => branch.ReplacePrimary(5, new ProbeControl()));

        root.PrimaryAt(0).ShouldBeSameAs(branch);
        branch.PrimaryAt(0).ShouldBeSameAs(child);
        branch.Parent.ShouldBeSameAs(root);
        child.Parent.ShouldBeSameAs(branch);
    }

    /// <summary>Verifies manager cleanup observes the old tree and lifecycle observes the complete new tree.</summary>
    [Fact]
    public async Task Replace_WhenManagersRelease_NotificationsSeeOldTreeThenLifecycleSeesNewTreeAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeOwnedControl(primaryCapacity: 1);
        var previous = new OwnershipObserverControl { CanFocus = true };
        var replacement = new OwnershipObserverControl();
        owner.AddPrimary(previous);

        await dispatcher.InvokeAsync(() =>
        {
            owner.Attach(dispatcher);
            using FocusManager focus = new(owner);
            using CaptureManager capture = new(owner);
            focus.Focus(previous).ShouldBeTrue();
            capture.Capture(previous).ShouldBeTrue();
            focus.Lost += (_, _) =>
            {
                previous.Parent.ShouldBeSameAs(owner);
                owner.PrimaryAt(0).ShouldBeSameAs(previous);
                previous.Dispatcher.ShouldBeSameAs(dispatcher);
            };
            capture.Cancelled += (_, eventArgs) =>
            {
                eventArgs.Control.ShouldBeSameAs(previous);
                previous.Parent.ShouldBeSameAs(owner);
                owner.PrimaryAt(0).ShouldBeSameAs(previous);
            };
            previous.ParentChanging = (control, oldParent, newParent) =>
            {
                oldParent.ShouldBeSameAs(owner);
                newParent.ShouldBeNull();
                control.Parent.ShouldBeNull();
                owner.PrimaryAt(0).ShouldBeSameAs(replacement);
                replacement.Parent.ShouldBeSameAs(owner);
                control.Dispatcher.ShouldBeNull();
            };
            replacement.ParentChanging = (control, oldParent, newParent) =>
            {
                oldParent.ShouldBeNull();
                newParent.ShouldBeSameAs(owner);
                control.Parent.ShouldBeSameAs(owner);
                owner.PrimaryAt(0).ShouldBeSameAs(replacement);
                previous.Parent.ShouldBeNull();
                control.Dispatcher.ShouldBeSameAs(dispatcher);
                control.InheritedFocusOwner.ShouldBeSameAs(focus);
                control.InheritedCaptureOwner.ShouldBeSameAs(capture);
            };

            owner.ReplacePrimary(0, replacement);

            focus.Focused.ShouldBeNull();
            capture.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies every callback observes a fully committed attached or detached subtree.</summary>
    [Fact]
    public async Task AddAndRemove_WhenSubtreeSpansSlots_PublishesAfterWholeContextCommitsAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var policy = new Policy(Ambiguous.Wide);
        var root = new ProbeOwnedControl();
        var branch = new ProbeOwnedControl();
        var first = new OwnershipObserverControl();
        var second = new OwnershipObserverControl();
        branch.AddPrimary(first);
        branch.AddSecondary(second);
        var context = ThemeContext.Create(new Theme());

        await dispatcher.InvokeAsync(() =>
        {
            root.Attach(dispatcher, policy);
            root.PropagateThemeContext(context);
            using FocusManager focus = new(root);
            using CaptureManager capture = new(root);

            first.Attaching = _ => AssertAttachedSubtree();
            second.Attaching = _ => AssertAttachedSubtree();
            first.Detaching = _ => AssertDetachedSubtree();
            second.Detaching = _ => AssertDetachedSubtree();

            root.AddPrimary(branch);
            root.RemovePrimary(branch).ShouldBeTrue();

            void AssertAttachedSubtree()
            {
                foreach (var control in new[] { first, second })
                {
                    control.Dispatcher.ShouldBeSameAs(dispatcher);
                    control.InheritedThemeContext.ShouldBeSameAs(context);
                    control.InheritedCellPolicy.ShouldBeSameAs(policy);
                    control.InheritedFocusOwner.ShouldBeSameAs(focus);
                    control.InheritedCaptureOwner.ShouldBeSameAs(capture);
                }
            }

            void AssertDetachedSubtree()
            {
                foreach (var control in new[] { first, second })
                {
                    control.Dispatcher.ShouldBeNull();
                    control.InheritedThemeContext.ShouldBeNull();
                    control.InheritedCellPolicy.ShouldBeSameAs(Policy.Default);
                    control.InheritedFocusOwner.ShouldBeNull();
                    control.InheritedCaptureOwner.ShouldBeNull();
                }
            }
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies publication failures cannot strand any edge or suppress later callbacks.</summary>
    [Fact]
    public async Task Clear_WhenPublicationCallbackThrows_CommitsAllEdgesAndContinuesCleanupAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeOwnedControl();
        var first = new OwnershipObserverControl
        {
            ThrowWhenParentClears = true,
            ThrowOnDetached = true,
        };
        var second = new OwnershipObserverControl();
        owner.AddPrimary(first);
        owner.AddPrimary(second);

        await dispatcher.InvokeAsync(() =>
        {
            owner.Attach(dispatcher);

            var exception = Should.Throw<InvalidOperationException>(owner.ClearPrimary);

            exception.Message.ShouldBe("The parent callback failed.");
            owner.PrimaryCount.ShouldBe(0);
            first.Parent.ShouldBeNull();
            second.Parent.ShouldBeNull();
            first.Dispatcher.ShouldBeNull();
            second.Dispatcher.ShouldBeNull();
            first.DetachedCalls.ShouldBe(1);
            second.DetachedCalls.ShouldBe(1);
            owner.AddPrimary(new ProbeControl());
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies direct child disposal removes through its exact slot with one reason.</summary>
    [Fact]
    public async Task Dispose_WhenOwnedChildIsDisposedDirectly_RemovesThroughExactSlotOnceAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeOwnedControl();
        var child = new OwnershipObserverControl { CanFocus = true };
        owner.AddPrimary(child);

        await dispatcher.InvokeAsync(() =>
        {
            owner.Attach(dispatcher);
            using FocusManager focus = new(owner);
            using CaptureManager capture = new(owner);
            focus.Focus(child).ShouldBeTrue();
            capture.Capture(child).ShouldBeTrue();
            var cancelled = 0;
            capture.Cancelled += (_, eventArgs) =>
            {
                eventArgs.Reason.ShouldBe(ReleaseReason.Disposed);
                cancelled++;
            };

            child.Dispose();

            owner.PrimaryCount.ShouldBe(0);
            owner.PrimaryChanges.ShouldBe(2);
            child.Parent.ShouldBeNull();
            child.IsDisposed.ShouldBeTrue();
            child.DisposingCalls.ShouldBe(1);
            child.UnavailableReasons.ShouldBe([ReleaseReason.Disposed]);
            cancelled.ShouldBe(1);
            owner.Dispose();
            child.DisposingCalls.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies owner disposal continues across slots after one child callback fails.</summary>
    [Fact]
    public void Dispose_WhenOwnerHasChildrenAcrossSlots_DisposesEveryDescendantOnce()
    {
        var owner = new ProbeOwnedControl();
        var first = new OwnershipObserverControl { ThrowOnDisposing = true };
        var second = new OwnershipObserverControl();
        owner.AddPrimary(first);
        owner.AddSecondary(second);

        var exception = Should.Throw<InvalidOperationException>(owner.Dispose);

        exception.Message.ShouldBe("The disposal callback failed.");
        owner.IsDisposed.ShouldBeTrue();
        first.IsDisposed.ShouldBeTrue();
        second.IsDisposed.ShouldBeTrue();
        first.DisposingCalls.ShouldBe(1);
        second.DisposingCalls.ShouldBe(1);
        owner.PrimaryCount.ShouldBe(0);
        owner.SecondaryCount.ShouldBe(0);
    }

    /// <summary>Verifies callbacks cannot mutate any registry participating in an active tree transaction.</summary>
    [Fact]
    public void Replace_WhenCallbackMutatesRemovedSubtreeRegistry_RejectsReentrancy()
    {
        var owner = new ProbeOwnedControl(primaryCapacity: 1);
        var previous = new ProbeOwnedControl();
        var replacement = new ProbeOwnedControl();
        Exception? nestedFailure = null;
        owner.AddPrimary(previous);
        previous.ParentChanging = (_, _, current) =>
        {
            if (current is not null)
            {
                return;
            }

            try
            {
                previous.AddPrimary(new ProbeControl());
            }
            catch (Exception exception)
            {
                nestedFailure = exception;
            }
        };

        owner.ReplacePrimary(0, replacement);

        _ = nestedFailure.ShouldBeOfType<InvalidOperationException>();
        previous.PrimaryCount.ShouldBe(0);
        owner.PrimaryAt(0).ShouldBeSameAs(replacement);
    }

    /// <summary>Verifies disposal cannot reenter structural publication through a removed root.</summary>
    [Fact]
    public void Replace_WhenCallbackDisposesRemovedRoot_RejectsReentrancy()
    {
        var owner = new ProbeOwnedControl(primaryCapacity: 1);
        var previous = new ProbeOwnedControl();
        var replacement = new ProbeOwnedControl();
        Exception? nestedFailure = null;
        owner.AddPrimary(previous);
        previous.ParentChanging = (_, _, current) =>
        {
            if (current is not null)
            {
                return;
            }

            try
            {
                previous.Dispose();
            }
            catch (Exception exception)
            {
                nestedFailure = exception;
            }
        };

        owner.ReplacePrimary(0, replacement);

        _ = nestedFailure.ShouldBeOfType<InvalidOperationException>();
        previous.IsDisposed.ShouldBeFalse();
        owner.PrimaryAt(0).ShouldBeSameAs(replacement);
    }

    /// <summary>Verifies a disposing hook cannot detach through the ordinary slot path.</summary>
    [Fact]
    public void Dispose_WhenDisposingHookRequestsOrdinaryRemoval_RejectsBeforeMutation()
    {
        var owner = new ProbeOwnedControl();
        var child = new OwnershipObserverControl();
        Exception? nestedFailure = null;
        owner.AddPrimary(child);
        child.Disposing = control =>
        {
            try
            {
                _ = owner.RemovePrimary(control);
            }
            catch (Exception exception)
            {
                nestedFailure = exception;
            }

            owner.PrimaryCount.ShouldBe(1);
            control.Parent.ShouldBeSameAs(owner);
        };

        child.Dispose();

        _ = nestedFailure.ShouldBeOfType<InvalidOperationException>();
        child.UnavailableReasons.ShouldBe([ReleaseReason.Disposed]);
        child.IsDisposed.ShouldBeTrue();
        child.Parent.ShouldBeNull();
        owner.PrimaryCount.ShouldBe(0);
    }

    /// <summary>Verifies an unavailable hook cannot publish Detached during disposal.</summary>
    [Fact]
    public void Dispose_WhenUnavailableHookRequestsOrdinaryRemoval_PublishesOnlyDisposed()
    {
        var owner = new ProbeOwnedControl();
        var child = new OwnershipObserverControl();
        Exception? nestedFailure = null;
        owner.AddPrimary(child);
        child.BecomingUnavailable = (control, reason) =>
        {
            if (reason != ReleaseReason.Disposed)
            {
                return;
            }

            try
            {
                _ = owner.RemovePrimary(control);
            }
            catch (Exception exception)
            {
                nestedFailure = exception;
            }

            owner.PrimaryCount.ShouldBe(1);
            control.Parent.ShouldBeSameAs(owner);
        };

        child.Dispose();

        _ = nestedFailure.ShouldBeOfType<InvalidOperationException>();
        child.UnavailableReasons.ShouldBe([ReleaseReason.Disposed]);
        child.IsDisposed.ShouldBeTrue();
        child.Parent.ShouldBeNull();
        owner.PrimaryCount.ShouldBe(0);
    }

    /// <summary>Verifies a removed root cannot reparent itself to an unrelated owner during publication.</summary>
    [Fact]
    public void Remove_WhenParentCallbackReparentsToUnrelatedOwner_RejectsCandidateBeforeMutation()
    {
        var owner = new ProbeOwnedControl();
        var unrelated = new ProbeOwnedControl();
        var child = new OwnershipObserverControl();
        Exception? nestedFailure = null;
        owner.AddPrimary(child);
        child.ParentChanging = (control, _, current) =>
        {
            if (current is not null)
            {
                return;
            }

            try
            {
                unrelated.AddPrimary(control);
            }
            catch (Exception exception)
            {
                nestedFailure = exception;
            }
        };

        owner.RemovePrimary(child).ShouldBeTrue();

        _ = nestedFailure.ShouldBeOfType<InvalidOperationException>();
        owner.PrimaryCount.ShouldBe(0);
        unrelated.PrimaryCount.ShouldBe(0);
        child.Parent.ShouldBeNull();
        child.UnavailableReasons.ShouldBe([ReleaseReason.Detached]);
    }

    /// <summary>Verifies a disposing root cannot reparent itself from its detach callback.</summary>
    [Fact]
    public async Task Dispose_WhenDetachedCallbackReparentsToUnrelatedOwner_RejectsCandidateBeforeMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeOwnedControl();
        var unrelated = new ProbeOwnedControl();
        var child = new OwnershipObserverControl();
        Exception? nestedFailure = null;
        owner.AddPrimary(child);

        await dispatcher.InvokeAsync(() =>
        {
            owner.Attach(dispatcher);
            unrelated.Attach(dispatcher);
            child.Detaching = control =>
            {
                try
                {
                    unrelated.AddPrimary(control);
                }
                catch (Exception exception)
                {
                    nestedFailure = exception;
                }
            };

            try
            {
                child.Dispose();

                _ = nestedFailure.ShouldBeOfType<InvalidOperationException>();
                owner.PrimaryCount.ShouldBe(0);
                unrelated.PrimaryCount.ShouldBe(0);
                child.Parent.ShouldBeNull();
                child.UnavailableReasons.ShouldBe([ReleaseReason.Disposed]);
                child.IsDisposed.ShouldBeTrue();
            }
            finally
            {
                unrelated.Dispose();
                owner.Dispose();
            }
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an owned control cannot attach independently from its detached owner.</summary>
    [Fact]
    public async Task Attach_WhenControlIsOwned_ThrowsBeforeContextMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeOwnedControl();
        var child = new OwnershipObserverControl();
        owner.AddPrimary(child);

        await dispatcher.InvokeAsync(() =>
        {
            _ = Should.Throw<InvalidOperationException>(() => child.Attach(dispatcher));

            owner.Dispatcher.ShouldBeNull();
            child.Dispatcher.ShouldBeNull();
            child.Parent.ShouldBeSameAs(owner);
            child.AttachedCalls.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an owned control cannot detach independently from its attached owner.</summary>
    [Fact]
    public async Task Detach_WhenControlIsOwned_ThrowsBeforeContextMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeOwnedControl();
        var child = new OwnershipObserverControl();
        owner.AddPrimary(child);

        await dispatcher.InvokeAsync(() =>
        {
            owner.Attach(dispatcher);

            _ = Should.Throw<InvalidOperationException>(child.Detach);

            owner.Dispatcher.ShouldBeSameAs(dispatcher);
            child.Dispatcher.ShouldBeSameAs(dispatcher);
            child.Parent.ShouldBeSameAs(owner);
            child.DetachedCalls.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }
}
