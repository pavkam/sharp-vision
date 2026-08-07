// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Windows;

/// <summary>Verifies modal Window presentation lifetime, focus, dismissal, and failure recovery.</summary>
public sealed class WindowModalityTests
{
    #region Presentation lifetime

    /// <summary>Verifies the default policy ignores outside input and one Window cannot own two live presentations.</summary>
    [Fact]
    public async Task ShowModal_WhenPresentationIsAlreadyLive_DefaultsToIgnoreAndRejectsDuplicateAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { Focusable = true };
            var window = new Window { Content = action, Visibility = Visibility.Collapsed };
            var root = new Overlay { Children = { window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = window.ShowModal();

            var exception = Should.Throw<InvalidOperationException>(() => window.ShowModal());

            exception.Message.ShouldBe("The Window already has an active modal presentation.");
            scope.OutsideInteraction.ShouldBe(OutsideInteraction.Ignore);
            scope.Active.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(scope);
            window.Visibility.ShouldBe(Visibility.Visible);
            focus.Focused.ShouldBeSameAs(action);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a duplicate attempted from modal-entry callbacks cannot disturb the entering presentation.</summary>
    [Fact]
    public async Task ShowModal_WhenFocusCallbackReenters_RejectsNestedCallAndKeepsOuterPresentationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { Focusable = true };
            var window = new Window { Content = action, Visibility = Visibility.Hidden };
            var root = new Overlay { Children = { window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            InvalidOperationException? nested = null;
            focus.Gained += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Current, action))
                {
                    nested = Should.Throw<InvalidOperationException>(() => window.ShowModal());
                }
            };

            using var scope = window.ShowModal();

            nested.ShouldNotBeNull().Message.ShouldBe("Window modal presentations cannot be reentered.");
            scope.Active.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(scope);
            window.Visibility.ShouldBe(Visibility.Visible);
            focus.Focused.ShouldBeSameAs(action);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies external disposal ends only modality and permits another presentation of the visible Window.</summary>
    [Fact]
    public async Task ShowModal_WhenScopeIsDisposedExternally_LeavesWindowVisibleAndAllowsReopenAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { Focusable = true };
            var window = new Window { Content = action };
            var root = new Overlay { Children = { window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);

            var first = window.ShowModal(initialFocus: action);
            first.Dispose();

            first.Active.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            window.Visibility.ShouldBe(Visibility.Visible);

            using var second = window.ShowModal(initialFocus: action);

            second.ShouldNotBeSameAs(first);
            second.Active.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(second);
            window.Visibility.ShouldBe(Visibility.Visible);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an exit callback may reopen without old-scope cleanup erasing the replacement.</summary>
    [Fact]
    public async Task ShowModal_WhenExternalExitCallbackReopens_TracksReplacementByIdentityAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { Focusable = true };
            var window = new Window { Content = action };
            var root = new Overlay { Children = { window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var first = window.ShowModal();
            ModalScope? replacement = null;
            first.Exited += (_, _) => replacement = window.ShowModal();

            first.Dispose();

            first.Active.ShouldBeFalse();
            replacement.ShouldNotBeNull().Active.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(replacement);
            window.Visibility.ShouldBe(Visibility.Visible);
            replacement.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a scope disposed from entry callbacks is returned inactive without stale Window tracking.</summary>
    [Fact]
    public async Task ShowModal_WhenEntryCallbackDisposesScope_ReturnsInactiveAndAllowsReopenAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { Focusable = true };
            var window = new Window { Content = action, Visibility = Visibility.Collapsed };
            var root = new Overlay { Children = { window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var disposeOnEntry = true;
            focus.Gained += (_, eventArgs) =>
            {
                if (disposeOnEntry && ReferenceEquals(eventArgs.Current, action))
                {
                    modality.Active.ShouldNotBeNull().Dispose();
                }
            };

            var first = window.ShowModal();

            first.Active.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            window.Visibility.ShouldBe(Visibility.Visible);
            disposeOnEntry = false;

            using var second = window.ShowModal();

            second.Active.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(second);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a Window hidden from entry callbacks returns an inactive, untracked presentation.</summary>
    [Fact]
    public async Task ShowModal_WhenEntryCallbackHidesWindow_ReturnsInactiveWithoutRestoringVisibilityAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { Focusable = true };
            var action = new ProbeControl { Focusable = true };
            var window = new Window { Content = action, Visibility = Visibility.Collapsed };
            var root = new Overlay { Children = { background, window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            focus.Gained += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Current, action))
                {
                    window.Visibility = Visibility.Hidden;
                }
            };

            var scope = window.ShowModal();

            scope.Active.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            window.Visibility.ShouldBe(Visibility.Hidden);
            focus.Focused.ShouldBeSameAs(background);
        }, TestContext.Current.CancellationToken);
    }

    #endregion

    #region Visibility, focus, and failure recovery

    /// <summary>Verifies hiding or collapsing a modal Window exits before its visibility notification.</summary>
    [Theory]
    [InlineData(Visibility.Hidden)]
    [InlineData(Visibility.Collapsed)]
    public async Task Visibility_WhenModalWindowBecomesUnavailable_ExitsAndRestoresBeforeNotificationAsync(
        Visibility visibility)
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { Focusable = true };
            var action = new ProbeControl { Focusable = true };
            var window = new Window { Content = action };
            var root = new Overlay { Children = { background, window } };
            new LayoutEngine().Layout(root, new Size(24, 10));
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            var scope = window.ShowModal(initialFocus: action);
            var observations = 0;
            var closing = 0;
            var closed = 0;
            var presentedBounds = window.SurfaceBounds;
            window.Closing += (_, _) => closing++;
            window.Closed += (_, _) => closed++;
            window.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlBase.Visibility) &&
                    window.Visibility != Visibility.Visible)
                {
                    observations++;
                    scope.Active.ShouldBeFalse();
                    modality.Active.ShouldBeNull();
                    focus.Focused.ShouldBeSameAs(background);
                }
            };

            window.Visibility = visibility;

            observations.ShouldBe(1);
            scope.Active.ShouldBeFalse();
            window.Visibility.ShouldBe(visibility);
            focus.Focused.ShouldBeSameAs(background);
            window.SurfaceBounds.ShouldBe(default);
            closing.ShouldBe(0);
            closed.ShouldBe(0);

            window.Visibility = Visibility.Visible;

            window.SurfaceBounds.ShouldBe(presentedBounds);
            modality.Active.ShouldBeNull();
            closing.ShouldBe(0);
            closed.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies forced detachment silently clears Window presentation and modality.</summary>
    [Fact]
    public async Task Detach_WhenModalWindowIsRemoved_DoesNotPublishCloseLifecycleAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var window = new Window { Width = Length.Cells(12), Height = Length.Cells(5) };
            using var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(24, 10));
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var closing = 0;
            var closed = 0;
            window.Closing += (_, _) => closing++;
            window.Closed += (_, _) => closed++;
            var scope = window.ShowModal();

            root.Children.Remove(window).ShouldBeTrue();

            closing.ShouldBe(0);
            closed.ShouldBe(0);
            scope.Active.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            window.Dispatcher.ShouldBeNull();
            window.SurfaceBounds.ShouldBe(default);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies forced disposal silently clears Window presentation and modality.</summary>
    [Fact]
    public async Task Dispose_WhenModalWindowIsDisposed_DoesNotPublishCloseLifecycleAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var window = new Window { Width = Length.Cells(12), Height = Length.Cells(5) };
            using var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(24, 10));
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var closing = 0;
            var closed = 0;
            window.Closing += (_, _) => closing++;
            window.Closed += (_, _) => closed++;
            var scope = window.ShowModal();

            window.Dispose();

            closing.ShouldBe(0);
            closed.ShouldBe(0);
            scope.Active.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            window.Disposed.ShouldBeTrue();
            window.SurfaceBounds.ShouldBe(default);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a failing modal exit cannot suppress visibility publication or replace its first failure.</summary>
    [Fact]
    public async Task Visibility_WhenModalExitCallbackFails_CompletesTransitionAndPreservesFailureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var expected = new InvalidOperationException("The modal exit callback failed.");
            var action = new ProbeControl { Focusable = true };
            var window = new Window { Content = action };
            var root = new Overlay { Children = { window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var scope = window.ShowModal();
            scope.Exited += (_, _) => throw expected;
            var published = 0;
            window.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlBase.Visibility))
                {
                    published++;
                    scope.Active.ShouldBeFalse();
                    modality.Active.ShouldBeNull();
                }
            };

            var exception = Should.Throw<InvalidOperationException>(() =>
                window.Visibility = Visibility.Collapsed);

            exception.ShouldBeSameAs(expected);
            published.ShouldBe(1);
            window.Visibility.ShouldBe(Visibility.Collapsed);
            scope.Active.ShouldBeFalse();
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies explicit modal focus bypasses the legacy first-descendant visibility autofocus.</summary>
    [Fact]
    public async Task ShowModal_WhenInitialFocusIsProvided_FocusesOnlyThatDescendantAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { Focusable = true };
            var first = new ProbeControl { Focusable = true };
            var second = new ProbeControl { Focusable = true };
            var content = new Overlay { Children = { first, second } };
            var window = new Window { Content = content, Visibility = Visibility.Collapsed };
            var root = new Overlay { Children = { background, window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            var gained = new List<ControlBase?>();
            focus.Gained += (_, eventArgs) => gained.Add(eventArgs.Current);

            using var scope = window.ShowModal(OutsideInteraction.Ignore, second);

            gained.ShouldBe([second]);
            first.Focused.ShouldBeFalse();
            focus.Focused.ShouldBeSameAs(second);
            scope.Active.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies invalid focus restores the exact pre-call Window visibility and background focus.</summary>
    [Theory]
    [InlineData(Visibility.Hidden)]
    [InlineData(Visibility.Collapsed)]
    public async Task ShowModal_WhenInitialFocusIsOutsideWindow_RestoresPriorVisibilityAsync(
        Visibility visibility)
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { Focusable = true };
            var action = new ProbeControl { Focusable = true };
            var window = new Window { Content = action, Visibility = visibility };
            var root = new Overlay { Children = { background, window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();

            var exception = Should.Throw<ArgumentException>(() =>
                window.ShowModal(initialFocus: background));

            exception.ParamName.ShouldBe("initialFocus");
            window.Visibility.ShouldBe(visibility);
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeSameAs(background);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an exposure callback failure rolls the Window back to its exact prior visibility.</summary>
    [Fact]
    public async Task ShowModal_WhenVisibilityCallbackFails_RestoresPriorVisibilityAndFailureIdentityAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var expected = new InvalidOperationException("The visibility callback failed.");
            var action = new ProbeControl { Focusable = true };
            var window = new Window { Content = action, Visibility = Visibility.Collapsed };
            var root = new Overlay { Children = { window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            window.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlBase.Visibility) &&
                    window.Visibility == Visibility.Visible)
                {
                    throw expected;
                }
            };

            var exception = Should.Throw<InvalidOperationException>(() => window.ShowModal());

            exception.ShouldBeSameAs(expected);
            window.Visibility.ShouldBe(Visibility.Collapsed);
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies modal-entry failure wins over rollback callback failure and restores prior visibility.</summary>
    [Fact]
    public async Task ShowModal_WhenEntryAndRollbackCallbacksFail_PreservesEntryFailureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var expected = new InvalidOperationException("The modal focus callback failed.");
            var background = new ProbeControl { Focusable = true };
            var action = new ProbeControl { Focusable = true };
            var window = new Window { Content = action, Visibility = Visibility.Hidden };
            var root = new Overlay { Children = { background, window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            focus.Gained += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Current, action))
                {
                    throw expected;
                }
            };
            window.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlBase.Visibility) &&
                    window.Visibility == Visibility.Hidden)
                {
                    throw new InvalidOperationException("The rollback callback failed.");
                }
            };

            var exception = Should.Throw<InvalidOperationException>(() => window.ShowModal());

            exception.ShouldBeSameAs(expected);
            window.Visibility.ShouldBe(Visibility.Hidden);
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeSameAs(background);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies policy validation occurs before a hidden Window is exposed.</summary>
    [Fact]
    public async Task ShowModal_WhenOutsideInteractionIsUndefined_ThrowsBeforeMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { Focusable = true };
            var window = new Window { Content = action, Visibility = Visibility.Hidden };
            var root = new Overlay { Children = { window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var visibilityChanges = 0;
            window.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlBase.Visibility))
                {
                    visibilityChanges++;
                }
            };

            var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
                window.ShowModal((OutsideInteraction) int.MaxValue));

            exception.ParamName.ShouldBe("outsideInteraction");
            visibilityChanges.ShouldBe(0);
            window.Visibility.ShouldBe(Visibility.Hidden);
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    #endregion

    #region Modal interaction

    /// <summary>Verifies default and cancel Button clicks alone decide whether the modal Window remains presented.</summary>
    [Fact]
    public async Task ShowModal_WhenDefaultAndCancelButtonsRun_ClickHandlersOwnVisibilityAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var editor = new ProbeControl { Focusable = true };
            var accept = new Button { IsDefault = true };
            var cancel = new Button { IsCancel = true };
            var accepted = 0;
            var cancelled = 0;
            accept.Click += (_, _) => accepted++;
            var window = new Window
            {
                Content = new Stack { Children = { editor, accept, cancel } },
                Visibility = Visibility.Collapsed,
            };
            cancel.Click += (_, _) =>
            {
                cancelled++;
                window.Visibility = Visibility.Hidden;
            };
            var root = new Overlay { Children = { window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var scope = window.ShowModal(initialFocus: editor);

            _ = Router.Route(editor, Events.Key, Key(Code.Enter));

            accepted.ShouldBe(1);
            scope.Active.ShouldBeTrue();
            window.Visibility.ShouldBe(Visibility.Visible);

            _ = Router.Route(editor, Events.Key, Key(Code.Escape));

            cancelled.ShouldBe(1);
            scope.Active.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            window.Visibility.ShouldBe(Visibility.Hidden);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the close glyph collapses the Window and ends its modal scope on a single press by default.</summary>
    [Fact]
    public async Task ShowModal_WhenCloseGlyphRequestsClosing_ClosesOnOnePressByDefaultAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { Focusable = true };
            var window = new Window
            {
                CanClose = true,
                Content = action,
                Width = Length.Cells(12),
                Height = Length.Cells(5),
            };
            var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(24, 10));
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var closing = 0;
            var closed = 0;
            window.Closing += (_, _) => closing++;
            window.Closed += (_, _) => closed++;
            var scope = window.ShowModal();
            var close = new Point(window.Bounds.X + 4, window.Bounds.Y);

            _ = pointer.Dispatch(Pointer(close, PointerAction.Press));
            _ = pointer.Dispatch(Pointer(close, PointerAction.Release));

            closing.ShouldBe(1);
            closed.ShouldBe(1);
            scope.Active.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            window.Visibility.ShouldBe(Visibility.Collapsed);
            window.SurfaceBounds.ShouldBe(default);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a Closing handler that explicitly hides the Window on its own is not double-collapsed.</summary>
    [Fact]
    public async Task ShowModal_WhenClosingHandlerHidesWindowItself_DoesNotDoubleCloseAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var window = new Window
            {
                CanClose = true,
                Width = Length.Cells(12),
                Height = Length.Cells(5),
            };
            var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(24, 10));
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var closing = 0;
            var closed = 0;
            window.Closing += (_, _) =>
            {
                closing++;
                window.Visibility = Visibility.Hidden;
            };
            window.Closed += (_, _) => closed++;
            var scope = window.ShowModal();
            var close = new Point(window.Bounds.X + 4, window.Bounds.Y);

            _ = pointer.Dispatch(Pointer(close, PointerAction.Press));
            _ = pointer.Dispatch(Pointer(close, PointerAction.Release));

            closing.ShouldBe(1);
            closed.ShouldBe(1);
            scope.Active.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            window.Visibility.ShouldBe(Visibility.Hidden);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a close owner may hide then restore the Window without leaving presentation state stale.</summary>
    [Fact]
    public async Task ShowModal_WhenClosingHandlerRestoresVisibility_ReopensPresentedAndModelessAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var window = new Window
            {
                CanClose = true,
                Width = Length.Cells(12),
                Height = Length.Cells(5)
            };
            using var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(24, 10));
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var closing = 0;
            var closed = 0;
            window.Closing += (_, _) =>
            {
                closing++;
                window.Visibility = Visibility.Hidden;
                window.Visibility = Visibility.Visible;
            };
            window.Closed += (_, _) => closed++;
            var scope = window.ShowModal();
            var close = new Point(window.Bounds.X + 4, window.Bounds.Y);

            _ = pointer.Dispatch(Pointer(close, PointerAction.Press));
            _ = pointer.Dispatch(Pointer(close, PointerAction.Release));

            closing.ShouldBe(1);
            closed.ShouldBe(0);
            scope.Active.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            window.Visibility.ShouldBe(Visibility.Visible);
            window.SurfaceBounds.ShouldBe(window.Bounds);
        }, TestContext.Current.CancellationToken);
    }

    #endregion

    #region Modeless compatibility

    /// <summary>Verifies ordinary modeless visibility continues to focus the first eligible descendant.</summary>
    [Fact]
    public async Task Visibility_WhenWindowIsShownModelessly_FocusesFirstDescendantAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { Focusable = true };
            var first = new ProbeControl { Focusable = true };
            var second = new ProbeControl { Focusable = true };
            var window = new Window
            {
                Content = new Overlay { Children = { first, second } },
                Visibility = Visibility.Hidden,
            };
            var root = new Overlay { Children = { background, window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();

            window.Visibility = Visibility.Visible;

            focus.Focused.ShouldBeSameAs(first);
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    #endregion

    #region MessageBox scope stacking

    /// <summary>Verifies MessageBox.ShowAsync inside a modal window stacks modal scopes correctly.</summary>
    [Fact]
    public async Task ShowAsync_WhenCalledInsideModalWindow_StacksScopesAndRestoresWindowFocusAsync()
    {
        // Arrange
        var trigger = new Button { Text = "Trigger" };
        var parentWindow = new Window
        {
            Content = trigger,
            Visibility = Visibility.Collapsed,
        };
        var host = new Overlay { Children = { parentWindow } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(60, 20),
            TestContext.Current.CancellationToken);
        ModalScope? windowScope = null;

        // Act — show the window modally
        await surface.UpdateAsync(
            () => windowScope = parentWindow.ShowModal(),
            "show modal window");

        // Assert — window scope is active
        windowScope.ShouldNotBeNull().Active.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(windowScope);

        // Act — show a MessageBox from the trigger button inside the modal window
        Task<MessageBoxResult>? messagePending = null;
        await surface.UpdateAsync(
            () => messagePending = MessageBox.ShowAsync(trigger, "Continue?", "Confirm"),
            "show MessageBox");
        var messageBox = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        var messageBoxWindow = OwnedTree.Find<Window>(messageBox).ShouldNotBeNull();

        // Assert — both scopes are active; MessageBox scope is youngest
        var messageBoxScope = surface.Application.Modality.Active.ShouldNotBeNull();
        messageBoxScope.ShouldNotBeSameAs(windowScope);
        messageBoxScope.Root.ShouldBeSameAs(messageBoxWindow);
        windowScope.Active.ShouldBeTrue();

        // Act — press Escape to dismiss the MessageBox
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert — MessageBox dismissed, window scope restored
        (await messagePending!).ShouldBe(MessageBoxResult.Cancel);
        windowScope.Active.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(windowScope);

        // Clean up
        await surface.UpdateAsync(windowScope.Dispose, "end window modal");
    }

    #endregion

    #region Test data

    private static KeyEventArgs Key(Code code) => new(new Stroke(
        code,
        default,
        nativeCode: 0,
        Modifiers.None,
        KeyAction.Press));

    private static Pointer Pointer(Point cells, PointerAction action) => new(
        cells,
        pixels: null,
        action == PointerAction.Release ? Buttons.None : Buttons.Primary,
        action,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: false,
        isCellPositionInferred: false);

    #endregion
}
