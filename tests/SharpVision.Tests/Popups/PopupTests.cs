// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Popups;

/// <summary>Verifies anchored popup visibility, placement, and dismissal behavior.</summary>
public sealed class PopupTests
{
    /// <summary>Verifies every Popup family releases common and family presentation state when an
    /// Opened observer fails, then remains reusable.</summary>
    [Fact]
    public async Task IsOpen_WhenOpenedObserverFails_AllPopupFamiliesRollbackAndReopenAsync()
    {
        var anchor = new Button { Text = "Anchor" };
        Popup[] popups =
        [
            new Popup { Anchor = anchor, Content = new ControlText("Popup") },
            new Flyout { Anchor = anchor, Content = new ControlText("Flyout") },
            new Tooltip { Anchor = anchor, Content = new ControlText("Tooltip") }
        ];
        var root = new Overlay { Children = { anchor } };

        foreach (var popup in popups)
        {
            root.Children.Add(popup);
        }

        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);

        foreach (var popup in popups)
        {
            var expected = new InvalidOperationException("opened failed");
            void Failing(object? sender, EventArgs eventArgs)
            {
                _ = sender;
                _ = eventArgs;
                throw expected;
            }

            popup.Opened += Failing;

            var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
                surface.UpdateAsync(() => popup.IsOpen = true, "fail Popup-family Opened observer"));

            thrown.ShouldBeSameAs(expected);
            popup.IsOpen.ShouldBeFalse();
            popup.HasLightDismissRegistration.ShouldBeFalse();
            popup.SurfaceBounds.ShouldBe(default);
            popup.Content.ShouldNotBeNull().Visibility.ShouldBe(Visibility.Collapsed);
            popup.Opened -= Failing;
            await surface.UpdateAsync(() => popup.IsOpen = true, "reopen Popup family after failure");
            popup.IsOpen.ShouldBeTrue();
            await surface.UpdateAsync(() => popup.IsOpen = false, "close reopened Popup family");
        }
    }

    /// <summary>Verifies sibling closure uses stable identities when a closing popup removes itself
    /// from the collection being traversed.</summary>
    [Fact]
    public async Task IsOpen_WhenClosingSiblingRemovesItself_StillClosesRemainingPopupSnapshotAsync()
    {
        var first = new Popup
        {
            Content = new ControlText("First"),
            SuppressCloseOtherPopups = true
        };
        var second = new Popup
        {
            Content = new ControlText("Second"),
            SuppressCloseOtherPopups = true
        };
        var opening = new Popup { Content = new ControlText("Opening") };
        var root = new Overlay { Children = { first, second, opening } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () =>
            {
                first.IsOpen = true;
                second.IsOpen = true;
            },
            "open retained Popup siblings");
        first.Closing += (_, _) => _ = root.Children.Remove(first);

        await surface.UpdateAsync(() => opening.IsOpen = true, "open exclusive Popup");

        first.IsOpen.ShouldBeFalse();
        second.IsOpen.ShouldBeFalse();
        opening.IsOpen.ShouldBeTrue();
        root.Children.ShouldNotContain(first);
    }

    /// <summary>Verifies one peer disposing another during closure cannot invalidate the remaining
    /// stable snapshot or prevent the initiating Popup from opening.</summary>
    [Fact]
    public async Task IsOpen_WhenClosingPeerDisposesAnotherPeer_CompletesStableTraversalAsync()
    {
        var first = new Popup
        {
            Content = new ControlText("First"),
            SuppressCloseOtherPopups = true,
            ModalBehavior = PopupModalBehavior.None
        };
        var disposed = new Popup
        {
            Content = new ControlText("Disposed"),
            SuppressCloseOtherPopups = true,
            ModalBehavior = PopupModalBehavior.None
        };
        var opening = new Popup
        {
            Content = new ControlText("Opening"),
            ModalBehavior = PopupModalBehavior.None
        };
        var root = new Overlay { Children = { first, disposed, opening } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() =>
        {
            first.IsOpen = true;
            disposed.IsOpen = true;
        }, "open existing Popup peers");
        first.Closing += (_, _) => disposed.Dispose();

        await surface.UpdateAsync(() => opening.IsOpen = true, "open Popup while one peer disposes another");

        first.IsOpen.ShouldBeFalse();
        disposed.IsDisposed.ShouldBeTrue();
        opening.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies a sibling callback may open a third popup without reentering the popup
    /// currently being established; the outermost opening transaction remains the survivor.</summary>
    [Fact]
    public async Task IsOpen_WhenClosingSiblingOpensThirdPopup_OpeningTransactionWinsDeterministicallyAsync()
    {
        var first = new Popup
        {
            Content = new ControlText("First"),
            SuppressCloseOtherPopups = true
        };
        var opening = new Popup { Content = new ControlText("Opening") };
        var third = new Popup { Content = new ControlText("Third") };
        var root = new Overlay { Children = { first, opening, third } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => first.IsOpen = true, "open first Popup");
        first.Closing += (_, _) => third.IsOpen = true;

        await surface.UpdateAsync(() => opening.IsOpen = true, "open Popup across reentrant sibling opening");

        first.IsOpen.ShouldBeFalse();
        opening.IsOpen.ShouldBeTrue();
        third.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies disposal superseding the initiating open stops its stale traversal before
    /// a callback-opened newer peer can be closed.</summary>
    [Fact]
    public async Task IsOpen_WhenSiblingCallbackDisposesOpeningPopup_DoesNotCloseNewerPeerAsync()
    {
        var first = new Popup
        {
            Content = new ControlText("First"),
            SuppressCloseOtherPopups = true,
            ModalBehavior = PopupModalBehavior.None
        };
        var opening = new Popup
        {
            Content = new ControlText("Opening"),
            ModalBehavior = PopupModalBehavior.None
        };
        var newer = new Popup
        {
            Content = new ControlText("Newer"),
            SuppressCloseOtherPopups = true,
            ModalBehavior = PopupModalBehavior.None
        };
        var root = new Overlay { Children = { first, opening, newer } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() =>
        {
            first.IsOpen = true;
            newer.IsOpen = true;
        }, "open existing Popup peers");
        newer.IsOpen.ShouldBeTrue();
        var newerOpenAfterSupersession = false;
        first.Closing += (_, _) =>
        {
            opening.Dispose();
            newerOpenAfterSupersession = newer.IsOpen;
        };

        _ = await Should.ThrowAsync<InvalidOperationException>(() =>
            surface.UpdateAsync(() => opening.IsOpen = true, "supersede Popup opening during peer closure"));

        opening.IsDisposed.ShouldBeTrue();
        newerOpenAfterSupersession.ShouldBeTrue();
        newer.IsOpen.ShouldBeTrue();
    }
    /// <summary>Verifies attached presentation rejects every anchor relation that cannot remain
    /// coherent in the popup's owning tree.</summary>
    [Fact]
    public async Task IsOpen_WhenAnchorIsDetachedForeignOrOwnedByPopup_RejectsPresentationAtomicallyAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var valid = new Button { Text = "Valid" };
            var content = new Button { Text = "Content" };
            var popup = new Popup { Content = content };
            var root = new Overlay { Children = { valid, popup } };
            var foreign = new Button { Text = "Foreign" };
            root.Attach(dispatcher);
            foreign.Attach(dispatcher);

            foreach (var invalid in new ControlBase[] { new Button(), foreign, popup, content })
            {
                popup.Anchor = invalid;

                _ = Should.Throw<ArgumentException>(() => popup.IsOpen = true);

                popup.IsOpen.ShouldBeFalse();
                content.Visibility.ShouldBe(Visibility.Collapsed);
                popup.SurfaceBounds.ShouldBe(default);
            }

            popup.Anchor = valid;
            popup.IsOpen = true;
            popup.IsOpen.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an invalid anchor replacement is rejected before an open popup mutates
    /// either its anchor or presentation state.</summary>
    [Fact]
    public async Task Anchor_WhenOpenAndReplacementIsDetached_LeavesExistingPresentationUnchangedAsync()
    {
        var anchor = new Button { Text = "Anchor" };
        var popup = new Popup { Anchor = anchor, Content = new ControlText("Menu") };
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(18, 7),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => popup.IsOpen = true, "open Popup with valid anchor");

        _ = await Should.ThrowAsync<ArgumentException>(() =>
            surface.UpdateAsync(() => popup.Anchor = new Button(), "reject detached anchor"));

        popup.Anchor.ShouldBeSameAs(anchor);
        popup.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies a disposed anchor is distinguished from a merely invalid tree relation.</summary>
    [Fact]
    public async Task IsOpen_WhenAnchorIsDisposed_ThrowsObjectDisposedExceptionAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var anchor = new Button();
            anchor.Dispose();
            var popup = new Popup { Anchor = anchor, Content = new ControlText("Menu") };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);

            _ = Should.Throw<ObjectDisposedException>(() => popup.IsOpen = true);

            popup.IsOpen.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies owned popup families may anchor to an ancestor in the same attached tree.</summary>
    [Fact]
    public async Task IsOpen_WhenAnchorIsPopupAncestor_PresentsNormallyAsync()
    {
        var popup = new Popup { Content = new ControlText("Menu") };
        var anchor = new Overlay { Children = { popup } };
        popup.Anchor = anchor;
        await using var surface = await ComponentSurface.MountAsync(
            anchor,
            new Size(18, 7),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => popup.IsOpen = true, "open Popup anchored to ancestor");

        popup.IsOpen.ShouldBeTrue();
        popup.SurfaceBounds.ShouldNotBe(default);
    }
    /// <summary>Verifies an opaque popup owns the dedicated application-window background,
    /// distinct from ordinary Control/Container content, plus the standard themed border
    /// color.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasThemeWindowAppearanceDefaults()
    {
        using var popup = new Popup();

        popup.Face.Background.ShouldBe(SemanticColor.Window);
        popup.Border.Foreground.ShouldBe(SemanticColor.ControlBorder);
    }

    /// <summary>Verifies every Popup-declared property starts at its documented default.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        using var popup = new Popup();

        popup.Anchor.ShouldBeNull();
        popup.Placement.ShouldBe(PopupPlacement.Below);
        popup.ConnectsToAnchor.ShouldBeFalse();
        popup.SuppressCloseOtherPopups.ShouldBeFalse();
        popup.ShowAnchorIndicator.ShouldBeFalse();
        popup.ModalBehavior.ShouldBe(PopupModalBehavior.Auto);
        popup.FocusOnOpen.ShouldBeTrue();
        popup.IsOpen.ShouldBeFalse();
        popup.CloseOnEscape.ShouldBeTrue();
        popup.Style.ShouldBe(default);
    }

    /// <summary>Verifies resolving popup anchor glyphs registers their root structural render
    /// dependency for later Theme swaps.</summary>
    [Fact]
    public void SetTheme_WhenResolvedAnchorGlyphsChange_InvalidatesRender()
    {
        var previous = PopupTheme("^");
        var current = PopupTheme("+");
        using var popup = new Popup();
        popup.SetTheme(previous);
        _ = popup.ResolvedAnchorGlyphs;
        popup.Clear(Invalidation.All);

        popup.SetTheme(current);

        popup.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies changing ShowAnchorIndicator after the popup is already rendered
    /// publishes PropertyChanged, matching every other live-mutable Popup property, instead
    /// of silently doing nothing until an unrelated change forces a repaint.</summary>
    [Fact]
    public void ShowAnchorIndicator_WhenChanged_PublishesPropertyChanged()
    {
        using var popup = new Popup();
        var notifications = 0;
        popup.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Popup.ShowAnchorIndicator))
            {
                notifications++;
            }
        };

        popup.ShowAnchorIndicator = true;
        popup.ShowAnchorIndicator = true;
        popup.ShowAnchorIndicator = false;

        popup.ShowAnchorIndicator.ShouldBeFalse();
        notifications.ShouldBe(2);
    }

    /// <summary>Verifies setting Style publishes PropertyChanged for Style itself, alongside the
    /// Border/Shadow notifications it already forwards, so a data-bound consumer of the composite
    /// property observes the change instead of only the individual components it delegates to.</summary>
    [Fact]
    public void Style_WhenChanged_PublishesPropertyChanged()
    {
        using var popup = new Popup();
        var border = new Border(BorderSide.All, BorderGlyphStyle.Rounded, Color.Rgb(65, 43, 21), Color.Transparent, TerminalAttributes.None);
        var raised = new List<string?>();
        popup.PropertyChanged += (_, eventArgs) => raised.Add(eventArgs.PropertyName);

        popup.Style = new PopupChrome { Border = border };

        raised.ShouldContain(nameof(Popup.Style));

        raised.Clear();
        popup.Style = new PopupChrome { Border = border };

        raised.ShouldNotContain(nameof(Popup.Style));
    }

    /// <summary>Verifies Style's computed getter returns exactly the Border and Shadow components
    /// last assigned, and that resetting to default releases both back to Theme ownership.</summary>
    [Fact]
    public void Style_WhenSet_RoundTripsBorderAndShadowComponents()
    {
        using var popup = new Popup();
        var border = new Border(BorderSide.All, BorderGlyphStyle.Rounded, Color.Rgb(65, 43, 21), Color.Transparent, TerminalAttributes.None);
        var shadow = AppearanceTestValues.Shadow(visible: true);

        popup.Style = new PopupChrome { Border = border, Shadow = shadow };

        popup.Style.ShouldBe(new PopupChrome { Border = border, Shadow = shadow });
        popup.Border.ShouldBe(border);
        popup.Shadow.ShouldBe(shadow);

        popup.Style = default;

        popup.Style.ShouldBe(default);
    }

    /// <summary>Verifies unknown modal policies are rejected before observable Popup state changes.</summary>
    [Fact]
    public void ModalBehavior_WhenValueIsUnknown_ThrowsBeforeMutation()
    {
        using var popup = new Popup();
        var notifications = 0;
        popup.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Popup.ModalBehavior))
            {
                notifications++;
            }
        };

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            popup.ModalBehavior = (PopupModalBehavior) int.MaxValue);

        popup.ModalBehavior.ShouldBe(PopupModalBehavior.Auto);
        notifications.ShouldBe(0);
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
            if (eventArgs.PropertyName == nameof(ControlBase.Visibility))
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
    public async Task
        Content_WhenClosedAttachmentAcquiresManagersAndVisibilitySubscriberThrows_CleansBeforeContentPublicationAsync()
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
            var content = new OwnershipObserverControl { IsFocusable = true };
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
                if (eventArgs.PropertyName == nameof(ControlBase.Visibility))
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

    /// <summary>Verifies failed opening completes callbacks, rolls back atomically, and preserves the first failure.</summary>
    [Fact]
    public async Task IsOpen_WhenOpeningCallbacksThrow_CompletesTransitionAndPreservesFirstFailureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var content = new ProbeControl { IsFocusable = true };
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
                if (eventArgs.PropertyName != nameof(ControlBase.Visibility))
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
            order.ShouldBe(["property", "content", "focus", "property", "content"]);
            popup.IsOpen.ShouldBeFalse();
            content.Visibility.ShouldBe(Visibility.Collapsed);
            focus.Focused.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies post-content setup participates in opening aggregation and atomic rollback.</summary>
    [Fact]
    public void IsOpen_WhenContentAvailableHookThrows_CompletesHookAndPreservesEarlierFailure()
    {
        var content = new ProbeControl();
        var hookFailure = new InvalidOperationException("The content-available hook failed.");
        var expected = new InvalidOperationException("The open-state subscriber failed first.");
        var popup = new PopupContentAvailableProbe
        {
            Content = content,
            ContentAvailableFailure = hookFailure
        };
        popup.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Popup.IsOpen) && popup.IsOpen)
            {
                throw expected;
            }
        };

        var exception = Should.Throw<InvalidOperationException>(() => popup.IsOpen = true);

        exception.ShouldBeSameAs(expected);
        popup.ContentAvailableCalls.ShouldBe(1);
        popup.IsOpen.ShouldBeFalse();
        popup.SurfaceBounds.ShouldBe(default);
        content.Visibility.ShouldBe(Visibility.Collapsed);
    }

    /// <summary>Verifies detached opening aggregates every stage, rolls back, and cannot reopen during attachment.</summary>
    [Fact]
    public async Task IsOpen_WhenDetachedOpeningCallbacksThrow_RollsBackBeforeLaterAttachmentAsync()
    {
        var expected = new InvalidOperationException("The detached open-state subscriber failed.");
        var order = new List<string>();
        var content = new ProbeControl();
        var popup = new Popup { Content = content };
        popup.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName != nameof(Popup.IsOpen))
            {
                return;
            }

            order.Add(popup.IsOpen ? "property-open" : "property-rollback");
            throw popup.IsOpen
                ? expected
                : new InvalidOperationException("The detached rollback subscriber failed.");
        };
        content.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName != nameof(ControlBase.Visibility))
            {
                return;
            }

            order.Add(content.Visibility == Visibility.Visible ? "content-open" : "content-rollback");
            throw new InvalidOperationException("The detached visibility subscriber failed.");
        };

        var exception = Should.Throw<InvalidOperationException>(() => popup.IsOpen = true);

        exception.ShouldBeSameAs(expected);
        order.ShouldBe(["property-open", "content-open", "property-rollback", "content-rollback"]);
        popup.IsOpen.ShouldBeFalse();
        content.Visibility.ShouldBe(Visibility.Collapsed);

        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);

            popup.IsOpen.ShouldBeFalse();
            content.Visibility.ShouldBe(Visibility.Collapsed);
            modality.Active.ShouldBeNull();
            popup.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies closing completes every ordered stage and preserves the first callback failure.</summary>
    [Fact]
    public async Task IsOpen_WhenClosingCallbacksThrow_CompletesTransitionAndPreservesFirstFailureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var content = new ProbeControl { IsFocusable = true };
            var popup = new Popup { Content = content };
            var root = new Overlay();
            root.Children.Add(popup);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var capture = new PointerManager(root);
            popup.IsOpen = true;
            new LayoutEngine().Layout(root, new Size(12, 6));
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
                if (eventArgs.PropertyName != nameof(ControlBase.Visibility))
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

    /// <summary>Verifies a cancelled CloseRequested leaves the surface exactly as it was: still
    /// presented, with committed bounds unchanged, and no Closing or Closed notification.</summary>
    [Fact]
    public async Task CloseRequested_WhenHandlerCancels_LeavesSurfacePresentedAndRaisesNeitherClosingNorClosedAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup { Content = new ProbeControl() };
            var root = new Overlay();
            root.Children.Add(popup);
            root.Attach(dispatcher);
            popup.IsOpen = true;
            new LayoutEngine().Layout(root, new Size(12, 6));
            var openBounds = popup.SurfaceBounds;
            openBounds.ShouldNotBe(default);
            var closingCalls = 0;
            var closedCalls = 0;
            popup.CloseRequested += (_, eventArgs) => eventArgs.Cancel = true;
            popup.Closing += (_, _) => closingCalls++;
            popup.Closed += (_, _) => closedCalls++;

            popup.IsOpen = false;

            popup.IsOpen.ShouldBeTrue();
            popup.SurfaceBounds.ShouldBe(openBounds);
            closingCalls.ShouldBe(0);
            closedCalls.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a CloseRequested veto is honored while un-staging a detached Popup - the
    /// CloseUnpresented path a staged-then-cleared Opened takes never presents at all - matching
    /// the presented path's own veto guarantee instead of ignoring it outright.</summary>
    [Fact]
    public void CloseRequested_WhenStagedDetached_HonorsVeto()
    {
        var popup = new Popup { Content = new ProbeControl(), IsOpen = true };

        popup.CloseRequested += (_, eventArgs) => eventArgs.Cancel = true;
        var closingCalls = 0;
        var closedCalls = 0;
        popup.Closing += (_, _) => closingCalls++;
        popup.Closed += (_, _) => closedCalls++;

        popup.IsOpen = false;

        popup.IsOpen.ShouldBeTrue();
        closingCalls.ShouldBe(0);
        closedCalls.ShouldBe(0);
    }

    /// <summary>Verifies an uncancelled CloseRequested still closes normally, publishing the
    /// request once before Closing and Closed each fire exactly once.</summary>
    [Fact]
    public async Task CloseRequested_WhenNotCancelled_PublishesRequestThenClosingThenClosedExactlyOnceAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup { Content = new ProbeControl() };
            var root = new Overlay();
            root.Children.Add(popup);
            root.Attach(dispatcher);
            popup.IsOpen = true;
            new LayoutEngine().Layout(root, new Size(12, 6));
            var order = new List<string>();
            popup.CloseRequested += (_, eventArgs) =>
            {
                order.Add("requested");
                eventArgs.Cancel.ShouldBeFalse();
                popup.IsOpen.ShouldBeTrue();
            };
            popup.Closing += (_, _) => order.Add("closing");
            popup.Closed += (_, _) => order.Add("closed");

            popup.IsOpen = false;

            order.ShouldBe(["requested", "closing", "closed"]);
            popup.IsOpen.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Closed still fires exactly once even when a Closing handler disposes the
    /// Popup synchronously, mirroring Window's equivalent contract.</summary>
    [Fact]
    public async Task Closed_WhenClosingHandlerDisposesPopupSynchronously_FiresOnceAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup { Content = new ProbeControl() };
            var root = new Overlay();
            root.Children.Add(popup);
            root.Attach(dispatcher);
            popup.IsOpen = true;
            new LayoutEngine().Layout(root, new Size(12, 6));
            var closed = 0;
            popup.Closing += (_, _) => popup.Dispose();
            popup.Closed += (_, _) => closed++;

            popup.IsOpen = false;

            closed.ShouldBe(1);
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

    /// <summary>Verifies an anchor reflow landing while this popup's own opening transition is
    /// still on the call stack - subscription is live by the time OnContentAvailable runs, and a
    /// family whose response dismisses (Flyout's policy) would otherwise try to reenter the still
    /// -active IsOpen = true call - does not reenter or throw. The internal subscription must gate
    /// on the transition itself rather than relying on every response staying reentry-safe.</summary>
    [Fact]
    public void IsOpen_WhenAnchorReflowsDuringOwnOpeningTransition_DoesNotReenterOrThrow()
    {
        var anchor = new ProbeControl(new Size(6, 1));
        anchor.Arrange(new Rect(0, 0, 6, 1), widthResolved: true, heightResolved: true);
        var popup = new PopupAnchorReflowReentrancyProbe
        {
            Anchor = anchor,
            Content = new ProbeControl(new Size(4, 2)),
            ReflowAnchorDuringOpenTo = new Rect(0, 5, 6, 1)
        };

        _ = Should.NotThrow(() => popup.IsOpen = true);

        popup.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies closing completes the inherited collapsed-child layout transactions.</summary>
    [Fact]
    public void Layout_WhenOpenPopupCloses_ClearsContentGeometryWithoutInvokingOverrides()
    {
        var content = new ProbeControl(new Size(4, 2));
        var popup = new Popup { Content = content, IsOpen = true };
        var engine = new LayoutEngine();

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
        var popup = new Popup { Content = child };
        new LayoutEngine().Layout(popup, new Size(8, 4));
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
        var anchor = new ProbeControl { Bounds = new Rect(2, 3, 2, 1) };
        var child = new ProbeControl(new Size(4, 2));
        var popup = new Popup { Anchor = anchor, Content = child, IsOpen = true };

        new LayoutEngine().Layout(popup, new Size(10, 5));

        child.Bounds.ShouldBe(new Rect(3, 2, 4, 2));
    }

    /// <summary>Verifies a flip to the opposite placement reserves the frame space the resolved
    /// side actually needs rather than the space the preferred side would have needed. With
    /// ConnectsToAnchor, Below and Above zero opposite border edges, so an asymmetric border (here
    /// missing only Bottom) makes the two placements disagree on vertical frame space; arranging
    /// the flipped child against a size still sized for the preferred placement would silently
    /// clip it one row short.</summary>
    [Fact]
    public void Arrange_WhenAsymmetricBorderFlipsPlacement_ArrangesChildAtFullDesiredHeight()
    {
        var anchor = new ProbeControl { Bounds = new Rect(2, 5, 2, 1) };
        var child = new ProbeControl(new Size(3, 5));
        var popup = new Popup
        {
            Anchor = anchor,
            Content = child,
            IsOpen = true,
            Placement = PopupPlacement.Below,
            ConnectsToAnchor = true,
            Border = new Border(
                BorderSide.Left | BorderSide.Top | BorderSide.Right,
                BorderGlyphStyle.Rounded,
                Color.Rgb(65, 43, 21),
                Color.Transparent,
                TerminalAttributes.None)
        };

        new LayoutEngine().Layout(popup, new Size(10, 10));

        child.Bounds.Height.ShouldBe(child.DesiredSize.Height);
    }

    /// <summary>Verifies an open popup owns an opaque framed surface around its content rather than leaking the child inline.</summary>
    [Fact]
    public void Render_WhenOpen_DrawsSurfaceFrameAndContainsChild()
    {
        var anchor = new ProbeControl { Bounds = new Rect(2, 0, 2, 1) };
        var child = new ProbeControl(new Size(4, 1)) { Content = "pick".AsMemory() };
        var popup = new Popup { Anchor = anchor, Content = child, IsOpen = true };
        var size = new Size(12, 6);
        new LayoutEngine().Layout(popup, size);
        using Frame frame = new(size);

        popup.Render(frame.Canvas);

        popup.SurfaceBounds.ShouldBe(new Rect(2, 1, 6, 3));
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("╭");
        FrameOracle.Get(frame, new Point(3, 2)).ShouldBe("p");
        FrameOracle.Get(frame, new Point(7, 3)).ShouldBe("╯");
        popup.HitTest(new Point(3, 2)).ShouldBeSameAs(child);
        popup.HitTest(new Point(2, 1)).ShouldBeSameAs(popup);
    }

    /// <summary>Verifies ConnectsToAnchor omits exactly the border edge that visually touches the
    /// anchor for every placement - Below/Above omit the horizontal edge nearer the anchor and
    /// Left/Right omit the vertical edge nearer the anchor - not merely for the one placement
    /// (Below) most other tests exercise.</summary>
    [Theory]
    [InlineData(PopupPlacement.Below, 5, 1, "│", "│", "╰", "╯")]
    [InlineData(PopupPlacement.Above, 5, 6, "╭", "╮", "│", "│")]
    [InlineData(PopupPlacement.Right, 1, 3, "─", "╮", "─", "╯")]
    [InlineData(PopupPlacement.Left, 9, 3, "╭", "─", "╰", "─")]
    public void Render_WhenConnectsToAnchor_OmitsBorderEdgeTouchingAnchor(
        PopupPlacement placement,
        int anchorX,
        int anchorY,
        string expectedTopLeft,
        string expectedTopRight,
        string expectedBottomLeft,
        string expectedBottomRight)
    {
        var anchor = new ProbeControl { Bounds = new Rect(anchorX, anchorY, 2, 1) };
        var child = new ProbeControl(new Size(3, 1));
        var popup = new Popup
        {
            Anchor = anchor,
            Content = child,
            IsOpen = true,
            Placement = placement,
            ConnectsToAnchor = true
        };
        var size = new Size(12, 8);
        new LayoutEngine().Layout(popup, size);
        using Frame frame = new(size);

        popup.Render(frame.Canvas);

        var bounds = popup.SurfaceBounds;
        FrameOracle.Get(frame, new Point(bounds.X, bounds.Y)).ShouldBe(expectedTopLeft);
        FrameOracle.Get(frame, new Point(bounds.Right - 1, bounds.Y)).ShouldBe(expectedTopRight);
        FrameOracle.Get(frame, new Point(bounds.X, bounds.Bottom - 1)).ShouldBe(expectedBottomLeft);
        FrameOracle.Get(frame, new Point(bounds.Right - 1, bounds.Bottom - 1)).ShouldBe(expectedBottomRight);
    }

    /// <summary>Verifies retained child shadows cannot replace the owning Popup frame.</summary>
    [Fact]
    public void Render_WhenContentShadowTouchesFrame_PaintsPopupFrameAfterContent()
    {
        var anchor = new ProbeControl { Bounds = new Rect(2, 0, 2, 1) };
        var content = new Button
        {
            Width = Length.Cells(3),
            Height = Length.Cells(2),
            Style = TestButtonStyles.WithShadow(
                AppearanceTestValues.Shadow(mode: ShadowMode.BlockGlyph, offset: new Point(1, 1), glyph: new Rune('▓'))),
        };
        var popup = new Popup { Anchor = anchor, Content = content, IsOpen = true };
        var size = new Size(12, 6);
        new LayoutEngine().Layout(popup, size);
        using Frame frame = new(size);

        popup.Render(frame.Canvas);

        FrameOracle.Get(
            frame,
            new Point(popup.SurfaceBounds.Right - 1, popup.SurfaceBounds.Y + 2)).ShouldBe("│");
        FrameOracle.Get(
            frame,
            new Point(popup.SurfaceBounds.X + 3, popup.SurfaceBounds.Bottom - 1)).ShouldBe("─");
    }

    /// <summary>Verifies an open popup is painted and hit-tested above later ordinary siblings in its owning overlay.</summary>
    [Fact]
    public void Render_WhenLaterSiblingOverlaps_PopupRetainsTopmostInputAndSurface()
    {
        var comboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(10),
            Height = Length.Cells(1),
            Items = ["Small", "Large"],
            DropDownHeight = Length.Cells(2),
            IsOpen = true
        };
        var cover = new Dock { Face = AppearanceTestValues.Face(background: ReferenceColors.Get(7)) };
        var root = new Overlay { ClipToBounds = false };
        root.Children.Add(comboBox);
        root.Children.Add(cover);
        var size = new Size(16, 8);
        new LayoutEngine().Layout(root, size);
        var list = OwnedTree.Find<UiListView>(comboBox).ShouldNotBeNull();
        var point = new Point(list.Bounds.X + 1, list.Bounds.Y);
        using Frame frame = new(size);

        root.Render(frame.Canvas);

        cover.Bounds.ShouldBe(root.Bounds);
        frame.GetCell(new Point(15, 7)).Style.Background.ShouldBe(ReferenceColors.Get(7));
        _ = root.HitTest(point).ShouldBeOfType<ListItem>();
        FrameOracle.Get(frame, point).ShouldBe("m");
    }

    /// <summary>Verifies saturated anchor edges still flip to the fitting opposite side.</summary>
    [Theory]
    [InlineData(PopupPlacement.Right, PopupPlacement.Left)]
    [InlineData(PopupPlacement.Below, PopupPlacement.Above)]
    public void Arrange_WhenAnchorEdgeIsNearIntegerMaximum_ResolvesOppositePlacement(
        PopupPlacement preferred,
        PopupPlacement expected)
    {
        var rootBounds = new Rect(int.MaxValue - 20, int.MaxValue - 10, 20, 10);
        var anchor = new ProbeControl
        {
            Bounds = preferred == PopupPlacement.Right
                ? new Rect(int.MaxValue - 3, int.MaxValue - 8, 3, 1)
                : new Rect(int.MaxValue - 18, int.MaxValue - 2, 2, 2)
        };
        var child = new ProbeControl(new Size(4, 2));
        var popup = new Popup { Anchor = anchor, Content = child, Placement = preferred, IsOpen = true };
        var root = new TraversalOwner { Bounds = rootBounds };
        root.AddNormal(anchor);
        root.AddPopup(popup);
        popup.Measure(new Constraint(rootBounds.Width, rootBounds.Height));

        popup.Arrange(rootBounds, widthResolved: true, heightResolved: true);

        popup.ResolvedPlacement.ShouldBe(expected);
        if (expected == PopupPlacement.Left)
        {
            popup.SurfaceBounds.Right.ShouldBeLessThanOrEqualTo(anchor.Bounds.X);
        }
        else
        {
            popup.SurfaceBounds.Bottom.ShouldBeLessThanOrEqualTo(anchor.Bounds.Y);
        }
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
            Items = ["pick"]
        };
        var size = new Size(12, 6);
        new LayoutEngine().Layout(box, size);
        using Frame frame = new(size);

        box.Render(frame.Canvas);

        var popup = OwnedTree.Find<Popup>(box).ShouldNotBeNull();
        popup.OwningSlot.ShouldNotBeNull().Options.Layer.ShouldBe(OwnedControlLayer.Popup);
        var list = OwnedTree.Find<UiListView>(popup).ShouldNotBeNull();
        FrameOracle.Get(frame, new Point(list.Bounds.X, list.Bounds.Y)).ShouldBe("p");
    }

    /// <summary>Verifies Escape bubbles through popup content and closes the owner.</summary>
    [Fact]
    public void Dispatch_WhenEscapeArrives_ClosesOpenPopup()
    {
        var child = new ProbeControl();
        var popup = new Popup { Content = child, IsOpen = true };
        var eventArgs = new KeyEventArgs(new Stroke(
            Code.Escape,
            default,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press));

        _ = Router.Route(child, Events.Key, eventArgs);

        popup.IsOpen.ShouldBeFalse();
        eventArgs.IsHandled.ShouldBeTrue();
    }

    /// <summary>Verifies opening transfers focus to a focusable popup child for keyboard-driven pickers.</summary>
    [Fact]
    public async Task IsOpen_WhenFocusableChildExists_MovesFocusToChildAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var child = new UiListView { Items = ["first", "second"] };
            var popup = new Popup { Content = child };
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
            var child = new ProbeControl { IsFocusable = true };
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

    /// <summary>Verifies Popup autofocus shares the modal resolver's deterministic descendant-first
    /// policy when both the content root and one of its retained parts can focus.</summary>
    [Fact]
    public async Task IsOpen_WhenContentAndOwnedPartAreFocusable_MovesFocusToOwnedPartAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var content = new TraversalOwner { IsFocusable = true };
            var child = new ProbeControl { IsFocusable = true };
            content.AddNormal(child);
            var popup = new Popup { Content = content, ModalBehavior = PopupModalBehavior.None };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            popup.IsOpen = true;

            focus.Focused.ShouldBeSameAs(child);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies SuppressCloseOtherPopups prevents sibling popup close on open.</summary>
    [Fact]
    public void SuppressCloseOtherPopups_WhenTrue_DoesNotCloseOtherPopups()
    {
        var anchor = new ProbeControl(new Size(6, 1));
        var firstPopup = new Popup { Anchor = anchor, Content = new ProbeControl(new Size(4, 2)) };
        var secondPopup = new Popup
        {
            Anchor = anchor,
            Content = new ProbeControl(new Size(4, 2)),
            SuppressCloseOtherPopups = true,
            ModalBehavior = PopupModalBehavior.None
        };
        var root = new Overlay();
        root.Children.Add(anchor);
        root.Children.Add(firstPopup);
        root.Children.Add(secondPopup);

        firstPopup.IsOpen = true;
        secondPopup.IsOpen = true;

        firstPopup.IsOpen.ShouldBeTrue();
        secondPopup.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies ModalBehavior.None skips auto-modal scope entry.</summary>
    [Fact]
    public async Task ModalBehavior_WhenNone_SkipsAutoModalScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            var anchor = new ProbeControl(new Size(6, 1));
            var popup = new Popup
            {
                Anchor = anchor,
                Content = new ProbeControl(new Size(4, 2)),
                ModalBehavior = PopupModalBehavior.None
            };
            var root = new Overlay();
            root.Children.Add(anchor);
            root.Children.Add(popup);
            root.Attach(dispatcher);

            popup.IsOpen = true;

            // Simulate a press outside the popup surface — should NOT close
            // because light dismiss was not registered.
            var outsidePoint = new Point(0, 0);
            var pointer = new Pointer(
                outsidePoint,
                pixels: null,
                Buttons.Primary,
                PointerAction.Press,
                wheelX: 0,
                wheelY: 0,
                Modifiers.None,
                isMotion: false,
                isCellPositionInferred: false);
            var eventArgs = new PointerEventArgs(pointer);
            _ = Router.Route(root, Events.Pointer, eventArgs);

            popup.IsOpen.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies ordinary attached opening enters the default dismissing modal presentation.</summary>
    [Fact]
    public async Task IsOpen_WhenAttached_EntersDefaultDismissPresentationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { IsFocusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);

            popup.IsOpen = true;

            popup.IsOpen.ShouldBeTrue();
            var scope = modality.Active.ShouldNotBeNull();
            scope.Root.ShouldBeSameAs(popup);
            scope.OutsideInteraction.ShouldBe(OutsideInteraction.Dismiss);
            focus.Focused.ShouldBeSameAs(action);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a retained Popup can delegate modal lifetime to its logical framework owner.</summary>
    [Fact]
    public async Task IsOpen_WhenModalityIsOwnerManaged_DoesNotEnterASecondScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup
            {
                Content = new ProbeControl { IsFocusable = true },
                ModalBehavior = PopupModalBehavior.None
            };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);

            popup.IsOpen = true;

            popup.IsOpen.ShouldBeTrue();
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies one popup cannot own two simultaneous modal presentations.</summary>
    [Fact]
    public async Task OpenModal_WhenPresentationIsAlreadyLive_RejectsDuplicateWithoutDisturbingFirstAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { IsFocusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = popup.OpenModal();

            var exception = Should.Throw<InvalidOperationException>(() => popup.OpenModal());

            exception.Message.ShouldBe("The Popup already has an active modal presentation.");
            popup.IsOpen.ShouldBeTrue();
            action.Visibility.ShouldBe(Visibility.Visible);
            scope.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(scope);
            focus.Focused.ShouldBeSameAs(action);
            scope.Dispose();

            using var replacement = popup.OpenModal();

            replacement.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(replacement);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a duplicate attempted from modal entry callbacks cannot disturb the entering presentation.</summary>
    [Fact]
    public async Task OpenModal_WhenFocusCallbackReenters_RejectsNestedCallAndKeepsOuterPresentationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { IsFocusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            InvalidOperationException? nested = null;
            focus.Gained += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Current, action))
                {
                    nested = Should.Throw<InvalidOperationException>(() => popup.OpenModal());
                }
            };

            using var scope = popup.OpenModal();

            nested.ShouldNotBeNull().Message.ShouldBe("Popup modal presentations cannot be reentered.");
            popup.IsOpen.ShouldBeTrue();
            scope.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(scope);
            focus.Focused.ShouldBeSameAs(action);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies external disposal closes the default presentation instead of leaving a modeless Popup.</summary>
    [Fact]
    public async Task IsOpen_WhenDefaultScopeExits_ClosesAndAllowsReopenAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { IsFocusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            popup.IsOpen = true;
            var first = modality.Active.ShouldNotBeNull();

            first.Dispose();

            first.IsActive.ShouldBeFalse();
            popup.IsOpen.ShouldBeFalse();
            action.Visibility.ShouldBe(Visibility.Collapsed);
            modality.Active.ShouldBeNull();

            popup.IsOpen = true;
            var second = modality.Active.ShouldNotBeNull();

            second.ShouldNotBeSameAs(first);
            second.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(second);
            popup.IsOpen.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies reentrant replacement from an exit callback cannot be cleared as the old scope unwinds.</summary>
    [Fact]
    public async Task OpenModal_WhenExternalExitCallbackReopens_TracksReplacementByIdentityAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { IsFocusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var first = popup.OpenModal();
            ModalScope? replacement = null;
            first.Exited += (_, _) => replacement = popup.OpenModal();

            first.Dispose();

            first.IsActive.ShouldBeFalse();
            replacement.ShouldNotBeNull().IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(replacement);
            popup.IsOpen.ShouldBeTrue();
            replacement.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a scope disposed from entry callbacks is returned inactive without stale Popup tracking.</summary>
    [Fact]
    public async Task OpenModal_WhenEntryCallbackDisposesScope_ReturnsInactiveAndAllowsReopenAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { IsFocusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { popup } };
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

            var first = popup.OpenModal();

            first.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            popup.IsOpen.ShouldBeTrue();
            disposeOnEntry = false;

            using var second = popup.OpenModal();

            second.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(second);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a popup closed from entry callbacks also disposes the untracked returned scope.</summary>
    [Fact]
    public async Task OpenModal_WhenEntryCallbackClosesPopup_ReturnsInactiveWithoutStrandedScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { IsFocusable = true };
            var action = new ProbeControl { IsFocusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { background, popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            var closeOnEntry = true;
            focus.Gained += (_, eventArgs) =>
            {
                if (closeOnEntry && ReferenceEquals(eventArgs.Current, action))
                {
                    popup.IsOpen = false;
                }
            };

            var scope = popup.OpenModal();

            scope.IsActive.ShouldBeFalse();
            popup.IsOpen.ShouldBeFalse();
            action.Visibility.ShouldBe(Visibility.Collapsed);
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeSameAs(background);
            closeOnEntry = false;

            using var recovered = popup.OpenModal(initialFocus: action);

            recovered.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(recovered);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies ordinary close publishes Closing before modality exit and content collapse.</summary>
    [Fact]
    public async Task IsOpen_WhenModalPopupCloses_PublishesClosingBeforeExitAndRestoresBackgroundFocusAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { IsFocusable = true };
            var action = new ProbeControl { IsFocusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { background, popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            var scope = popup.OpenModal(initialFocus: action);
            var closingCalls = 0;
            popup.Closing += (_, _) =>
            {
                closingCalls++;
                popup.IsOpen.ShouldBeFalse();
                action.Visibility.ShouldBe(Visibility.Visible);
                scope.IsActive.ShouldBeTrue();
                modality.Active.ShouldBeSameAs(scope);
                focus.Focused.ShouldBeSameAs(action);
            };

            popup.IsOpen = false;

            closingCalls.ShouldBe(1);
            action.Visibility.ShouldBe(Visibility.Collapsed);
            focus.Focused.ShouldBeSameAs(background);
            scope.IsActive.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an earlier Closing failure outranks modal exit failure without suppressing cleanup.</summary>
    [Fact]
    public async Task IsOpen_WhenClosingAndModalExitCallbacksFail_CompletesCloseAndPreservesClosingFailureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var expected = new InvalidOperationException("The modal exit callback failed.");
            var action = new ProbeControl { IsFocusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var scope = popup.OpenModal();
            scope.Exited += (_, _) => throw expected;
            popup.Closing += (_, _) => throw new InvalidOperationException("The closing callback failed.");
            var closed = 0;
            popup.Closed += (_, _) => closed++;

            var exception = Should.Throw<InvalidOperationException>(() => popup.IsOpen = false);

            exception.Message.ShouldBe("The closing callback failed.");
            popup.IsOpen.ShouldBeFalse();
            action.Visibility.ShouldBe(Visibility.Collapsed);
            scope.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            closed.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies invalid focus rolls back only a popup exposed by the failing call.</summary>
    [Fact]
    public async Task OpenModal_WhenInitialFocusIsOutsideNewPopup_ReclosesExposedPopupAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { IsFocusable = true };
            var action = new ProbeControl { IsFocusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { background, popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();

            var exception = Should.Throw<ArgumentException>(() => popup.OpenModal(initialFocus: background));

            exception.ParamName.ShouldBe("initialFocus");
            popup.IsOpen.ShouldBeFalse();
            action.Visibility.ShouldBe(Visibility.Collapsed);
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeSameAs(background);

            using var recovered = popup.OpenModal(initialFocus: action);

            recovered.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(recovered);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies failed modal promotion does not close a pre-existing modeless presentation.</summary>
    [Fact]
    public async Task OpenModal_WhenInitialFocusIsOutsideModelessPopup_PreservesOpenPresentationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { IsFocusable = true };
            var action = new ProbeControl { IsFocusable = true };
            var popup = new Popup { Content = action, FocusOnOpen = false, IsOpen = true };
            var root = new Overlay { Children = { background, popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();

            _ = Should.Throw<ArgumentException>(() => popup.OpenModal(initialFocus: background));

            popup.IsOpen.ShouldBeTrue();
            action.Visibility.ShouldBe(Visibility.Visible);
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeSameAs(background);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies rollback failure cannot replace the initiating open-transition failure.</summary>
    [Fact]
    public async Task OpenModal_WhenExposureAndRollbackCallbacksFail_PreservesInitiatingExceptionAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var expected = new InvalidOperationException("The opening callback failed.");
            var popup = new Popup { Content = new ProbeControl { IsFocusable = true } };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            popup.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(Popup.IsOpen) && popup.IsOpen)
                {
                    throw expected;
                }
            };
            popup.Closing += (_, _) => throw new InvalidOperationException("The rollback callback failed.");

            var exception = Should.Throw<InvalidOperationException>(() => popup.OpenModal());

            exception.ShouldBeSameAs(expected);
            popup.IsOpen.ShouldBeFalse();
            popup.Content.ShouldNotBeNull().Visibility.ShouldBe(Visibility.Collapsed);
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies modal-entry failure remains authoritative when visual rollback also fails.</summary>
    [Fact]
    public async Task OpenModal_WhenEntryAndClosingCallbacksFail_PreservesEntryExceptionAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var expected = new InvalidOperationException("The modal focus callback failed.");
            var background = new ProbeControl { IsFocusable = true };
            var action = new ProbeControl { IsFocusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { background, popup } };
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
            popup.Closing += (_, _) => throw new InvalidOperationException("The visual rollback callback failed.");

            var exception = Should.Throw<InvalidOperationException>(() => popup.OpenModal());

            exception.ShouldBeSameAs(expected);
            popup.IsOpen.ShouldBeFalse();
            action.Visibility.ShouldBe(Visibility.Collapsed);
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeSameAs(background);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies policy validation occurs before a closed popup is exposed.</summary>
    [Fact]
    public async Task OpenModal_WhenOutsideInteractionIsUndefined_ThrowsBeforeMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { IsFocusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);

            var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
                popup.OpenModal((OutsideInteraction) int.MaxValue));

            exception.ParamName.ShouldBe("outsideInteraction");
            popup.IsOpen.ShouldBeFalse();
            action.Visibility.ShouldBe(Visibility.Collapsed);
            modality.Active.ShouldBeNull();

            using var recovered = popup.OpenModal();

            recovered.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(recovered);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a detached popup - one with no attached application tree, and therefore
    /// no <see cref="ModalityManager"/> to enter - rejects OpenModal instead of silently exposing
    /// content nothing can route input through.</summary>
    [Fact]
    public void OpenModal_WhenPopupIsDetached_ThrowsInvalidOperationException()
    {
        var action = new ProbeControl { IsFocusable = true };
        using var popup = new Popup { Content = action };

        var exception = Should.Throw<InvalidOperationException>(() => popup.OpenModal());

        exception.Message.ShouldBe("A modal Popup must belong to an attached application tree.");
        popup.IsOpen.ShouldBeFalse();
        action.Visibility.ShouldBe(Visibility.Collapsed);
    }

    /// <summary>Verifies OpenModal called synchronously from a callback that runs during this
    /// exact popup's own open-state transition - not a nested OpenModal call, which
    /// <see cref="OpenModal_WhenFocusCallbackReenters_RejectsNestedCallAndKeepsOuterPresentationAsync"/>
    /// already covers - is rejected before it can enter a second, conflicting transition.</summary>
    [Fact]
    public async Task OpenModal_WhenCalledDuringOwnOpenStateTransition_ThrowsInvalidOperationExceptionAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { IsFocusable = true };
            var popup = new Popup { ModalBehavior = PopupModalBehavior.None, Content = action };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            InvalidOperationException? nested = null;
            popup.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(Popup.IsOpen) && popup.IsOpen)
                {
                    nested = Should.Throw<InvalidOperationException>(() => popup.OpenModal());
                }
            };

            popup.IsOpen = true;

            nested.ShouldNotBeNull().Message.ShouldBe(
                "Popup modal presentation cannot begin during an open-state transition.");
            popup.IsOpen.ShouldBeTrue();
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies setting IsOpen to its own current value is a complete no-op: no
    /// PropertyChanged, no Closing/Closed, and no repeated content-availability work, matching
    /// every other SetProperty-backed member's own no-op contract.</summary>
    [Fact]
    public void IsOpen_WhenSetToCurrentValue_IsNoOp()
    {
        using var popup = new Popup { Content = new ProbeControl() };
        var notifications = 0;
        popup.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Popup.IsOpen))
            {
                notifications++;
            }
        };

        popup.IsOpen = false;

        notifications.ShouldBe(0);

        popup.IsOpen = true;
        notifications = 0;
        var closingRaised = false;
        var closedRaised = false;
        popup.Closing += (_, _) => closingRaised = true;
        popup.Closed += (_, _) => closedRaised = true;

        popup.IsOpen = true;

        notifications.ShouldBe(0);
        closingRaised.ShouldBeFalse();
        closedRaised.ShouldBeFalse();
        popup.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies a caller-selected eligible descendant receives modal entry focus.</summary>
    [Fact]
    public async Task OpenModal_WhenInitialFocusIsProvided_FocusesThatDescendantAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var first = new ProbeControl { IsFocusable = true };
            var second = new ProbeControl { IsFocusable = true };
            var content = new Overlay { Children = { first, second } };
            var popup = new Popup { Content = content };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);

            using var scope = popup.OpenModal(OutsideInteraction.Ignore, second);

            scope.OutsideInteraction.ShouldBe(OutsideInteraction.Ignore);
            focus.Focused.ShouldBeSameAs(second);
            popup.IsOpen.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies reentrant modal opening is rejected and the outer failed exposure rolls back.</summary>
    [Fact]
    public async Task OpenModal_WhenOpenNotificationReenters_RejectsNestedPresentationAndReclosesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup { Content = new ProbeControl { IsFocusable = true } };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            Exception? nested = null;
            popup.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName != nameof(Popup.IsOpen) || !popup.IsOpen || nested is not null)
                {
                    return;
                }

                nested = Should.Throw<InvalidOperationException>(() => popup.OpenModal());
                throw nested;
            };

            var exception = Should.Throw<InvalidOperationException>(() => popup.OpenModal());

            exception.ShouldBeSameAs(nested);
            popup.IsOpen.ShouldBeFalse();
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a popup opened from within another popup's modal content stacks scopes correctly.</summary>
    [Fact]
    public async Task OpenModal_WhenInnerPopupOpensInsideOuterModal_StacksScopesAndUnwindsInOrderAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { IsFocusable = true };
            var outerAction = new ProbeControl { IsFocusable = true };
            var innerAction = new ProbeControl { IsFocusable = true };
            var innerPopup = new Popup { Content = innerAction };
            var outerPopup = new Popup { Content = new Overlay { Children = { outerAction, innerPopup } } };
            var root = new Overlay { Children = { background, outerPopup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();

            var outerScope = outerPopup.OpenModal(initialFocus: outerAction);

            outerScope.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(outerScope);
            focus.Focused.ShouldBeSameAs(outerAction);

            var innerScope = innerPopup.OpenModal(initialFocus: innerAction);

            innerScope.IsActive.ShouldBeTrue();
            outerScope.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(innerScope);
            focus.Focused.ShouldBeSameAs(innerAction);

            innerScope.Dispose();

            innerScope.IsActive.ShouldBeFalse();
            outerScope.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(outerScope);
            focus.Focused.ShouldBeSameAs(outerAction);

            outerScope.Dispose();

            outerScope.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeSameAs(background);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies rapid modal open/close cycles don't accumulate stale scope state.</summary>
    [Fact]
    public async Task OpenModal_WhenCycledRapidly_DoesNotAccumulateStaleStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup { Content = new ProbeControl { IsFocusable = true } };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);

            for (var i = 0; i < 20; i++)
            {
                var scope = popup.OpenModal();
                scope.Dispose();
            }

            modality.Active.ShouldBeNull();

            using var final = popup.OpenModal();

            final.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(final);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disposing the popup's owner while the popup is modal cleanly exits the scope.</summary>
    [Fact]
    public async Task Dispose_WhenOwnerIsDisposedDuringModal_ExitsScopeWithoutCrashAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { IsFocusable = true };
            var popup = new Popup { Content = new ProbeControl { IsFocusable = true } };
            var root = new Overlay { Children = { background, popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();

            var scope = popup.OpenModal();

            scope.IsActive.ShouldBeTrue();

            popup.Dispose();

            scope.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies detachment reconciles open state and permits one explicit presentation after reattachment.</summary>
    [Fact]
    public async Task Detach_WhenOpenPopupIsReattached_ReopensOnePresentationWithoutLifecycleCloseAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup { Content = new ProbeControl { IsFocusable = true } };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            popup.IsOpen = true;
            var first = modality.Active.ShouldNotBeNull();
            var closing = 0;
            var closed = 0;
            popup.Closing += (_, _) => closing++;
            popup.Closed += (_, _) => closed++;

            root.Children.Remove(popup).ShouldBeTrue();

            popup.IsOpen.ShouldBeFalse();
            popup.SurfaceBounds.ShouldBe(default);
            first.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            closing.ShouldBe(0);
            closed.ShouldBe(0);

            root.Children.Add(popup);

            popup.IsOpen.ShouldBeFalse();
            modality.Active.ShouldBeNull();

            popup.IsOpen = true;

            var second = modality.Active.ShouldNotBeNull();
            second.ShouldNotBeSameAs(first);
            second.Root.ShouldBeSameAs(popup);
            closing.ShouldBe(0);
            closed.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a forced close continuation cannot collapse content belonging to a
    /// newer open transition started synchronously by the close-state notification.</summary>
    [Fact]
    public async Task Visibility_WhenCloseNotificationRestoresAndReopensPopup_PreservesNewOpenContentAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var content = new ProbeControl { IsFocusable = true };
            var popup = new Popup { Content = content, ModalBehavior = PopupModalBehavior.None };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            popup.IsOpen = true;
            var reopened = false;
            popup.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(Popup.IsOpen) && !popup.IsOpen && !reopened)
                {
                    reopened = true;
                    popup.Visibility = Visibility.Visible;
                    popup.IsOpen = true;
                }
            };

            popup.Visibility = Visibility.Collapsed;

            reopened.ShouldBeTrue();
            popup.IsOpen.ShouldBeTrue();
            popup.Visibility.ShouldBe(Visibility.Visible);
            content.Visibility.ShouldBe(Visibility.Visible);
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies removing an ancestor of an open popup — not the popup itself — still releases
    /// presentation, so the popup can reopen after the ancestor is reattached instead of permanently
    /// failing FloatingSurfaceBase's already-open guard.</summary>
    [Fact]
    public async Task Detach_WhenAncestorOfOpenPopupIsRemoved_ReleasesPresentationAndPermitsReopenAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup { Content = new ProbeControl { IsFocusable = true } };
            var holder = new Overlay { Children = { popup } };
            var root = new Overlay { Children = { holder } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            popup.IsOpen = true;
            _ = modality.Active.ShouldNotBeNull();

            root.Children.Remove(holder).ShouldBeTrue();

            popup.IsOpen.ShouldBeFalse();
            popup.SurfaceBounds.ShouldBe(default);
            modality.Active.ShouldBeNull();

            root.Children.Add(holder);

            popup.IsOpen.ShouldBeFalse();

            _ = Should.NotThrow(() => popup.IsOpen = true);

            popup.IsOpen.ShouldBeTrue();
            var scope = modality.Active.ShouldNotBeNull();
            scope.Root.ShouldBeSameAs(popup);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disabling preserves presentation while ending and later restoring automatic modality.
    /// This is also the detached-unit disabled-contract evidence for Popup: it proves direct
    /// disable, stable geometry across the disabled window, and re-enable recovery.</summary>
    [Fact]
    public async Task IsEnabled_WhenOpenPopupIsDisabled_PreservesPresentationAndRestoresAutomaticModalityAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup { Content = new ProbeControl { IsFocusable = true } };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            popup.IsOpen = true;
            var first = modality.Active.ShouldNotBeNull();
            new LayoutEngine().Layout(root, new Size(12, 6));
            var bounds = popup.SurfaceBounds;

            popup.IsEnabled = false;

            popup.IsOpen.ShouldBeTrue();
            popup.SurfaceBounds.ShouldBe(bounds);
            first.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();

            popup.IsEnabled = true;

            popup.IsOpen.ShouldBeTrue();
            var second = modality.Active.ShouldNotBeNull();
            second.ShouldNotBeSameAs(first);
            second.Root.ShouldBeSameAs(popup);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies hiding reconciles open state and showing permits an explicit new presentation.</summary>
    [Fact]
    public async Task Visibility_WhenOpenPopupIsHiddenAndShown_ReconcilesPresentationAndModalityAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup { Content = new ProbeControl { IsFocusable = true } };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            popup.IsOpen = true;
            var first = modality.Active.ShouldNotBeNull();
            new LayoutEngine().Layout(root, new Size(12, 6));
            popup.SurfaceBounds.ShouldNotBe(default);

            popup.Visibility = Visibility.Hidden;

            popup.IsOpen.ShouldBeFalse();
            popup.SurfaceBounds.ShouldBe(default);
            first.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();

            popup.Visibility = Visibility.Visible;

            popup.IsOpen.ShouldBeFalse();
            modality.Active.ShouldBeNull();

            popup.IsOpen = true;

            var second = modality.Active.ShouldNotBeNull();
            second.ShouldNotBeSameAs(first);
            second.Root.ShouldBeSameAs(popup);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies automatic modality follows the nearest disabled ancestor until the full chain recovers.</summary>
    [Fact]
    public async Task IsEnabled_WhenParentAndGrandparentRecover_RestoresExactlyOneAutomaticScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup { Content = new ProbeControl { IsFocusable = true } };
            var parent = new Overlay { Children = { popup } };
            var grandparent = new Overlay { Children = { parent } };
            grandparent.Attach(dispatcher);
            using var focus = new FocusManager(grandparent);
            using var pointer = new PointerManager(grandparent);
            using var modality = new ModalityManager(grandparent, focus, pointer);
            popup.IsOpen = true;
            var first = modality.Active.ShouldNotBeNull();

            parent.IsEnabled = false;

            popup.IsOpen.ShouldBeTrue();
            first.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();

            grandparent.IsEnabled = false;
            parent.IsEnabled = true;

            popup.IsOpen.ShouldBeTrue();
            modality.Active.ShouldBeNull();

            grandparent.IsEnabled = true;

            var restored = modality.Active.ShouldNotBeNull();
            restored.ShouldNotBeSameAs(first);
            restored.Root.ShouldBeSameAs(popup);
            restored.IsActive.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies automatic modality follows the nearest hidden ancestor until the full chain recovers.</summary>
    [Fact]
    public async Task Visibility_WhenParentAndGrandparentRecover_RestoresExactlyOneAutomaticScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup { Content = new ProbeControl { IsFocusable = true } };
            var parent = new Overlay { Children = { popup } };
            var grandparent = new Overlay { Children = { parent } };
            grandparent.Attach(dispatcher);
            using var focus = new FocusManager(grandparent);
            using var pointer = new PointerManager(grandparent);
            using var modality = new ModalityManager(grandparent, focus, pointer);
            popup.IsOpen = true;
            var first = modality.Active.ShouldNotBeNull();

            parent.Visibility = Visibility.Hidden;

            popup.IsOpen.ShouldBeTrue();
            first.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();

            grandparent.Visibility = Visibility.Hidden;
            parent.Visibility = Visibility.Visible;

            popup.IsOpen.ShouldBeTrue();
            modality.Active.ShouldBeNull();

            grandparent.Visibility = Visibility.Visible;

            var restored = modality.Active.ShouldNotBeNull();
            restored.ShouldNotBeSameAs(first);
            restored.Root.ShouldBeSameAs(popup);
            restored.IsActive.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    private static Theme PopupTheme(string pointingUp) => ThemeCatalog.Parse(ThemeJson.Create().Replace(
        "\"popup\": { \"normal\": { \"border\": { \"sides\":\"all\", \"glyphStyle\":\"rounded\" } } }",
        $"\"popup\": {{ \"normal\": {{ \"border\": {{ \"sides\":\"all\", \"glyphStyle\":\"rounded\" }}, \"anchorGlyphs\": {{ \"pointingUp\": \"{pointingUp}\" }} }} }}",
        StringComparison.Ordinal));
}
