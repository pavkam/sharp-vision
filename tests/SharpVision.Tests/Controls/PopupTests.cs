// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;




/// <summary>Verifies anchored popup visibility, placement, and dismissal behavior.</summary>
public sealed class PopupTests
{
    /// <summary>Verifies Popup exposes only the public single-content authoring role.</summary>
    [Fact]
    public void Type_WhenInspected_DerivesFromContentControlWithoutChildCollectionOrAlias()
    {
        var type = typeof(Popup);

        type.BaseType.ShouldBe(typeof(ContentControl));
        typeof(Container).IsAssignableFrom(type).ShouldBeFalse();
        type.GetProperty(nameof(Container.Children)).ShouldBeNull();
        type.GetProperty(nameof(Container.AutoScroll)).ShouldBeNull();
        type.GetProperty(nameof(Container.AutoSize)).ShouldBeNull();
        type.GetProperty("Child").ShouldBeNull();
        _ = type.GetProperty(nameof(ContentControl.Content)).ShouldNotBeNull();
        var constructor = type.GetConstructors().ShouldHaveSingleItem();
        constructor.GetParameters().ShouldBeEmpty();
    }

    /// <summary>Verifies replacement applies current open state only to newly committed content.</summary>
    [Fact]
    public void Content_WhenReplacedClosedAndOpen_ControlsCurrentVisibilityOnly()
    {
        var popup = new Popup();
        ContentControl owner = popup;
        var firstClosed = new ProbeControl();
        var secondClosed = new ProbeControl();
        var openReplacement = new ProbeControl { Visibility = Visibility.Hidden };
        var finalClosed = new ProbeControl { Visibility = Visibility.Hidden };

        owner.Content = firstClosed;
        owner.Content = secondClosed;

        firstClosed.Visibility.ShouldBe(Visibility.Collapsed);
        secondClosed.Visibility.ShouldBe(Visibility.Collapsed);

        popup.IsOpen = true;
        owner.Content = openReplacement;

        secondClosed.Visibility.ShouldBe(Visibility.Visible);
        openReplacement.Visibility.ShouldBe(Visibility.Visible);

        popup.IsOpen = false;
        owner.Content = finalClosed;

        openReplacement.Visibility.ShouldBe(Visibility.Collapsed);
        finalClosed.Visibility.ShouldBe(Visibility.Collapsed);
    }

    /// <summary>Verifies direct content disposal clears Popup through inherited ownership publication.</summary>
    [Fact]
    public void Dispose_WhenContentIsDisposedDirectly_ClearsPopupContentOnce()
    {
        var popup = new Popup();
        ContentControl owner = popup;
        var content = new ProbeControl();
        var notifications = 0;
        owner.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ContentControl.Content))
            {
                notifications++;
            }
        };
        owner.Content = content;
        notifications = 0;

        content.Dispose();

        owner.Content.ShouldBeNull();
        content.IsDisposed.ShouldBeTrue();
        notifications.ShouldBe(1);
    }

    /// <summary>Verifies Popup disposal clears close handlers before child teardown and preserves failure order.</summary>
    [Fact]
    public void Dispose_WhenPopupOwnsReplacement_CompletesContentPublicationAndDisposesCurrentOnce()
    {
        var popup = new Popup();
        var replaced = new OwnershipObserverControl();
        var current = new OwnershipObserverControl { ThrowOnDisposing = true };
        popup.Content = replaced;
        popup.Content = current;
        replaced.Visibility.ShouldBe(Visibility.Collapsed);
        popup.IsOpen = true;
        var closingCalls = 0;
        var closedCalls = 0;
        popup.Closing += (_, _) => closingCalls++;
        popup.Closed += (_, _) => closedCalls++;
        current.Disposing = _ => popup.IsOpen = false;
        var contentNotifications = 0;
        popup.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName != nameof(ContentControl.Content))
            {
                return;
            }

            contentNotifications++;
            popup.Content.ShouldBeNull();
            current.Parent.ShouldBeNull();
            current.IsDisposed.ShouldBeFalse();
            current.DisposingCalls.ShouldBe(1);
            closingCalls.ShouldBe(0);
            closedCalls.ShouldBe(0);
            throw new InvalidOperationException("The content subscriber failed.");
        };

        var exception = Should.Throw<InvalidOperationException>(popup.Dispose);
        popup.Dispose();

        exception.Message.ShouldBe("The disposal callback failed.");
        popup.IsDisposed.ShouldBeTrue();
        popup.Content.ShouldBeNull();
        popup.IsOpen.ShouldBeFalse();
        contentNotifications.ShouldBe(1);
        closingCalls.ShouldBe(0);
        closedCalls.ShouldBe(0);
        current.IsDisposed.ShouldBeTrue();
        current.DisposingCalls.ShouldBe(1);
        replaced.IsDisposed.ShouldBeFalse();
        replaced.Parent.ShouldBeNull();
        replaced.Visibility.ShouldBe(Visibility.Collapsed);
    }

    /// <summary>Verifies Popup hook and parent subscriber failures preserve committed content and first-failure order.</summary>
    [Fact]
    public void Content_WhenVisibilityHookAndPropertySubscriberThrow_CommitsAndPreservesHookFailure()
    {
        var popup = new Popup();
        ContentControl owner = popup;
        var content = new ProbeControl();
        var parentSubscriberRan = false;
        content.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Control.Visibility))
            {
                throw new InvalidOperationException("The visibility subscriber failed.");
            }
        };
        owner.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName != nameof(ContentControl.Content))
            {
                return;
            }

            parentSubscriberRan = true;
            owner.Content.ShouldBeSameAs(content);
            content.Parent.ShouldBeSameAs(popup);
            content.Visibility.ShouldBe(Visibility.Collapsed);
            throw new InvalidOperationException("The content subscriber failed.");
        };

        var exception = Should.Throw<InvalidOperationException>(() => owner.Content = content);

        exception.Message.ShouldBe("The visibility subscriber failed.");
        parentSubscriberRan.ShouldBeTrue();
        owner.Content.ShouldBeSameAs(content);
        content.Parent.ShouldBeSameAs(popup);
        content.Visibility.ShouldBe(Visibility.Collapsed);
    }

    /// <summary>Verifies closed-content attachment cannot retain focus or capture when collapse publication fails.</summary>
    [Fact]
    public async Task Content_WhenClosedAttachmentAcquiresManagersAndVisibilitySubscriberThrows_CleansBeforeContentPublicationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup();
            var root = new Overlay();
            root.Children.Add(popup);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var capture = new PointerManager(root);
            var content = new OwnershipObserverControl { Focusable = true };
            var acquiredFocus = false;
            var acquiredCapture = false;
            content.Attaching = control =>
            {
                acquiredFocus = control.RequestObserverFocus();
                acquiredCapture = control.CaptureObserverPointer();
            };
            var expected = new InvalidOperationException("The visibility subscriber failed.");
            content.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(Control.Visibility))
                {
                    throw expected;
                }
            };
            var contentNotificationRan = false;
            popup.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName != nameof(ContentControl.Content))
                {
                    return;
                }

                contentNotificationRan = true;
                focus.Focused.ShouldBeNull();
                capture.Captured.ShouldBeNull();
            };

            var exception = Should.Throw<InvalidOperationException>(() => popup.Content = content);

            exception.ShouldBeSameAs(expected);
            acquiredFocus.ShouldBeTrue();
            acquiredCapture.ShouldBeTrue();
            contentNotificationRan.ShouldBeTrue();
            popup.Content.ShouldBeSameAs(content);
            content.Visibility.ShouldBe(Visibility.Collapsed);
            focus.Focused.ShouldBeNull();
            capture.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies opening completes content visibility and focus after every callback path fails.</summary>
    [Fact]
    public async Task IsOpen_WhenOpeningCallbacksThrow_CompletesTransitionAndPreservesFirstFailureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var content = new ProbeControl { Focusable = true };
            var popup = new Popup { Content = content };
            var root = new Overlay();
            root.Children.Add(popup);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            var order = new List<string>();
            var expected = new InvalidOperationException("The open-state subscriber failed.");
            popup.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName != nameof(Popup.IsOpen))
                {
                    return;
                }

                order.Add("property");
                popup.IsOpen.ShouldBeTrue();
                content.Visibility.ShouldBe(Visibility.Collapsed);
                throw expected;
            };
            content.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName != nameof(Control.Visibility))
                {
                    return;
                }

                order.Add("content");
                content.Visibility.ShouldBe(Visibility.Visible);
                focus.Focused.ShouldBeNull();
                throw new InvalidOperationException("The visibility subscriber failed.");
            };
            focus.Gained += (_, _) =>
            {
                order.Add("focus");
                focus.Focused.ShouldBeSameAs(content);
                throw new InvalidOperationException("The focus subscriber failed.");
            };

            var exception = Should.Throw<InvalidOperationException>(() => popup.IsOpen = true);

            exception.ShouldBeSameAs(expected);
            order.ShouldBe(["property", "content", "focus"]);
            popup.IsOpen.ShouldBeTrue();
            content.Visibility.ShouldBe(Visibility.Visible);
            focus.Focused.ShouldBeSameAs(content);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies closing completes every ordered stage and preserves the first callback failure.</summary>
    [Fact]
    public async Task IsOpen_WhenClosingCallbacksThrow_CompletesTransitionAndPreservesFirstFailureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var content = new ProbeControl { Focusable = true };
            var popup = new Popup { Content = content };
            var root = new Overlay();
            root.Children.Add(popup);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var capture = new PointerManager(root);
            popup.IsOpen = true;
            new Engine().Layout(root, new Size(12, 6));
            content.CaptureProbePointer().ShouldBeTrue();
            var openSurface = popup.SurfaceBounds;
            openSurface.ShouldNotBe(default);
            content.ThrowOnPointerCaptureCancellation = true;
            var order = new List<string>();
            var expected = new InvalidOperationException("The open-state subscriber failed.");
            popup.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName != nameof(Popup.IsOpen))
                {
                    return;
                }

                order.Add("property");
                popup.IsOpen.ShouldBeFalse();
                content.Visibility.ShouldBe(Visibility.Visible);
                popup.SurfaceBounds.ShouldBe(openSurface);
                throw expected;
            };
            popup.Closing += (_, _) =>
            {
                order.Add("closing");
                content.Visibility.ShouldBe(Visibility.Visible);
                popup.SurfaceBounds.ShouldBe(openSurface);
                throw new InvalidOperationException("The closing subscriber failed.");
            };
            content.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName != nameof(Control.Visibility))
                {
                    return;
                }

                order.Add("content");
                content.Visibility.ShouldBe(Visibility.Collapsed);
                focus.Focused.ShouldBeNull();
                capture.Captured.ShouldBeNull();
                popup.SurfaceBounds.ShouldBe(openSurface);
                throw new InvalidOperationException("The visibility subscriber failed.");
            };
            popup.Closed += (_, _) =>
            {
                order.Add("closed");
                popup.SurfaceBounds.ShouldBe(default);
                content.Visibility.ShouldBe(Visibility.Collapsed);
                focus.Focused.ShouldBeNull();
                capture.Captured.ShouldBeNull();
                throw new InvalidOperationException("The closed subscriber failed.");
            };

            var exception = Should.Throw<InvalidOperationException>(() => popup.IsOpen = false);

            exception.ShouldBeSameAs(expected);
            order.ShouldBe(["property", "closing", "content", "closed"]);
            popup.IsOpen.ShouldBeFalse();
            popup.SurfaceBounds.ShouldBe(default);
            content.Visibility.ShouldBe(Visibility.Collapsed);
            focus.Focused.ShouldBeNull();
            capture.Captured.ShouldBeNull();
            content.PointerCaptureCancellationCalls.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a callback cannot reverse an active open-state transaction.</summary>
    [Fact]
    public void IsOpen_WhenPropertyCallbackReenters_RejectsNestedTransitionAndKeepsOuterStateCoherent()
    {
        var content = new ProbeControl();
        var popup = new Popup { Content = content };
        var attempted = false;
        Exception? nestedFailure = null;
        popup.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName != nameof(Popup.IsOpen) || attempted)
            {
                return;
            }

            attempted = true;

            try
            {
                popup.IsOpen = false;
            }
            catch (Exception exception)
            {
                nestedFailure = exception;
            }
        };

        popup.IsOpen = true;

        var invalidOperation = nestedFailure.ShouldBeOfType<InvalidOperationException>();
        invalidOperation.Message.ShouldBe("Popup open-state transitions cannot be reentered.");
        popup.IsOpen.ShouldBeTrue();
        content.Visibility.ShouldBe(Visibility.Visible);

        popup.IsOpen = false;

        popup.IsOpen.ShouldBeFalse();
        content.Visibility.ShouldBe(Visibility.Collapsed);
    }

    /// <summary>Verifies closing completes the inherited collapsed-child layout transactions.</summary>
    [Fact]
    public void Layout_WhenOpenPopupCloses_ClearsContentGeometryWithoutInvokingOverrides()
    {
        var content = new ProbeControl(new Size(4, 2));
        var popup = new Popup { Content = content, IsOpen = true };
        var engine = new Engine();

        engine.Layout(popup, new Size(12, 6));
        var measureCalls = content.MeasureConstraints.Count;
        var arrangeCalls = content.ArrangeBounds.Count;
        content.DesiredSize.ShouldNotBe(default);
        content.Bounds.ShouldNotBe(default);
        popup.SurfaceBounds.ShouldNotBe(default);

        popup.IsOpen = false;
        engine.Layout(popup, new Size(12, 6));

        popup.DesiredSize.ShouldBe(default);
        popup.SurfaceBounds.ShouldBe(default);
        content.DesiredSize.ShouldBe(default);
        content.Bounds.ShouldBe(default);
        content.MeasureConstraints.Count.ShouldBe(measureCalls);
        content.ArrangeBounds.Count.ShouldBe(arrangeCalls);
    }

    /// <summary>Verifies closed content cannot leak into rendering or hit testing.</summary>
    [Fact]
    public void Render_WhenClosed_DoesNotRenderOrHitTestChild()
    {
        var child = new ProbeControl(new Size(3, 1)) { Content = "pop".AsMemory() };
        var popup = new Popup() { Content = child };
        new Engine().Layout(popup, new Size(8, 4));
        using Frame frame = new(new Size(8, 4));

        popup.Render(frame.Canvas);

        child.RenderCalls.ShouldBe(0);
        popup.HitTest(new Point(0, 0)).ShouldBeNull();
        FrameOracle.Get(frame, default).ShouldBeEmpty();
    }

    /// <summary>Verifies a popup below an anchor flips above before terminal-edge clamping.</summary>
    [Fact]
    public void Arrange_WhenBelowWouldOverflow_FlipsAboveAnchor()
    {
        var anchor = new ProbeControl() { Bounds = new Rect(2, 3, 2, 1) };
        var child = new ProbeControl(new Size(4, 2));
        var popup = new Popup() { Anchor = anchor, Content = child, IsOpen = true };

        new Engine().Layout(popup, new Size(10, 5));

        child.Bounds.ShouldBe(new Rect(3, 2, 4, 2));
    }

    /// <summary>Verifies an open popup owns an opaque framed surface around its content rather than leaking the child inline.</summary>
    [Fact]
    public void Render_WhenOpen_DrawsSurfaceFrameAndContainsChild()
    {
        var anchor = new ProbeControl() { Bounds = new Rect(2, 0, 2, 1) };
        var child = new ProbeControl(new Size(4, 1)) { Content = "pick".AsMemory() };
        var popup = new Popup() { Anchor = anchor, Content = child, IsOpen = true };
        var size = new Size(12, 6);
        new Engine().Layout(popup, size);
        using Frame frame = new(size);

        popup.Render(frame.Canvas);

        popup.SurfaceBounds.ShouldBe(new Rect(2, 1, 6, 3));
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("╭");
        FrameOracle.Get(frame, new Point(3, 2)).ShouldBe("p");
        FrameOracle.Get(frame, new Point(7, 3)).ShouldBe("╯");
        popup.HitTest(new Point(3, 2)).ShouldBeSameAs(child);
        popup.HitTest(new Point(2, 1)).ShouldBeSameAs(popup);
    }

    /// <summary>Verifies an open popup is painted and hit-tested above later ordinary siblings in its owning overlay.</summary>
    [Fact]
    public void Render_WhenLaterSiblingOverlaps_PopupRetainsTopmostInputAndSurface()
    {
        var comboBox = new ComboBox()
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(10),
            Height = Length.Cells(1),
            Items = ["Small", "Large"],
            DropDownHeight = 2,
            IsOpen = true,
        };
        var cover = new Dock() { Background = Color.Indexed(7) };
        var root = new Overlay() { ClipToBounds = false };
        root.Children.Add(comboBox);
        root.Children.Add(cover);
        var size = new Size(16, 8);
        new Engine().Layout(root, size);
        var list = OwnedTree.Find<List>(comboBox).ShouldNotBeNull();
        var point = new Point(list.Bounds.X + 1, list.Bounds.Y);
        using Frame frame = new(size);

        root.Render(frame.Canvas);

        cover.Bounds.ShouldBe(root.Bounds);
        frame.GetCell(new Point(15, 7)).Style.Background.ShouldBe(Color.Indexed(7));
        _ = root.HitTest(point).ShouldBeOfType<ListItem>();
        FrameOracle.Get(frame, point).ShouldBe("m");
    }

    /// <summary>Verifies a real popup in a non-Container popup slot renders once and owns elevated hit testing.</summary>
    [Fact]
    public void Render_WhenNonContainerOwnsPopupLayer_PromotesOpenSurfaceOnly()
    {
        var size = new Size(12, 6);
        var root = new TraversalOwner { Bounds = new Rect(0, 0, size.Width, size.Height) };
        var anchor = new ProbeControl { Bounds = new Rect(2, 0, 2, 1) };
        var child = new ProbeControl(new Size(4, 1)) { Content = "pick".AsMemory() };
        var popup = new Popup { Anchor = anchor, Content = child, IsOpen = true };
        root.AddNormal(anchor);
        root.AddPopup(popup);
        popup.Measure(new Constraint(size.Width, size.Height));
        popup.Arrange(root.Bounds, widthResolved: true, heightResolved: true);
        using Frame frame = new(size);

        root.Render(frame.Canvas);

        child.RenderCalls.ShouldBe(1);
        root.HitTest(new Point(child.Bounds.X, child.Bounds.Y)).ShouldBeSameAs(child);
        popup.IsOpen = false;
        using Frame closedFrame = new(size);
        root.Render(closedFrame.Canvas);
        child.RenderCalls.ShouldBe(1);
        root.HitTest(new Point(child.Bounds.X, child.Bounds.Y)).ShouldNotBeSameAs(child);
    }

    /// <summary>Verifies the shipped ComboBox registers its private Popup directly in the elevated layer.</summary>
    [Fact]
    public void Ownership_WhenComboBoxOwnsPopup_UsesDedicatedPopupLayer()
    {
        var box = new ComboBox
        {
            IsOpen = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Items = ["pick"],
        };
        var size = new Size(12, 6);
        new Engine().Layout(box, size);
        using Frame frame = new(size);

        box.Render(frame.Canvas);

        var popup = OwnedTree.Find<Popup>(box).ShouldNotBeNull();
        popup.OwningSlot.ShouldNotBeNull().Options.Layer.ShouldBe(OwnedControlLayer.Popup);
        var list = OwnedTree.Find<List>(popup).ShouldNotBeNull();
        FrameOracle.Get(frame, new Point(list.Bounds.X, list.Bounds.Y)).ShouldBe("p");
    }

    /// <summary>Verifies Escape bubbles through popup content and closes the owner.</summary>
    [Fact]
    public void Dispatch_WhenEscapeArrives_ClosesOpenPopup()
    {
        var child = new ProbeControl();
        var popup = new Popup() { Content = child, IsOpen = true };
        var eventArgs = new KeyEventArgs(new Stroke(
            Code.Escape,
            default,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press));

        _ = Router.Route(child, Events.Key, eventArgs);

        popup.IsOpen.ShouldBeFalse();
        eventArgs.Handled.ShouldBeTrue();
    }

    /// <summary>Verifies opening transfers focus to a focusable popup child for keyboard-driven pickers.</summary>
    [Fact]
    public async Task IsOpen_WhenFocusableChildExists_MovesFocusToChildAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var child = new List() { Items = ["first", "second"] };
            var popup = new Popup() { Content = child };
            var root = new Overlay();
            root.Children.Add(popup);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);

            popup.IsOpen = true;

            focus.Focused.ShouldBeSameAs(child);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies focus discovery descends a non-Container popup content owner.</summary>
    [Fact]
    public async Task IsOpen_WhenNonContainerContentOwnsFocusablePart_MovesFocusToPartAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var content = new TraversalOwner();
            var child = new ProbeControl { Focusable = true };
            content.AddExcluded(child);
            var popup = new Popup { Content = content };
            var root = new Overlay();
            root.Children.Add(popup);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            popup.IsOpen = true;

            focus.Focused.ShouldBeSameAs(child);
        }, TestContext.Current.CancellationToken);
    }
}
