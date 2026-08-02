// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;

/// <summary>Defines the shared typed-dialog inheritance and identity contract.</summary>
public sealed class DialogTests
{
    /// <summary>Verifies the shared dialog base is a direct Window specialization.</summary>
    [Fact]
    public void Type_WhenInspected_IsTypedWindowSurface()
    {
        var type = typeof(Dialog<>);

        type.IsAbstract.ShouldBeTrue();
        type.BaseType.ShouldBe(typeof(Window));
        type.Namespace.ShouldBe("SharpVision.Dialogs");
    }

    /// <summary>Verifies concrete dialogs inherit the shared window identity without nested proxies.</summary>
    [Fact]
    public void ConcreteDialogs_WhenInspected_OwnOnlyTheirWindowIdentity()
    {
        using var picker = new FilePickerDialog();
        using var messageBox = new MessageBox("Ready.", "Status");

        typeof(Window).IsAssignableFrom(typeof(FilePickerDialog)).ShouldBeTrue();
        typeof(MessageBox).BaseType.ShouldBe(typeof(Dialog<MessageBoxResult>));
        OwnedTree.FindAll<Window>(picker).ShouldBe([picker]);
        OwnedTree.FindAll<Window>(messageBox).ShouldBe([messageBox]);
    }

    /// <summary>Verifies concrete policy overrides retain shared Escape and title-placement defaults.</summary>
    [Fact]
    public void Constructor_WhenMessageBoxOverridesChrome_RetainsSharedDialogPolicies()
    {
        using var messageBox = new MessageBox("Ready.", "Status");

        messageBox.CanMove.ShouldBeTrue();
        messageBox.CanClose.ShouldBeFalse();
        messageBox.CloseOnEscape.ShouldBeTrue();
        messageBox.HeaderPlacement.ShouldBe(WindowTitlePlacement.Center);
    }

    /// <summary>Verifies presentation, modality, host ownership, and disposal retain one dialog identity.</summary>
    [Fact]
    public async Task ShowAsync_WhenDialogCompletes_UsesOneIdentityAndDisposesAfterRemovalAsync()
    {
        var opener = new Button { Content = new ControlText("Open") };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(48, 14),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;

        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "Ready.", "Status"),
            "present direct dialog identity");
        var dialog = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        scope.Root.ShouldBeSameAs(dialog);
        _ = dialog.Parent.ShouldBeOfType<Overlay>();
        OwnedTree.FindAll<Window>(dialog).ShouldBe([dialog]);

        await surface.Keyboard.PressAsync(Code.Enter);

        (await pending!).ShouldBe(MessageBoxResult.Ok);
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        dialog.Parent.ShouldBeNull();
        dialog.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies explicit disposal settles cancellation and releases host and modality ownership.</summary>
    [Fact]
    public async Task Dispose_WhenDialogIsPending_CompletesCancellationWithoutLeaksAsync()
    {
        var opener = new Button { Content = new ControlText("Open") };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(48, 14),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "Ready.", "Status"),
            "present disposable dialog");
        var dialog = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        await surface.UpdateAsync(dialog.Dispose, "dispose pending dialog");

        (await pending!).ShouldBe(MessageBoxResult.Cancel);
        dialog.IsDisposed.ShouldBeTrue();
        dialog.Parent.ShouldBeNull();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        host.Children.ShouldBe([opener]);
    }

    /// <summary>Verifies unwinding an older modal scope from underneath a nested dialog settles it
    /// with cancellation instead of leaving it attached, painted, no longer modal, and awaiting
    /// forever - ModalityManager.Exit unwinds every younger scope in reverse order when an older
    /// one is exited, but the dialog previously never observed its own scope's Exited event
    /// (see #207).</summary>
    [Fact]
    public async Task ShowAsync_WhenOlderScopeUnwindsFromUnderneath_SettlesWithCancellationAsync()
    {
        var panel = new ProbeContainer { Focusable = true };
        var host = new Overlay { Children = { panel } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(48, 14),
            TestContext.Current.CancellationToken);
        ModalScope? outerScope = null;
        await surface.UpdateAsync(
            () => outerScope = surface.Application.EnterModal(panel),
            "enter outer modal scope");

        Task<MessageBoxResult>? pending = null;
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(panel, "Overwrite?", "Confirm"),
            "present nested dialog");
        var dialog = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        _ = surface.Application.Modality.Active.ShouldNotBeNull();

        await surface.UpdateAsync(outerScope!.Dispose, "dispose outer scope");

        (await pending!).ShouldBe(MessageBoxResult.Cancel);
        dialog.IsDisposed.ShouldBeTrue();
        dialog.Parent.ShouldBeNull();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies modal-entry failure removes and disposes the provisional dialog without replacing the failure.</summary>
    [Fact]
    public async Task ShowAsync_WhenModalEntryFails_RollsBackHostAndPreservesFailureAsync()
    {
        var expected = new InvalidOperationException("Dialog focus failed.");
        var opener = new Button { Content = new ControlText("Open") };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(48, 14),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(opener).ShouldBeTrue(),
            "focus dialog opener");
        surface.Application.Focus.Gained += ThrowForDialogFocus;
        InvalidOperationException? observed = null;

        await surface.UpdateAsync(
            () => observed = Should.Throw<InvalidOperationException>(() =>
                MessageBox.ShowAsync(opener, "Ready.", "Status")),
            "reject failed dialog modal entry");

        observed.ShouldBeSameAs(expected);
        surface.Application.Modality.Active.ShouldBeNull();
        OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldBeNull();
        host.Children.ShouldBe([opener]);
        opener.IsFocused.ShouldBeTrue();

        void ThrowForDialogFocus(object? sender, FocusChangedEventArgs eventArgs)
        {
            _ = sender;

            for (var current = eventArgs.Current; current is not null; current = current.Parent)
            {
                if (current is MessageBox)
                {
                    throw expected;
                }
            }
        }
    }

    /// <summary>Verifies synchronously invalidated modal entry cannot leave a hosted dialog with a pending result.</summary>
    [Fact]
    public async Task ShowAsync_WhenModalEntryIsInvalidated_RollsBackInsteadOfLeakingPendingTaskAsync()
    {
        var opener = new Button { Content = new ControlText("Open") };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(48, 14),
            TestContext.Current.CancellationToken);
        surface.Application.Focus.Gained += DisposeDialogScope;
        InvalidOperationException? observed = null;

        await surface.UpdateAsync(
            () => observed = Should.Throw<InvalidOperationException>(() =>
                MessageBox.ShowAsync(opener, "Ready.", "Status")),
            "roll back invalidated dialog modal entry");

        observed.ShouldNotBeNull().Message.ShouldContain("active modal");
        surface.Application.Modality.Active.ShouldBeNull();
        OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldBeNull();
        host.Children.ShouldBe([opener]);

        void DisposeDialogScope(object? sender, FocusChangedEventArgs eventArgs)
        {
            _ = sender;

            for (var current = eventArgs.Current; current is not null; current = current.Parent)
            {
                if (current is MessageBox)
                {
                    surface.Application.Modality.Active.ShouldNotBeNull().Dispose();
                    return;
                }
            }
        }
    }

    /// <summary>Verifies external host removal completes semantic cancellation after structural publication.</summary>
    [Fact]
    public async Task HostRemoval_WhenDialogIsPending_CancelsDisposesAndReleasesModalityAsync()
    {
        var opener = new Button { Content = new ControlText("Open") };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(48, 14),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "Ready.", "Status"),
            "present externally removable dialog");
        var dialog = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        var presentation = dialog.Parent.ShouldBeOfType<Overlay>();
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        await surface.UpdateAsync(
            () => presentation.Children.Remove(dialog).ShouldBeTrue(),
            "remove pending dialog from host");

        (await pending!).ShouldBe(MessageBoxResult.Cancel);
        dialog.Parent.ShouldBeNull();
        dialog.IsDisposed.ShouldBeTrue();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies disposal after result selection cannot replace the already committed result.</summary>
    [Fact]
    public async Task Dispose_WhenSelectedResultIsQueued_PreservesSelectionAndSettlesAfterDisposalAsync()
    {
        var opener = new Button { Content = new ControlText("Open") };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(48, 14),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "Ready.", "Status"),
            "present selectable dialog");
        var dialog = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        var ok = OwnedTree.FindAll<Button>(dialog).Single(static candidate => candidate.IsDefault);

        await surface.UpdateAsync(
            () =>
            {
                ok.PerformClick();
                dialog.Dispose();
            },
            "dispose after selecting dialog result");

        (await pending!).ShouldBe(MessageBoxResult.Ok);
        dialog.IsDisposed.ShouldBeTrue();
        dialog.Parent.ShouldBeNull();
    }

    /// <summary>Verifies full-queue completion ownership is canceled by disposal before the next idle boundary.</summary>
    [Fact]
    public async Task Dispose_WhenCompletionQueueIsFull_CancelsIdleScheduleAndSettlesBeforeShutdownAsync()
    {
        var opener = new Button { Content = new ControlText("Open") };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(48, 14),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "Ready.", "Status"),
            "present queue-pressure dialog");
        var dialog = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        var ok = OwnedTree.FindAll<Button>(dialog).Single(static candidate => candidate.IsDefault);
        var schedulePendingAtIdle = true;
        surface.Application.Dispatcher.Idle += ObserveIdle;

        try
        {
            await surface.UpdateAsync(
                () =>
                {
                    for (var index = 0; index < 4096; index++)
                    {
                        surface.Application.Dispatcher.Post(static () => { });
                    }

                    ok.PerformClick();
                    dialog.HasPendingCompletionSchedule.ShouldBeTrue();
                    dialog.Dispose();
                    dialog.HasPendingCompletionSchedule.ShouldBeFalse();
                },
                "dispose dialog with full completion queue");
        }
        finally
        {
            surface.Application.Dispatcher.Idle -= ObserveIdle;
        }

        (await pending!).ShouldBe(MessageBoxResult.Ok);
        schedulePendingAtIdle.ShouldBeFalse();
        dialog.IsDisposed.ShouldBeTrue();
        dialog.Parent.ShouldBeNull();
        host.Children.ShouldBe([opener]);

        void ObserveIdle(object? sender, EventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            schedulePendingAtIdle = dialog.HasPendingCompletionSchedule;
        }
    }

    /// <summary>Verifies completing a dialog whose owning dispatcher has already begun
    /// stopping settles the result task directly instead of leaving it to hang forever,
    /// since the deferred completion Post that would normally settle it is rejected.</summary>
    [Fact]
    public async Task Complete_WhenOwningDispatcherIsStopping_SettlesInsteadOfHangingAsync()
    {
        var opener = new Button { Content = new ControlText("Open") };
        var host = new Overlay { Children = { opener } };
        var surface = await ComponentSurface.MountAsync(
            host,
            new Size(48, 14),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;

        try
        {
            await surface.UpdateAsync(
                () => pending = MessageBox.ShowAsync(opener, "Ready.", "Status"),
                "present dialog to complete during dispatcher shutdown");
            var dialog = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
            var ok = OwnedTree.FindAll<Button>(dialog).Single(static candidate => candidate.IsDefault);

            // Invoked directly rather than through surface.UpdateAsync: that helper awaits
            // the next Application.Idle after the callback runs, which never arrives once
            // this callback kills the dispatcher partway through.
            await surface.Application.Dispatcher.InvokeAsync(
                () =>
                {
                    // Dispatcher.DisposeAsync sets its stopping flag synchronously before
                    // anything it owns actually tears down; calling it without awaiting from
                    // its own thread reproduces that exact window deterministically instead
                    // of racing a real shutdown against this click.
                    _ = surface.Application.Dispatcher.DisposeAsync().AsTask();

                    Should.NotThrow(ok.PerformClick);
                },
                TestContext.Current.CancellationToken);

            var result = await pending!.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            result.ShouldBe(MessageBoxResult.Ok);
        }
        finally
        {
            // The dispatcher was already force-disposed above to deterministically
            // reproduce the stopping race, so the surface's own shutdown — which posts
            // through that same now-disposed dispatcher — is expected to fail here too.
            try
            {
                await surface.DisposeAsync();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    /// <summary>Verifies successful completion publishes the common close lifecycle before task settlement.</summary>
    [Fact]
    public async Task Complete_WhenDialogIsPresented_PublishesOrderedCloseLifecycleAsync()
    {
        var opener = new Button { Content = new ControlText("Open") };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(48, 14),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "Ready.", "Status"),
            "present lifecycle dialog");
        var dialog = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        var order = new List<string>();
        await surface.UpdateAsync(
            () =>
            {
                dialog.Closing += (_, _) =>
                {
                    order.Add("closing");
                    dialog.SurfaceBounds.ShouldNotBe(default);
                    scope.IsActive.ShouldBeTrue();
                    _ = dialog.Content.ShouldNotBeNull();
                };
                dialog.Closed += (_, _) =>
                {
                    order.Add("closed");
                    dialog.SurfaceBounds.ShouldBe(default);
                    scope.IsActive.ShouldBeFalse();
                    dialog.Parent.ShouldBeNull();
                    dialog.IsDisposed.ShouldBeFalse();
                };
            },
            "observe dialog close lifecycle");

        await surface.Keyboard.PressAsync(Code.Enter);

        (await pending!).ShouldBe(MessageBoxResult.Ok);
        order.ShouldBe(["closing", "closed"]);
        dialog.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies an observed Escape close request does not duplicate Closing before Closed.</summary>
    [Fact]
    public async Task Escape_WhenDialogIsPresented_PublishesOneClosingAndOneClosedAsync()
    {
        var opener = new Button { Content = new ControlText("Open") };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(48, 14),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "Ready.", "Status"),
            "present dismissible lifecycle dialog");
        var dialog = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        var closing = 0;
        var closed = 0;
        await surface.UpdateAsync(
            () =>
            {
                dialog.Closing += (_, _) => closing++;
                dialog.Closed += (_, _) => closed++;
            },
            "observe dismissed dialog lifecycle");

        await surface.Keyboard.PressAsync(Code.Escape);

        (await pending!).ShouldBe(MessageBoxResult.Cancel);
        closing.ShouldBe(1);
        closed.ShouldBe(1);
        dialog.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies the Control-based PresentAsync overload throws and leaves the dialog
    /// unattached and undisposed when the owner has no presentation host (see #77).</summary>
    [Fact]
    public async Task PresentAsync_WhenOwnerHasNoPresentationHost_ThrowsAndLeavesTheDialogUnattachedAndUndisposedAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var owner = new Button { Content = new ControlText("Bare") };
            owner.Attach(dispatcher, Policy.Default, Capabilities.Conservative);
            using var dialog = new TestDialog();

            var exception = Should.Throw<ArgumentException>(
                () => dialog.Present(owner, initialFocus: null, CancellationToken.None));

            exception.ParamName.ShouldBe("owner");
            dialog.Parent.ShouldBeNull();
            dialog.IsDisposed.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a repeated PresentAsync call on an already-presented dialog throws without
    /// disturbing the still-pending first presentation (see #77).</summary>
    [Fact]
    public async Task PresentAsync_WhenCalledTwiceOnTheSameDialog_ThrowsAndLeavesTheFirstTaskPendingAsync()
    {
        var opener = new Button { Content = new ControlText("Open") };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(48, 14),
            TestContext.Current.CancellationToken);
        var dialog = new TestDialog();
        Task<bool>? first = null;

        await surface.UpdateAsync(
            () =>
            {
                first = dialog.Present(opener, initialFocus: null, CancellationToken.None);

                _ = Should.Throw<InvalidOperationException>(
                    () => dialog.Present(opener, initialFocus: null, CancellationToken.None));
            },
            "present the same dialog twice");

        first!.IsCompleted.ShouldBeFalse();
        _ = dialog.Parent.ShouldNotBeNull();
        dialog.IsDisposed.ShouldBeFalse();
    }

    /// <summary>Verifies external cancellation of the token passed to the Control-based PresentAsync
    /// overload cancels the task and releases modality (see #77).</summary>
    [Fact]
    public async Task PresentAsync_WhenExternalDialogIsCancelledByToken_CancelsTaskAndReleasesModalityAsync()
    {
        var opener = new Button { Content = new ControlText("Open") };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(48, 14),
            TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        Task<bool>? pending = null;

        await surface.UpdateAsync(
            () => pending = new TestDialog().Present(opener, initialFocus: null, cancellation.Token),
            "present a token-cancellable dialog");
        var dialog = OwnedTree.Find<TestDialog>(surface.Application.Root).ShouldNotBeNull();
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        scope.Root.ShouldBeSameAs(dialog);

        await cancellation.CancelAsync();
        await surface.UpdateAsync(static () => { }, "settle the cancelled dialog");

        _ = await Should.ThrowAsync<TaskCanceledException>(async () => await pending!);
        surface.Application.Modality.Active.ShouldBeNull();
        dialog.Parent.ShouldBeNull();
        dialog.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies a dialog presented through the Control-based overload returns its typed
    /// result and disposes after host removal (see #77).</summary>
    [Fact]
    public async Task PresentAsync_WhenExternalDialogCompletes_ReturnsTypedResultAndDisposesAfterHostRemovalAsync()
    {
        var opener = new Button { Content = new ControlText("Open") };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(48, 14),
            TestContext.Current.CancellationToken);
        Task<bool>? pending = null;
        TestDialog? dialog = null;

        await surface.UpdateAsync(
            () =>
            {
                dialog = new TestDialog();
                pending = dialog.Present(opener, initialFocus: null, CancellationToken.None);
            },
            "present a dialog through the Control-based overload");
        await surface.UpdateAsync(() => dialog!.Accept(true), "accept the presented dialog");

        (await pending!).ShouldBeTrue();
        dialog!.Parent.ShouldBeNull();
        dialog.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies a dialog presented through the Control-based overload sets completion state
    /// correctly, so Escape completes it with the cancelled result instead of silently bubbling as
    /// modeless input would (see #77).</summary>
    [Fact]
    public async Task Escape_WhenExternalDialogIsPresentedThroughPresentAsync_CompletesWithTheCancelledResultAndDoesNotBubbleAsync()
    {
        var opener = new Button { Content = new ControlText("Open") };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(48, 14),
            TestContext.Current.CancellationToken);
        Task<bool>? pending = null;

        await surface.UpdateAsync(
            () => pending = new TestDialog().Present(opener, initialFocus: null, CancellationToken.None),
            "present a dialog through the Control-based overload");

        await surface.Keyboard.PressAsync(Code.Escape);

        (await pending!).ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>A minimal Dialog subclass exposing the protected Control-based PresentAsync overload
    /// and Complete/Cancel to same-assembly tests, mirroring the shape of an externally-derived
    /// dialog without requiring the packed-package consumer harness for these behavioral checks.</summary>
    private sealed class TestDialog: Dialog<bool>
    {
        public TestDialog() : base(cancelledResult: false) =>
            Content = new ControlText("Test dialog");

        public Task<bool> Present(Control owner, Control? initialFocus, CancellationToken cancellationToken) =>
            PresentAsync(owner, initialFocus, cancellationToken);

        public bool Accept(bool result) => Complete(result);
    }
}
