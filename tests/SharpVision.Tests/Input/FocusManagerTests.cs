// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies transactional focus, navigation, and invalid-state cleanup.</summary>
public sealed class FocusManagerTests
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
                child.Focused.ShouldBeFalse();
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
                    child.Focused.ShouldBeFalse();
                    order.Add("can-focus");
                }
            };

            child.Focusable = false;

            manager.Focused.ShouldBeNull();
            child.Focused.ShouldBeFalse();
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
                child.Focused.ShouldBeFalse();
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
                    child.Focused.ShouldBeFalse();
                    notificationCalls++;
                }
            };

            manager.Focus(child).ShouldBeTrue();
            focusReturned = true;

            manager.Focused.ShouldBeNull();
            child.Focused.ShouldBeFalse();
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
            child.Focused.ShouldBeTrue();
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
                first.Focused.ShouldBeFalse();
                second.Focused.ShouldBeTrue();
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
            first.Focused.ShouldBeTrue();
            second.Focused.ShouldBeFalse();
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
            second.Focused.ShouldBeFalse();

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
                    eventArgs.PropertyName == nameof(ControlBase.Focused) &&
                    second.Focused)
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
            first.Focused.ShouldBeFalse();
            second.Focused.ShouldBeFalse();
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
                if (eventArgs.PropertyName == nameof(ControlBase.Focused) && !focused.Focused)
                {
                    throw expected;
                }
            };

            var thrown = Should.Throw<InvalidOperationException>(manager.Dispose);

            thrown.ShouldBeSameAs(expected);
            manager.Focused.ShouldBeNull();
            focused.Focused.ShouldBeFalse();
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
            first.Focused.ShouldBeFalse();
            second.Focused.ShouldBeFalse();
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

            root.Enabled = false;
            manager.Focused.ShouldBeNull();
            child.Focused.ShouldBeFalse();
            root.Enabled = true;
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

    #region Tab navigation across nested scopes

    /// <summary>
    /// Tree: root > [A, Menu(Cycle > M1, M2, M3), B]
    /// Tab from A enters Menu at M1. Tab inside Menu cycles M1→M2→M3→M1.
    /// Shift+Tab from A wraps to B (skips Menu internals in global scope).
    /// </summary>
    [Fact]
    public async Task FullTraversal_WhenTreeHasCycleScopeBetweenControls_EntersCyclesAndExitsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var a = new ProbeControl { Focusable = true };
            var menu = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var m1 = new ProbeControl { Focusable = true };
            var m2 = new ProbeControl { Focusable = true };
            var m3 = new ProbeControl { Focusable = true };
            menu.Children.Add(m1);
            menu.Children.Add(m2);
            menu.Children.Add(m3);
            var b = new ProbeControl { Focusable = true };
            root.Children.Add(a);
            root.Children.Add(menu);
            root.Children.Add(b);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.Focus(a).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(m1);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(m2);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(m3);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(m1);

            focus.MoveNext(reverse: true).ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(m3);
            focus.MoveNext(reverse: true).ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(m2);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [A, Sidebar(Cycle > S1, S2), B, Popup(Contained > P1, P2)]
    /// Tab from A enters Sidebar. Tab from B enters Popup. Popup traps.
    /// Shift+Tab from B goes to Sidebar entry.
    /// </summary>
    [Fact]
    public async Task FullTraversal_WhenTreeHasCycleAndContained_EachScopeIsIndependentAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var a = new ProbeControl { Focusable = true };
            var sidebar = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var s1 = new ProbeControl { Focusable = true };
            var s2 = new ProbeControl { Focusable = true };
            sidebar.Children.Add(s1);
            sidebar.Children.Add(s2);
            var b = new ProbeControl { Focusable = true };
            var popup = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var p1 = new ProbeControl { Focusable = true };
            var p2 = new ProbeControl { Focusable = true };
            popup.Children.Add(p1);
            popup.Children.Add(p2);
            root.Children.Add(a);
            root.Children.Add(sidebar);
            root.Children.Add(b);
            root.Children.Add(popup);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.Focus(a).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(s1);

            focus.Focus(b).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(p1);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(p2);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(p1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [Outer(Cycle > A, Inner(Contained > X, Y), B]
    /// Tab from A enters Inner at X. Inner traps: X→Y→X.
    /// Tab from B cycles back to A in the outer scope.
    /// </summary>
    [Fact]
    public async Task NestedScopes_WhenContainedInsideCycle_InnerTrapsAndOuterCyclesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var outer = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var a = new ProbeControl { Focusable = true };
            var inner = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var x = new ProbeControl { Focusable = true };
            var y = new ProbeControl { Focusable = true };
            inner.Children.Add(x);
            inner.Children.Add(y);
            var b = new ProbeControl { Focusable = true };
            outer.Children.Add(a);
            outer.Children.Add(inner);
            outer.Children.Add(b);
            root.Children.Add(outer);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.Focus(a).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(x);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(y);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(x);

            focus.Focus(b).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(a);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [A, Scope1(Cycle > S1a, S1b), Scope2(Cycle > S2a, S2b), B]
    /// Tab from A → enters Scope1 at S1a. Tab from S1a cycles S1a→S1b→S1a.
    /// Explicit focus to B, then Shift+Tab enters Scope2 at S2a.
    /// </summary>
    [Fact]
    public async Task SiblingScopes_WhenTabTraverses_EntersEachScopeIndependentlyAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var a = new ProbeControl { Focusable = true };
            var scope1 = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var s1A = new ProbeControl { Focusable = true };
            var s1B = new ProbeControl { Focusable = true };
            scope1.Children.Add(s1A);
            scope1.Children.Add(s1B);
            var scope2 = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var s2A = new ProbeControl { Focusable = true };
            var s2B = new ProbeControl { Focusable = true };
            scope2.Children.Add(s2A);
            scope2.Children.Add(s2B);
            var b = new ProbeControl { Focusable = true };
            root.Children.Add(a);
            root.Children.Add(scope1);
            root.Children.Add(scope2);
            root.Children.Add(b);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.Focus(a).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(s1A);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(s1B);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(s1A);

            focus.Focus(b).ShouldBeTrue();
            focus.MoveNext(reverse: true).ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(s2B);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [Dock > [Top: Header(non-focusable), Fill: Scope(Cycle > I1, I2, I3)], Button]
    /// Simulates a real sidebar layout. Tab from nothing starts at I1.
    /// Inside scope cycles. Button is reachable from root scope.
    /// </summary>
    [Fact]
    public async Task RealLayout_WhenSidebarWithHeaderAndButton_TabEntersAndCyclesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var sidebarDock = new Dock();
            var header = new ProbeControl();
            Dock.SetSide(header, DockSide.Top);
            sidebarDock.Children.Add(header);
            var navScope = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var i1 = new ProbeControl { Focusable = true };
            var i2 = new ProbeControl { Focusable = true };
            var i3 = new ProbeControl { Focusable = true };
            navScope.Children.Add(i1);
            navScope.Children.Add(i2);
            navScope.Children.Add(i3);
            sidebarDock.Children.Add(navScope);
            var button = new ProbeControl { Focusable = true };
            root.Children.Add(sidebarDock);
            root.Children.Add(button);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(i1);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(i2);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(i3);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(i1);

            focus.Focus(button).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(i1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [Scope(Cycle > Disabled, Hidden, Visible)]
    /// Only Visible is eligible. Tab enters scope at Visible.
    /// Inside scope, single tab stop wraps to itself.
    /// </summary>
    [Fact]
    public async Task ScopeEntry_WhenFirstChildrenAreIneligible_SkipsToFirstEligibleAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var before = new ProbeControl { Focusable = true };
            var scope = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var disabled = new ProbeControl { Focusable = true, Enabled = false };
            var hidden = new ProbeControl { Focusable = true, Visibility = Visibility.Hidden };
            var visible = new ProbeControl { Focusable = true };
            scope.Children.Add(disabled);
            scope.Children.Add(hidden);
            scope.Children.Add(visible);
            root.Children.Add(before);
            root.Children.Add(scope);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.Focus(before).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(visible);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [A, EmptyScope(Cycle, no children), B]
    /// Empty scopes are skipped entirely. Tab goes A→B.
    /// </summary>
    [Fact]
    public async Task ScopeEntry_WhenScopeIsEmpty_SkipsToNextControlAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var a = new ProbeControl { Focusable = true };
            var empty = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var b = new ProbeControl { Focusable = true };
            root.Children.Add(a);
            root.Children.Add(empty);
            root.Children.Add(b);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.Focus(a).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(b);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [Scope(Cycle > [Nested(Continue > D1, D2), E])]
    /// A Continue scope inside a Cycle scope is transparent.
    /// Tab cycles D1→D2→E→D1.
    /// </summary>
    [Fact]
    public async Task ContinueInsideCycle_WhenTraversed_IsFlattenedAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var cycle = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var group = new ProbeContainer();
            var d1 = new ProbeControl { Focusable = true };
            var d2 = new ProbeControl { Focusable = true };
            group.Children.Add(d1);
            group.Children.Add(d2);
            var e = new ProbeControl { Focusable = true };
            cycle.Children.Add(group);
            cycle.Children.Add(e);
            root.Children.Add(cycle);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.Focus(d1).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(d2);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(e);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(d1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [A, Scope(Cycle > TabIndex=2:Z, TabIndex=1:Y, TabIndex=0:X), B]
    /// Entry from outside goes to first by tree order (X after sorting by TabIndex).
    /// Inside scope, cycles X→Y→Z→X (respecting TabIndex).
    /// </summary>
    [Fact]
    public async Task ScopeWithTabIndex_WhenEntered_UsesTabIndexOrderInsideScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var a = new ProbeControl { Focusable = true };
            var scope = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var z = new ProbeControl { Focusable = true, TabIndex = 2 };
            var y = new ProbeControl { Focusable = true, TabIndex = 1 };
            var x = new ProbeControl { Focusable = true, TabIndex = 0 };
            scope.Children.Add(z);
            scope.Children.Add(y);
            scope.Children.Add(x);
            var b = new ProbeControl { Focusable = true };
            root.Children.Add(a);
            root.Children.Add(scope);
            root.Children.Add(b);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.Focus(a).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(x);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(y);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(z);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(x);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [Lvl1(Cycle > Lvl2(Cycle > Lvl3(Contained > X, Y)))]
    /// Three levels deep. Tab from outside enters through Lvl1→Lvl2→Lvl3 entry point.
    /// Inside Lvl3 traps: X→Y→X.
    /// </summary>
    [Fact]
    public async Task DeeplyNested_WhenThreeLevelsDeep_EntersThroughAllLevelsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var before = new ProbeControl { Focusable = true };
            var lvl1 = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var lvl2 = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var lvl3 = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var x = new ProbeControl { Focusable = true };
            var y = new ProbeControl { Focusable = true };
            lvl3.Children.Add(x);
            lvl3.Children.Add(y);
            lvl2.Children.Add(lvl3);
            lvl1.Children.Add(lvl2);
            root.Children.Add(before);
            root.Children.Add(lvl1);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.Focus(before).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(x);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(y);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(x);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [CanTabStop=false:Panel > [A, B, C]]
    /// A container with CanTabStop=false but no scope. Children are reachable. Tab cycles A→B→C→A.
    /// </summary>
    [Fact]
    public async Task NonTabStopContainer_WhenContinue_ChildrenAreReachableAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var panel = new ProbeContainer { TabStop = false };
            var a = new ProbeControl { Focusable = true };
            var b = new ProbeControl { Focusable = true };
            var c = new ProbeControl { Focusable = true };
            panel.Children.Add(a);
            panel.Children.Add(b);
            panel.Children.Add(c);
            root.Children.Add(panel);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(a);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(b);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(c);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(a);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [A, B, Skipped(TabStop=false, focusable), C, D]
    /// Skipped is focused by a pointer click (CanFocus only), not by MoveNext. Before an earlier
    /// fix, an anchor with no candidate-list position folded into the wrap branch, sending Tab to
    /// the scope's first candidate and Shift+Tab to its last regardless of where the anchor
    /// actually was. Tab and Shift+Tab from Skipped must instead be exact inverses that resolve
    /// by tree order: forward to the nearest following candidate, backward to the nearest
    /// preceding one.
    /// </summary>
    [Fact]
    public async Task MoveNext_WhenAnchorIsFocusableButNotATabStop_ResolvesByTreeOrderAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var a = new ProbeControl { Focusable = true };
            var b = new ProbeControl { Focusable = true };
            var skipped = new ProbeControl { Focusable = true, TabStop = false };
            var c = new ProbeControl { Focusable = true };
            var d = new ProbeControl { Focusable = true };
            root.Children.Add(a);
            root.Children.Add(b);
            root.Children.Add(skipped);
            root.Children.Add(c);
            root.Children.Add(d);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            focus.Focus(skipped).ShouldBeTrue();

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(c);

            focus.Focus(skipped).ShouldBeTrue();
            focus.MoveNext(reverse: true).ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(b);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [A, None(TabNavigation.None, focusable) > Inner(focusable), B]
    /// Inner is focused directly (e.g. by pointer click), even though None excludes it from
    /// traversal. Before the same earlier fix, this anchor also folded into the wrap branch. Tab and
    /// Shift+Tab from Inner must resolve to the candidates immediately after and before the
    /// excluding None container itself.
    /// </summary>
    [Fact]
    public async Task MoveNext_WhenAnchorIsExcludedByAncestorTabNavigationNone_ResolvesByTreeOrderAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var a = new ProbeControl { Focusable = true };
            var excluded = new ProbeContainer { TabNavigation = TabNavigation.None, Focusable = true };
            var inner = new ProbeControl { Focusable = true };
            excluded.Children.Add(inner);
            var b = new ProbeControl { Focusable = true };
            root.Children.Add(a);
            root.Children.Add(excluded);
            root.Children.Add(b);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            focus.Focus(inner).ShouldBeTrue();

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(b);

            focus.Focus(inner).ShouldBeTrue();
            focus.MoveNext(reverse: true).ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(excluded);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [Before, Once(TabNavigation.Once > Inner1, Inner2), After]
    /// Before a related earlier fix, FindScope treated Once as a traversal scope root, so MoveNext
    /// collected only Once's single contributed entry and both Tab and Shift+Tab resolved to it
    /// forever. Once is a contribution rule, not a boundary: Tab and Shift+Tab must traverse the
    /// enclosing scope normally, with Once contributing exactly its first eligible descendant.
    /// </summary>
    [Fact]
    public async Task MoveNext_WhenScopeContainsOnce_DoesNotTrapTraversalOnTheContributedEntryAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var before = new ProbeControl { Focusable = true };
            var once = new ProbeContainer { TabNavigation = TabNavigation.Once };
            var inner1 = new ProbeControl { Focusable = true };
            var inner2 = new ProbeControl { Focusable = true };
            once.Children.Add(inner1);
            once.Children.Add(inner2);
            var after = new ProbeControl { Focusable = true };
            root.Children.Add(before);
            root.Children.Add(once);
            root.Children.Add(after);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.Focus(before).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(inner1);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(after);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(before);

            focus.MoveNext(reverse: true).ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(after);
            focus.MoveNext(reverse: true).ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(inner1);
            focus.MoveNext(reverse: true).ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(before);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [Before, Leaf(TabNavigation.Once, childless focusable), After]
    /// The most alarming shape of that same defect: Once on a childless leaf with no descendants to contribute.
    /// Before the fix, Tab and Shift+Tab both trapped on Leaf forever; Leaf must instead be one
    /// ordinary stop between Before and After.
    /// </summary>
    [Fact]
    public async Task MoveNext_WhenChildlessLeafHasOnce_IsOneOrdinaryStopAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var before = new ProbeControl { Focusable = true };
            var leaf = new ProbeControl { Focusable = true, TabNavigation = TabNavigation.Once };
            var after = new ProbeControl { Focusable = true };
            root.Children.Add(before);
            root.Children.Add(leaf);
            root.Children.Add(after);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.Focus(before).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(leaf);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(after);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(before);
        }, TestContext.Current.CancellationToken);
    }

    #endregion
}
