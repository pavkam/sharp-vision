// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies transactional focus, navigation, and invalid-state cleanup.</summary>
public sealed class FocusTests
{
    #region Focus transactions and cleanup

    /// <summary>Verifies making the focused control ineligible commits uncancellable cleanup before notification returns.</summary>
    [Fact]
    public async Task CanFocus_WhenFocusedControlBecomesFalse_ReleasesFocusSynchronouslyAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var child = new ProbeControl { Focusable = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(child).ShouldBeTrue();
            var changingCalls = 0;
            var lostCalls = 0;
            var order = new List<string>();
            manager.Changing += (_, eventArgs) =>
            {
                changingCalls++;
                eventArgs.Cancel = true;
            };
            manager.Lost += (_, eventArgs) =>
            {
                manager.Focused.ShouldBeNull();
                child.CanFocus.ShouldBeFalse();
                child.IsFocused.ShouldBeFalse();
                eventArgs.Previous.ShouldBeSameAs(child);
                eventArgs.Current.ShouldBeNull();
                eventArgs.Reason.ShouldBe(FocusReason.Unavailable);
                lostCalls++;
                order.Add("lost");
            };
            child.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlBase.CanFocus))
                {
                    manager.Focused.ShouldBeNull();
                    child.IsFocused.ShouldBeFalse();
                    order.Add("can-focus");
                }
            };

            child.Focusable = false;

            manager.Focused.ShouldBeNull();
            child.IsFocused.ShouldBeFalse();
            changingCalls.ShouldBe(0);
            lostCalls.ShouldBe(1);
            order.ShouldBe(["lost", "can-focus"]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies ineligibility raised inside a focus notification is cleaned before the enclosing request returns.</summary>
    [Fact]
    public async Task Focus_WhenGainedMakesControlIneligible_CleansBeforeRequestReturnsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var child = new ProbeControl { Focusable = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            var focusReturned = false;
            var gainedCalls = 0;
            var lostCalls = 0;
            var notificationCalls = 0;
            manager.Gained += (_, eventArgs) =>
            {
                eventArgs.Current.ShouldBeSameAs(child);
                gainedCalls++;
                child.Focusable = false;
            };
            manager.Lost += (_, eventArgs) =>
            {
                focusReturned.ShouldBeFalse();
                manager.Focused.ShouldBeNull();
                child.CanFocus.ShouldBeFalse();
                child.IsFocused.ShouldBeFalse();
                eventArgs.Previous.ShouldBeSameAs(child);
                eventArgs.Current.ShouldBeNull();
                lostCalls++;
            };
            child.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlBase.CanFocus))
                {
                    focusReturned.ShouldBeFalse();
                    manager.Focused.ShouldBeNull();
                    child.IsFocused.ShouldBeFalse();
                    notificationCalls++;
                }
            };

            manager.Focus(child).ShouldBeTrue();
            focusReturned = true;

            manager.Focused.ShouldBeNull();
            child.IsFocused.ShouldBeFalse();
            gainedCalls.ShouldBe(1);
            lostCalls.ShouldBe(1);
            notificationCalls.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies one control's local focus eligibility does not evict a focused descendant.</summary>
    [Fact]
    public async Task CanFocus_WhenAncestorBecomesFalse_PreservesDescendantFocusAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Focusable = true };
            var child = new ProbeControl { Focusable = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(child).ShouldBeTrue();

            root.Focusable = false;

            manager.Focused.ShouldBeSameAs(child);
            child.IsFocused.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies one focus transaction drains eligibility notifications for both old and new targets.</summary>
    [Fact]
    public async Task Focus_WhenOldAndNewTargetsBecomeIneligible_PublishesEveryDeferredChangeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var previous = new ProbeControl { Focusable = true };
            var next = new ProbeControl { Focusable = true };
            root.Children.Add(previous);
            root.Children.Add(next);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(previous).ShouldBeTrue();
            var previousNotifications = 0;
            var nextNotifications = 0;
            previous.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlBase.CanFocus))
                {
                    manager.Focused.ShouldBeNull();
                    previousNotifications++;
                }
            };
            next.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlBase.CanFocus))
                {
                    manager.Focused.ShouldBeNull();
                    nextNotifications++;
                }
            };
            manager.Changing += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Next, next))
                {
                    previous.Focusable = false;
                }
            };
            manager.Gained += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Current, next))
                {
                    next.Focusable = false;
                }
            };

            manager.Focus(next).ShouldBeTrue();

            manager.Focused.ShouldBeNull();
            previousNotifications.ShouldBe(1);
            nextNotifications.ShouldBe(1);
            previous.Focusable = true;
            previous.Focusable = false;
            previousNotifications.ShouldBe(3);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies commit happens before lost and gained callbacks.</summary>
    [Fact]
    public async Task Focus_WhenTargetIsEligible_CommitsBeforeNotificationsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            List<string> order = [];
            var root = new RecordingControl("root", order);
            var first = new ProbeControl { Focusable = true };
            var second = new ProbeControl { Focusable = true };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            using FocusManager manager = new(root);
            manager.Focus(first).ShouldBeTrue();
            order.Clear();
            manager.Changing += (_, eventArgs) =>
            {
                eventArgs.Previous.ShouldBeSameAs(first);
                eventArgs.Next.ShouldBeSameAs(second);
                eventArgs.Reason.ShouldBe(FocusReason.Programmatic);
                order.Add("preview");
            };
            manager.Lost += (_, eventArgs) =>
            {
                manager.Focused.ShouldBeSameAs(second);
                first.IsFocused.ShouldBeFalse();
                second.IsFocused.ShouldBeTrue();
                eventArgs.Previous.ShouldBeSameAs(first);
                eventArgs.Reason.ShouldBe(FocusReason.Programmatic);
                order.Add("lost");
            };
            manager.Gained += (_, eventArgs) =>
            {
                manager.Focused.ShouldBeSameAs(second);
                eventArgs.Current.ShouldBeSameAs(second);
                eventArgs.Reason.ShouldBe(FocusReason.Programmatic);
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
            var first = new ProbeControl { Focusable = true };
            var second = new ProbeControl { Focusable = true };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            using FocusManager manager = new(root);
            manager.Focus(first).ShouldBeTrue();
            manager.Changing += (_, eventArgs) => eventArgs.Cancel = true;

            manager.Focus(second).ShouldBeFalse();

            manager.Focused.ShouldBeSameAs(first);
            first.IsFocused.ShouldBeTrue();
            second.IsFocused.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies focus event payload constructors preserve compatibility and reject undefined reasons.</summary>
    [Fact]
    public void Constructors_WhenReasonIsSupplied_ValidateAndExposeIt()
    {
        var changing = new FocusChangingEventArgs(null, null);
        var changed = new FocusChangedEventArgs(null, null);

        changing.Reason.ShouldBe(FocusReason.Programmatic);
        changed.Reason.ShouldBe(FocusReason.Programmatic);
        new FocusChangingEventArgs(null, null, FocusReason.Keyboard).Reason.ShouldBe(FocusReason.Keyboard);
        new FocusChangedEventArgs(null, null, FocusReason.Pointer).Reason.ShouldBe(FocusReason.Pointer);
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new FocusChangingEventArgs(null, null, (FocusReason) int.MaxValue));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new FocusChangedEventArgs(null, null, (FocusReason) int.MaxValue));
    }

    /// <summary>Verifies reentrant requests preserve target, reason, and cancellation policy.</summary>
    [Fact]
    public async Task Focus_WhenRequestIsReentrant_PreservesReasonAndCancellabilityAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var first = new ProbeControl { Focusable = true };
            var second = new ProbeControl { Focusable = true };
            var third = new ProbeControl { Focusable = true };
            var foreign = new ProbeControl { Focusable = true };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Children.Add(third);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(first).ShouldBeTrue();
            var observed = new List<FocusReason>();
            manager.Changing += (_, eventArgs) =>
            {
                observed.Add(eventArgs.Reason);

                if (ReferenceEquals(eventArgs.Next, second))
                {
                    _ = Should.Throw<ArgumentException>(() =>
                        manager.Focus(foreign, FocusReason.Pointer, cancellable: true));
                    manager.Focus(third, FocusReason.Restore, cancellable: false).ShouldBeFalse();
                }
                else if (ReferenceEquals(eventArgs.Next, third))
                {
                    eventArgs.Cancel = true;
                }
            };

            manager.Focus(second, FocusReason.Keyboard, cancellable: true).ShouldBeTrue();

            manager.Focused.ShouldBeSameAs(third);
            observed.ShouldBe([FocusReason.Keyboard, FocusReason.Restore]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies one failing queued completion cannot strand later queued focus work.</summary>
    [Fact]
    public async Task Focus_WhenQueuedCompletionThrows_ContinuesLaterRequestsBeforeRethrowAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var first = new ProbeControl { Focusable = true };
            var second = new ProbeControl { Focusable = true };
            var rejected = new ProbeControl { Focusable = true };
            var final = new ProbeControl { Focusable = true };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Children.Add(rejected);
            root.Children.Add(final);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(first).ShouldBeTrue();
            var expected = new InvalidOperationException("completion failed");
            var finalCompleted = false;
            manager.Changing += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Next, second))
                {
                    manager.Focus(
                        rejected,
                        FocusReason.Restore,
                        cancellable: false,
                        (_, _) => throw expected);
                    manager.Focus(
                        final,
                        FocusReason.Keyboard,
                        cancellable: false,
                        (committed, failure) =>
                        {
                            failure.ShouldBeNull();
                            finalCompleted = committed;
                        });
                    rejected.Focusable = false;
                }
            };

            var thrown = Should.Throw<InvalidOperationException>(() => manager.Focus(second));

            thrown.ShouldBeSameAs(expected);
            finalCompleted.ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(final);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the primary transaction failure wins while every deferred cleanup stage still runs.</summary>
    [Fact]
    public async Task Focus_WhenTransactionAndCleanupCallbacksThrow_PreservesPrimaryAndDrainsRequestsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var first = new ProbeControl { Focusable = true };
            var second = new ProbeControl { Focusable = true };
            var third = new ProbeControl { Focusable = true };
            var final = new ProbeControl { Focusable = true };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Children.Add(third);
            root.Children.Add(final);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(first).ShouldBeTrue();
            var primary = new InvalidOperationException("primary focus failure");
            var notification = new InvalidOperationException("notification failure");
            var completion = new InvalidOperationException("completion failure");
            var notificationCalls = 0;
            var completionCalls = 0;
            var primaryCompletionCalls = 0;
            Exception? primaryOutcomeFailure = null;
            var finalCompleted = false;
            second.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlBase.CanFocus))
                {
                    notificationCalls++;
                    throw notification;
                }
            };
            manager.Gained += (_, eventArgs) =>
            {
                if (!ReferenceEquals(eventArgs.Current, second))
                {
                    return;
                }

                manager.Focus(
                    third,
                    FocusReason.Restore,
                    cancellable: false,
                    (_, _) =>
                    {
                        completionCalls++;
                        throw completion;
                    });
                manager.Focus(
                    final,
                    FocusReason.Keyboard,
                    cancellable: false,
                    (committed, failure) =>
                    {
                        failure.ShouldBeNull();
                        finalCompleted = committed;
                    });
                second.Focusable = false;
                throw primary;
            };

            var thrown = Should.Throw<InvalidOperationException>(() =>
                manager.Focus(
                    second,
                    FocusReason.Programmatic,
                    cancellable: true,
                    (committed, failure) =>
                    {
                        committed.ShouldBeFalse();
                        primaryCompletionCalls++;
                        primaryOutcomeFailure = failure;
                    }));

            thrown.ShouldBeSameAs(primary);
            primaryCompletionCalls.ShouldBe(1);
            primaryOutcomeFailure.ShouldBeSameAs(primary);
            notificationCalls.ShouldBe(1);
            completionCalls.ShouldBe(1);
            finalCompleted.ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(final);
            second.IsFocused.ShouldBeFalse();

            var afterCompleted = false;
            manager.Focus(
                first,
                FocusReason.Programmatic,
                cancellable: false,
                (committed, failure) =>
                {
                    failure.ShouldBeNull();
                    afterCompleted = committed;
                });
            afterCompleted.ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(first);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies completion-enqueued requests execute iteratively without nesting callbacks.</summary>
    [Fact]
    public async Task Focus_WhenCompletionEnqueuesRequests_PumpsWithoutCallbackNestingAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            var callbackDepth = 0;
            var maximumDepth = 0;
            var completions = 0;

            manager.Focus(
                null,
                FocusReason.Restore,
                cancellable: false,
                Complete);

            completions.ShouldBe(32);
            maximumDepth.ShouldBe(1);
            callbackDepth.ShouldBe(0);
            manager.Focused.ShouldBeNull();
            return;

            void Complete(bool committed, Exception? failure)
            {
                failure.ShouldBeNull();
                committed.ShouldBeTrue();
                callbackDepth++;
                maximumDepth = Math.Max(maximumDepth, callbackDepth);
                completions++;

                if (completions < 32)
                {
                    manager.Focus(
                        null,
                        FocusReason.Restore,
                        cancellable: false,
                        Complete);
                }

                callbackDepth--;
            }
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disposal from every focus callback stage prevents an in-flight request from surviving.</summary>
    /// <param name="callback">The callback stage that disposes the manager.</param>
    [Theory]
    [InlineData("changing")]
    [InlineData("state")]
    [InlineData("lost")]
    [InlineData("gained")]
    public async Task Focus_WhenCallbackDisposesManager_EndsDisposedAndReturnsFalseAsync(
        string callback)
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var first = new ProbeControl { Focusable = true };
            var second = new ProbeControl { Focusable = true };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            var manager = new FocusManager(root);
            manager.Focus(first).ShouldBeTrue();
            var callbacks = 0;
            var disposalRequested = false;

            void DisposeFromCallback()
            {
                if (disposalRequested)
                {
                    return;
                }

                disposalRequested = true;
                callbacks++;
                manager.Dispose();
                _ = Should.Throw<ObjectDisposedException>(() => manager.Focus(first));
            }

            manager.Changing += (_, eventArgs) =>
            {
                if (callback == "changing" && ReferenceEquals(eventArgs.Next, second))
                {
                    DisposeFromCallback();
                }
            };
            second.PropertyChanged += (_, eventArgs) =>
            {
                if (callback == "state" &&
                    eventArgs.PropertyName == nameof(ControlBase.IsFocused) &&
                    second.IsFocused)
                {
                    DisposeFromCallback();
                }
            };
            manager.Lost += (_, eventArgs) =>
            {
                if (callback == "lost" && ReferenceEquals(eventArgs.Current, second))
                {
                    DisposeFromCallback();
                }
            };
            manager.Gained += (_, eventArgs) =>
            {
                if (callback == "gained" && ReferenceEquals(eventArgs.Current, second))
                {
                    DisposeFromCallback();
                }
            };

            manager.Focus(second).ShouldBeFalse();

            callbacks.ShouldBe(1);
            manager.Focused.ShouldBeNull();
            first.IsFocused.ShouldBeFalse();
            second.IsFocused.ShouldBeFalse();
            root.FocusOwner.ShouldBeNull();
            first.FocusOwner.ShouldBeNull();
            second.FocusOwner.ShouldBeNull();
            _ = Should.Throw<ObjectDisposedException>(() => manager.Focus(first));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a throwing focus-state callback cannot skip the rest of manager disposal.</summary>
    [Fact]
    public async Task Dispose_WhenFocusedStateCallbackThrows_CompletesCleanupThenRethrowsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var focused = new ProbeControl { Focusable = true };
            root.Children.Add(focused);
            root.Attach(dispatcher);
            var manager = new FocusManager(root);
            manager.Focus(focused).ShouldBeTrue();
            var expected = new InvalidOperationException("The focus-state callback failed.");
            focused.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlBase.IsFocused) && !focused.IsFocused)
                {
                    throw expected;
                }
            };

            var thrown = Should.Throw<InvalidOperationException>(manager.Dispose);

            thrown.ShouldBeSameAs(expected);
            manager.Focused.ShouldBeNull();
            focused.IsFocused.ShouldBeFalse();
            root.FocusOwner.ShouldBeNull();
            focused.FocusOwner.ShouldBeNull();
            _ = Should.Throw<ObjectDisposedException>(() => manager.Focus(focused));
            manager.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies cancellation preserves each request's failure payload while the aggregate keeps the earliest prior.</summary>
    [Fact]
    public async Task Focus_WhenDisposalCancelsQueuedPriorFailure_KeepsCompletionPayloadsIsolatedAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var first = new ProbeControl { Focusable = true };
            var second = new ProbeControl { Focusable = true };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            var manager = new FocusManager(root);
            manager.Focus(first).ShouldBeTrue();
            var prior = new InvalidOperationException("The queued request failed earlier.");
            var priorDispatch = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(prior);
            bool? activeCommitted = null;
            Exception? activeFailure = null;
            bool? queuedCommitted = null;
            Exception? queuedFailure = null;
            manager.Changing += (_, eventArgs) =>
            {
                if (!ReferenceEquals(eventArgs.Next, second))
                {
                    return;
                }

                manager.Focus(
                    first,
                    FocusReason.Restore,
                    cancellable: false,
                    (committed, failure) =>
                    {
                        queuedCommitted = committed;
                        queuedFailure = failure;
                    },
                    priorFailure: priorDispatch);
                manager.Dispose();
            };

            var thrown = Should.Throw<InvalidOperationException>(() =>
                manager.Focus(
                    second,
                    FocusReason.Programmatic,
                    cancellable: false,
                    (committed, failure) =>
                    {
                        activeCommitted = committed;
                        activeFailure = failure;
                    }));

            thrown.ShouldBeSameAs(prior);
            activeCommitted.ShouldBe(false);
            activeFailure.ShouldBeNull();
            queuedCommitted.ShouldBe(false);
            queuedFailure.ShouldBeSameAs(prior);
            manager.Focused.ShouldBeNull();
            first.IsFocused.ShouldBeFalse();
            second.IsFocused.ShouldBeFalse();
            root.FocusOwner.ShouldBeNull();
            manager.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    #endregion

    #region Traversal and tree lifetime

    /// <summary>Verifies tab order uses index then tree order and wraps both directions.</summary>
    [Fact]
    public async Task MoveNext_WhenTreeHasFocusableControls_OrdersAndWrapsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var first = new ProbeControl { Focusable = true, TabIndex = 1 };
            var second = new ProbeControl { Focusable = true, TabIndex = 0 };
            var third = new ProbeControl { Focusable = true, TabIndex = 1 };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Children.Add(third);
            root.Attach(dispatcher);
            using FocusManager manager = new(root);

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
            var hidden = new ProbeControl { Focusable = true, Visibility = Visibility.Hidden };
            var foreign = new ProbeControl { Focusable = true };
            root.Children.Add(hidden);
            root.Attach(dispatcher);
            using FocusManager manager = new(root);

            manager.Focus(hidden).ShouldBeFalse();
            _ = Should.Throw<ArgumentException>(() => manager.Focus(foreign));
            manager.Focused.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies MoveNext wraps within a Cycle scope instead of traversing globally.</summary>
    [Fact]
    public async Task MoveNext_WhenScopeIsCycle_WrapsWithinScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var outside = new ProbeControl { Focusable = true };
            var scope = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var inner1 = new ProbeControl { Focusable = true };
            var inner2 = new ProbeControl { Focusable = true };
            scope.Children.Add(inner1);
            scope.Children.Add(inner2);
            root.Children.Add(outside);
            root.Children.Add(scope);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(inner1).ShouldBeTrue();

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(inner2);
            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(inner1);
            manager.MoveNext(reverse: true).ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(inner2);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies MoveNext traps focus within a Contained scope.</summary>
    [Fact]
    public async Task MoveNext_WhenScopeIsContained_TrapsFocusAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var outside = new ProbeControl { Focusable = true };
            var scope = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var inner1 = new ProbeControl { Focusable = true };
            var inner2 = new ProbeControl { Focusable = true };
            scope.Children.Add(inner1);
            scope.Children.Add(inner2);
            root.Children.Add(outside);
            root.Children.Add(scope);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(inner1).ShouldBeTrue();

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(inner2);
            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(inner1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an eligible empty cycle scope remains its own traversal entry.</summary>
    [Fact]
    public async Task MoveNext_WhenScopeIsEmpty_WrapsToScopeOwnerAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var scope = new ProbeContainer { Focusable = true, TabNavigation = TabNavigation.Cycle };
            root.Children.Add(scope);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(scope).ShouldBeTrue();

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(scope);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies MoveNext wraps to the same control when the scope has one tab stop.</summary>
    [Fact]
    public async Task MoveNext_WhenScopeHasSingleTabStop_WrapsToSelfAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var scope = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var only = new ProbeControl { Focusable = true };
            scope.Children.Add(only);
            root.Children.Add(scope);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(only).ShouldBeTrue();

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(only);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies nested scopes use the innermost scope for Tab traversal.</summary>
    [Fact]
    public async Task MoveNext_WhenScopesAreNested_UsesInnermostAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var outer = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var outerChild = new ProbeControl { Focusable = true };
            var inner = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var innerA = new ProbeControl { Focusable = true };
            var innerB = new ProbeControl { Focusable = true };
            inner.Children.Add(innerA);
            inner.Children.Add(innerB);
            outer.Children.Add(outerChild);
            outer.Children.Add(inner);
            root.Children.Add(outer);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(innerA).ShouldBeTrue();

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(innerB);
            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(innerA);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an eligible scope root participates between its direct children.</summary>
    [Fact]
    public async Task MoveNext_WhenScopeRootIsTabStop_IncludesRootInOwnScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var scope = new ProbeContainer { Focusable = true, TabStop = true, TabNavigation = TabNavigation.Cycle };
            var child1 = new ProbeControl { Focusable = true };
            var child2 = new ProbeControl { Focusable = true };
            scope.Children.Add(child1);
            scope.Children.Add(child2);
            root.Children.Add(scope);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(child1).ShouldBeTrue();

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(child2);
            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(scope);
            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(child1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies MoveNext from outside a Cycle scope can enter the scope.</summary>
    [Fact]
    public async Task MoveNext_WhenOutsideCycleScope_EntersScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var before = new ProbeControl { Focusable = true };
            var scope = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var inner1 = new ProbeControl { Focusable = true };
            var inner2 = new ProbeControl { Focusable = true };
            scope.Children.Add(inner1);
            scope.Children.Add(inner2);
            var after = new ProbeControl { Focusable = true };
            root.Children.Add(before);
            root.Children.Add(scope);
            root.Children.Add(after);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(before).ShouldBeTrue();

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(inner1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies MoveNext from outside a Contained scope can enter it, and Tab then traps inside.</summary>
    [Fact]
    public async Task MoveNext_WhenOutsideContainedScope_EntersAndTrapsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var before = new ProbeControl { Focusable = true };
            var scope = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var inner1 = new ProbeControl { Focusable = true };
            var inner2 = new ProbeControl { Focusable = true };
            scope.Children.Add(inner1);
            scope.Children.Add(inner2);
            var after = new ProbeControl { Focusable = true };
            root.Children.Add(before);
            root.Children.Add(scope);
            root.Children.Add(after);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(before).ShouldBeTrue();

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(inner1);

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(inner2);

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(inner1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Tab traversal visits controls, enters scopes, and exits correctly in a mixed tree.</summary>
    [Fact]
    public async Task MoveNext_WhenTreeHasMixedScopes_TraversesFullyAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var a = new ProbeControl { Focusable = true };
            var menu = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var m1 = new ProbeControl { Focusable = true };
            var m2 = new ProbeControl { Focusable = true };
            menu.Children.Add(m1);
            menu.Children.Add(m2);
            var b = new ProbeControl { Focusable = true };
            root.Children.Add(a);
            root.Children.Add(menu);
            root.Children.Add(b);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(a);

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(m1);

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(m2);

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(m1);
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
            var child = new ProbeControl { Focusable = true };
            var replacement = new ProbeControl { Focusable = true };
            root.Children.Add(child);
            root.Children.Add(replacement);
            root.Attach(dispatcher);
            using FocusManager manager = new(root);
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

    #endregion
}
