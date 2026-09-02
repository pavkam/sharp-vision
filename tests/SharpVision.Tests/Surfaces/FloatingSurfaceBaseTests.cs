// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Surfaces;

using System.Reflection;

using SharpVision.Surfaces;

/// <summary>Verifies the shared lifecycle and modality contract for elevated surfaces.</summary>
public sealed class FloatingSurfaceBaseTests
{
    /// <summary>Verifies disposal releases subscribers retained by every lifecycle event.</summary>
    [Fact]
    public void Dispose_WhenOpenedHasSubscriber_ReleasesSubscriber()
    {
        var (surface, listener) = CreateDisposedSurfaceWithOpenedListener();

        for (var attempt = 0; attempt < 5 && listener.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        GC.KeepAlive(surface);
        listener.IsAlive.ShouldBeFalse();
    }

    /// <summary>Verifies the base models single content without exposing panel ownership.</summary>
    [Fact]
    public void Type_WhenInspected_IsContentSurfaceWithoutPublicChildren()
    {
        var type = typeof(FloatingSurfaceBase);

        type.BaseType.ShouldBe(typeof(ContentControl));
        typeof(Container).IsAssignableFrom(type).ShouldBeFalse();
        type.Namespace.ShouldBe("SharpVision.Surfaces");
        type.GetProperty(nameof(Container.Children)).ShouldBeNull();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (FloatingSurfaceProbe Surface, WeakReference Listener) CreateDisposedSurfaceWithOpenedListener()
    {
        var surface = new FloatingSurfaceProbe();
        var listener = new FloatingSurfaceOpenedListener();
        surface.Opened += listener.Handle;
        surface.Dispose();
        return (surface, new WeakReference(listener));
    }

    /// <summary>Verifies only in-assembly surface families may suppress an already-published Closing event.</summary>
    [Fact]
    public void CloseSurfaceAfterClosingRequest_WhenInspected_IsPrivateProtected()
    {
        var method = typeof(FloatingSurfaceBase).GetMethod(
            "CloseSurfaceAfterClosingRequest",
            BindingFlags.Instance | BindingFlags.NonPublic);

        _ = method.ShouldNotBeNull();
        (method.Attributes & MethodAttributes.MemberAccessMask).ShouldBe(MethodAttributes.FamANDAssem);
    }

    /// <summary>Verifies detached presentation is rejected without committing common surface state.</summary>
    [Fact]
    public void Open_WhenSurfaceIsDetached_ThrowsBeforeMutation()
    {
        using var surface = new FloatingSurfaceProbe();

        var exception = Should.Throw<InvalidOperationException>(() =>
            surface.PublishBounds(new Rect(1, 2, 3, 4)));

        exception.Message.ShouldContain("attached");
        surface.IsPresented.ShouldBeFalse();
        surface.SurfaceBounds.ShouldBe(default);
    }

    /// <summary>Verifies a family commit cannot recursively open the same surface.</summary>
    [Fact]
    public async Task Open_WhenFamilyCommitReentersOpen_RejectsReentryWithoutCommonStateAsync()
    {
        using var probe = new FloatingSurfaceProbe();
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        InvalidOperationException? exception = null;

        await surface.UpdateAsync(
            () => exception = Should.Throw<InvalidOperationException>(() =>
                probe.OpenForTest(() => probe.PublishBounds(new Rect(1, 2, 3, 4)))),
            "reject reentrant floating surface opening");

        exception.ShouldNotBeNull().Message.ShouldContain("reentered");
        probe.IsPresented.ShouldBeFalse();
        probe.SurfaceBounds.ShouldBe(default);
    }

    /// <summary>Verifies a family commit cannot close before opening has committed.</summary>
    [Fact]
    public async Task Open_WhenFamilyCommitCloses_RejectsTransitionWithoutCommonStateAsync()
    {
        using var probe = new FloatingSurfaceProbe();
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        InvalidOperationException? exception = null;

        await surface.UpdateAsync(
            () => exception = Should.Throw<InvalidOperationException>(() =>
                probe.OpenForTest(() => probe.CloseForTest())),
            "reject closing during floating surface opening");

        exception.ShouldNotBeNull().Message.ShouldContain("opening");
        probe.IsPresented.ShouldBeFalse();
        probe.SurfaceBounds.ShouldBe(default);
    }

    /// <summary>Verifies a failed atomic family commit clears provisional common surface state.</summary>
    [Fact]
    public async Task Open_WhenFamilyCommitThrows_ClearsCommonStateAndPreservesFailureAsync()
    {
        using var probe = new FloatingSurfaceProbe();
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        var expected = new InvalidOperationException("The family open commit failed.");
        InvalidOperationException? exception = null;

        await surface.UpdateAsync(
            () => exception = Should.Throw<InvalidOperationException>(() =>
                probe.PublishBoundsAndThrow(new Rect(1, 2, 3, 4), expected)),
            "roll back failed floating surface opening");

        exception.ShouldBeSameAs(expected);
        probe.IsPresented.ShouldBeFalse();
        probe.SurfaceBounds.ShouldBe(default);
    }

    /// <summary>Verifies shared fade durations validate timer bounds and remain immutable for the
    /// complete presented lifetime.</summary>
    [Fact]
    public async Task FadeDurations_WhenInvalidOrPresented_RejectBeforeMutationAsync()
    {
        using var probe = new FloatingSurfaceProbe
        {
            FadeInDuration = TimeSpan.FromMilliseconds(40),
            FadeOutDuration = TimeSpan.FromMilliseconds(60)
        };

        _ = Should.Throw<ArgumentOutOfRangeException>(() => probe.FadeInDuration = TimeSpan.FromTicks(-1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            probe.FadeOutDuration = TimeSpan.FromMilliseconds((double) int.MaxValue + 1));
        probe.FadeInDuration.ShouldBe(TimeSpan.FromMilliseconds(40));
        probe.FadeOutDuration.ShouldBe(TimeSpan.FromMilliseconds(60));
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            new ManualTimeProvider(),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () =>
            {
                probe.PublishBounds(new Rect(1, 2, 3, 4));
                _ = Should.Throw<InvalidOperationException>(() => probe.FadeInDuration = TimeSpan.Zero);
                _ = Should.Throw<InvalidOperationException>(() => probe.FadeOutDuration = TimeSpan.Zero);
            },
            "reject presented fade mutation");

        probe.FadeInDuration.ShouldBe(TimeSpan.FromMilliseconds(40));
        probe.FadeOutDuration.ShouldBe(TimeSpan.FromMilliseconds(60));
    }

    /// <summary>Verifies positive entrance commits logical presentation at zero progress, raises
    /// Opened synchronously, and advances to stable visibility on the dispatcher clock.</summary>
    [Fact]
    public async Task Open_WhenFadeInIsPositive_AdvancesFromZeroAfterOpenedAsync()
    {
        var clock = new ManualTimeProvider();
        using var probe = new FloatingSurfaceProbe { FadeInDuration = TimeSpan.FromMilliseconds(100) };
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            clock,
            TestContext.Current.CancellationToken);
        var opened = 0;
        probe.Opened += (_, _) =>
        {
            opened++;
            probe.FadeProgress.ShouldBe(0);
            probe.IsPresented.ShouldBeTrue();
        };

        await surface.UpdateAsync(
            () => probe.PublishBounds(new Rect(1, 2, 3, 4)),
            "open fading surface");

        opened.ShouldBe(1);
        probe.FadeProgress.ShouldBe(0);

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "advance entrance halfway");
        probe.FadeProgress.ShouldBe(0.5, tolerance: 0.001);
        probe.IsPresented.ShouldBeTrue();

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "complete entrance");
        probe.FadeProgress.ShouldBe(1);
        probe.IsPresented.ShouldBeTrue();
    }

    /// <summary>Verifies accepted positive exit preserves presentation, bounds, modality, and
    /// family availability until progress reaches zero, then commits cleanup exactly once.</summary>
    [Fact]
    public async Task Close_WhenFadeOutIsPositive_DefersStructuralCleanupUntilInvisibleAsync()
    {
        var clock = new ManualTimeProvider();
        var order = new List<string>();
        using var child = new ProbeControl();
        using var probe = new FloatingSurfaceProbe
        {
            Content = child,
            FadeOutDuration = TimeSpan.FromMilliseconds(100)
        };
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            clock,
            TestContext.Current.CancellationToken);
        ModalScope? scope = null;
        probe.Closing += (_, _) => order.Add("closing");
        probe.Closed += (_, _) => order.Add("closed");

        await surface.UpdateAsync(
            () =>
            {
                probe.PublishBounds(new Rect(1, 2, 3, 4));
                scope = probe.EnterModalForTest(OutsideInteraction.Ignore);
                probe.CloseForTest(
                    () => order.Add("commit-closing"),
                    () =>
                    {
                        order.Add("commit-unavailable");
                        child.Visibility = Visibility.Collapsed;
                    }).ShouldBeTrue();
                probe.CloseForTest().ShouldBeFalse();
            },
            "begin deferred surface close");

        order.ShouldBe(["commit-closing", "closing"]);
        probe.IsPresented.ShouldBeTrue();
        probe.SurfaceBounds.ShouldBe(new Rect(1, 2, 3, 4));
        probe.FadeProgress.ShouldBe(1);
        child.Visibility.ShouldBe(Visibility.Visible);
        scope.ShouldNotBeNull().IsActive.ShouldBeTrue();

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "advance exit halfway");
        probe.FadeProgress.ShouldBe(0.5, tolerance: 0.001);
        probe.IsPresented.ShouldBeTrue();
        scope.IsActive.ShouldBeTrue();

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "complete exit");
        order.ShouldBe(["commit-closing", "closing", "commit-unavailable", "closed"]);
        probe.FadeProgress.ShouldBe(0);
        probe.IsPresented.ShouldBeFalse();
        probe.SurfaceBounds.ShouldBe(default);
        child.Visibility.ShouldBe(Visibility.Collapsed);
        scope.IsActive.ShouldBeFalse();
    }

    /// <summary>Verifies closure during entrance reverses from the currently committed progress
    /// instead of jumping to full opacity or restarting from zero.</summary>
    [Fact]
    public async Task Close_WhenEntranceIsActive_FadesFromCurrentProgressAsync()
    {
        var clock = new ManualTimeProvider();
        using var probe = new FloatingSurfaceProbe
        {
            FadeInDuration = TimeSpan.FromMilliseconds(200),
            FadeOutDuration = TimeSpan.FromMilliseconds(100)
        };
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            clock,
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => probe.PublishBounds(new Rect(1, 2, 3, 4)),
            "open reversing surface");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(100), "advance entrance halfway");
        probe.FadeProgress.ShouldBe(0.5, tolerance: 0.001);

        await surface.UpdateAsync(
            () => probe.CloseForTest().ShouldBeTrue(),
            "reverse entrance into exit");

        probe.FadeProgress.ShouldBe(0.5, tolerance: 0.001);
        probe.IsPresented.ShouldBeTrue();
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "advance reversed exit halfway");
        probe.FadeProgress.ShouldBe(0.25, tolerance: 0.001);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "complete reversed exit");
        probe.FadeProgress.ShouldBe(0);
        probe.IsPresented.ShouldBeFalse();
    }

    /// <summary>Verifies failures raised by deferred finalization use dispatcher reporting after
    /// every structural and lifecycle stage has still completed.</summary>
    [Fact]
    public async Task Close_WhenDeferredFinalizationFails_ReportsAsynchronouslyAfterCleanupAsync()
    {
        var clock = new ManualTimeProvider();
        using var probe = new FloatingSurfaceProbe { FadeOutDuration = TimeSpan.FromMilliseconds(100) };
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            clock,
            TestContext.Current.CancellationToken);
        var expected = new InvalidOperationException("Deferred cleanup failed.");
        var observed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = 0;
        surface.Application.UnhandledException += (_, eventArgs) =>
        {
            eventArgs.IsHandled = true;
            _ = observed.TrySetResult(eventArgs.Exception);
        };
        probe.Closed += (_, _) => closed++;
        await surface.UpdateAsync(
            () =>
            {
                probe.PublishBounds(new Rect(1, 2, 3, 4));
                probe.CloseForTest(
                    commitUnavailableState: () => throw expected).ShouldBeTrue();
            },
            "begin failing deferred close");

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(100), "finish failing deferred close");

        (await observed.Task.WaitAsync(TestContext.Current.CancellationToken)).ShouldBeSameAs(expected);
        probe.IsPresented.ShouldBeFalse();
        probe.SurfaceBounds.ShouldBe(default);
        probe.FadeProgress.ShouldBe(0);
        closed.ShouldBe(1);
    }

    /// <summary>Verifies Closed runs after transition guards release so its handler may begin a
    /// distinct presentation without a stale timer clearing the new state.</summary>
    [Fact]
    public async Task Close_WhenClosedHandlerReopens_PreservesNewPresentationAsync()
    {
        var clock = new ManualTimeProvider();
        using var probe = new FloatingSurfaceProbe { FadeOutDuration = TimeSpan.FromMilliseconds(100) };
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            clock,
            TestContext.Current.CancellationToken);
        var reopenedBounds = new Rect(7, 1, 4, 3);
        probe.Closed += (_, _) => probe.PublishBounds(reopenedBounds);
        await surface.UpdateAsync(
            () =>
            {
                probe.PublishBounds(new Rect(1, 2, 3, 4));
                probe.CloseForTest().ShouldBeTrue();
            },
            "begin close that reopens from Closed");

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(100), "finish close and reopen");
        await surface.AdvanceAsync(TimeSpan.FromSeconds(1), "prove stale ticks do not clear reopen");

        probe.IsPresented.ShouldBeTrue();
        probe.SurfaceBounds.ShouldBe(reopenedBounds);
        probe.FadeProgress.ShouldBe(1);
    }

    /// <summary>Verifies an already-captured tick carrying an old timer, clock plan, and
    /// presentation version cannot update a later presentation of the same surface instance.</summary>
    [Fact]
    public async Task FadeTick_WhenCapturedPresentationIsReplaced_CannotAffectNewPresentationAsync()
    {
        var clock = new ManualTimeProvider();
        using var probe = new FloatingSurfaceProbe { FadeInDuration = TimeSpan.FromMilliseconds(100) };
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            clock,
            TestContext.Current.CancellationToken);
        Action? staleTick = null;
        var previousVersion = 0L;

        await surface.UpdateAsync(
            () =>
            {
                probe.PublishBounds(new Rect(1, 1, 4, 2));
                previousVersion = probe.PresentationVersion;
                staleTick = probe.CaptureFadeTickForInvariant();
                var host = probe.Parent.ShouldBeOfType<Overlay>();
                host.Children.Remove(probe).ShouldBeTrue();
                probe.FadeInDuration = TimeSpan.FromMilliseconds(200);
                host.Children.Add(probe);
                probe.PublishBounds(new Rect(8, 1, 5, 2));
            },
            "replace one fading presentation before its captured tick runs");

        probe.PresentationVersion.ShouldBeGreaterThan(previousVersion);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(100), "advance only the replacement fade halfway");
        probe.FadeProgress.ShouldBe(0.5, tolerance: 0.001);

        await surface.UpdateAsync(staleTick!, "deliver stale tick from replaced presentation");

        probe.FadeProgress.ShouldBe(0.5, tolerance: 0.001);
        probe.SurfaceBounds.ShouldBe(new Rect(8, 1, 5, 2));
        probe.PresentationVersion.ShouldBeGreaterThan(previousVersion);
    }

    /// <summary>Verifies direct detachment aborts transition resources and performs immediate
    /// structural cleanup without inventing lifecycle notifications.</summary>
    [Fact]
    public async Task Detach_WhenFadeIsActive_CancelsTransitionWithoutClosedAsync()
    {
        var clock = new ManualTimeProvider();
        using var probe = new FloatingSurfaceProbe
        {
            FadeInDuration = TimeSpan.FromMilliseconds(100),
            FadeOutDuration = TimeSpan.FromMilliseconds(100)
        };
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            clock,
            TestContext.Current.CancellationToken);
        var closed = 0;
        probe.Closed += (_, _) => closed++;

        await surface.UpdateAsync(
            () =>
            {
                probe.PublishBounds(new Rect(1, 2, 3, 4));
                ((Overlay) probe.Parent!).Children.Remove(probe).ShouldBeTrue();
            },
            "detach entering surface");

        probe.FadeProgress.ShouldBe(0);
        probe.IsPresented.ShouldBeFalse();
        closed.ShouldBe(0);
        clock.Advance(TimeSpan.FromSeconds(1));
        probe.FadeProgress.ShouldBe(0);
        closed.ShouldBe(0);
    }

    /// <summary>Verifies disposal during either fade direction retires the owned timer and every
    /// deferred close plan before a later clock advance can publish lifecycle work.</summary>
    [Fact]
    public async Task Dispose_WhenFadeIsEnteringOrExiting_ClearsTransitionAndDeferredPlanAsync()
    {
        var clock = new ManualTimeProvider();
        var entering = new FloatingSurfaceProbe { FadeInDuration = TimeSpan.FromSeconds(10) };
        var exiting = new FloatingSurfaceProbe { FadeOutDuration = TimeSpan.FromSeconds(10) };
        var root = new Overlay { Children = { entering, exiting } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 6),
            clock,
            TestContext.Current.CancellationToken);
        var enteringClosed = 0;
        var exitingClosed = 0;
        entering.Closed += (_, _) => enteringClosed++;
        exiting.Closed += (_, _) => exitingClosed++;

        await surface.UpdateAsync(
            () =>
            {
                entering.PublishBounds(new Rect(1, 1, 4, 2));
                exiting.PublishBounds(new Rect(6, 1, 4, 2));
                exiting.CloseForTest().ShouldBeTrue();
            },
            "start entering and exiting fades before disposal");

        entering.HasActiveFadeTransition.ShouldBeTrue();
        entering.HasDeferredSurfaceClosePlan.ShouldBeFalse();
        exiting.HasActiveFadeTransition.ShouldBeTrue();
        exiting.HasDeferredSurfaceClosePlan.ShouldBeTrue();

        await surface.UpdateAsync(
            () =>
            {
                entering.Dispose();
                exiting.Dispose();
            },
            "dispose both active fade directions");

        entering.HasActiveFadeTransition.ShouldBeFalse();
        entering.HasDeferredSurfaceClosePlan.ShouldBeFalse();
        exiting.HasActiveFadeTransition.ShouldBeFalse();
        exiting.HasDeferredSurfaceClosePlan.ShouldBeFalse();
        entering.IsDisposed.ShouldBeTrue();
        exiting.IsDisposed.ShouldBeTrue();

        await surface.AdvanceAsync(TimeSpan.FromSeconds(20), "prove disposed fade work cannot resume");

        enteringClosed.ShouldBe(0);
        exitingClosed.ShouldBe(0);
        entering.FadeProgress.ShouldBe(0);
        exiting.FadeProgress.ShouldBe(0);
    }

    /// <summary>Verifies accepted exit immediately clears pointer state, consumes direct and
    /// application keyboard routes, retains existing focus, and blocks pointer click-through.</summary>
    [Fact]
    public async Task Input_WhenFadeOutIsActive_SuppressesTheCompleteSurfaceSubtreeAsync()
    {
        var background = new Button { Text = "Background" };
        var action = new Button { Text = "Action" };
        using var probe = new FloatingSurfaceProbe
        {
            Content = action,
            FadeOutDuration = TimeSpan.FromSeconds(10)
        };
        var root = new Overlay { Children = { background, probe } };
        var actionClicks = 0;
        var backgroundClicks = 0;
        var routedKeys = 0;
        var routedPastes = 0;
        var routedText = 0;
        action.Click += (_, _) => actionClicks++;
        background.Click += (_, _) => backgroundClicks++;
        _ = action.AddHandler(Events.Key, (_, _) => routedKeys++, handledEventsToo: true);
        _ = action.AddHandler(Events.Paste, (_, _) => routedPastes++, handledEventsToo: true);
        _ = action.AddHandler(Events.Text, (_, _) => routedText++, handledEventsToo: true);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 6),
            new ManualTimeProvider(),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () =>
            {
                probe.PublishBounds(probe.Bounds);
                action.Focus().ShouldBeTrue();
            },
            "open and focus interactive floating surface");
        await surface.Pointer.MoveToAsync(action);
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(action);
        action.IsPressed.ShouldBeTrue();
        RouteResult direct = default;
        RouteResult directPaste = default;
        RouteResult directText = default;

        await surface.UpdateAsync(
            () =>
            {
                probe.CloseForTest().ShouldBeTrue();
                direct = Router.Route(
                    action,
                    Events.Key,
                    new KeyEventArgs(new Stroke(
                        Code.Enter,
                        character: null,
                        nativeCode: 0,
                        Modifiers.None,
                        KeyAction.Press)));
                directText = Router.Route(
                    action,
                    Events.Text,
                    new TextEventArgs(new TerminalText(new Rune('x'))));
                directPaste = Router.Route(
                    action,
                    Events.Paste,
                    new PasteEventArgs(new Paste("y"u8)));
            },
            "begin exit and attempt direct route");

        direct.IsHandled.ShouldBeTrue();
        directText.IsHandled.ShouldBeTrue();
        directPaste.IsHandled.ShouldBeTrue();
        routedKeys.ShouldBe(0);
        routedText.ShouldBe(0);
        routedPastes.ShouldBe(0);
        surface.ShouldHaveCapture(null);
        surface.Application.Capture.Hovered.ShouldBeNull();
        action.IsPressed.ShouldBeFalse();
        surface.ShouldHaveFocus(action);
        var focusResolutionCount = surface.Application.Capture.FocusResolutionCount;

        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.TypeAsync("x");
        await surface.Keyboard.PasteAsync("y");
        await surface.Pointer.ReleaseAsync();
        await surface.Pointer.ClickAsync(action);

        actionClicks.ShouldBe(0);
        backgroundClicks.ShouldBe(0);
        routedKeys.ShouldBe(0);
        routedText.ShouldBe(0);
        routedPastes.ShouldBe(0);
        surface.ShouldHaveFocus(action);
        surface.Application.Capture.Hovered.ShouldBeNull();
        surface.Application.Capture.FocusResolutionCount.ShouldBe(focusResolutionCount);
    }

    /// <summary>Verifies closure publishes family state, lifecycle, modality, and availability in order.</summary>
    [Fact]
    public async Task Close_WhenSurfaceIsPresented_PublishesOrderedLifecycleAndClearsBoundsAsync()
    {
        var order = new List<string>();
        using var child = new ProbeControl();
        using var probe = new FloatingSurfaceProbe { Content = child };
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        ModalScope? scope = null;
        probe.Closing += (_, _) =>
        {
            order.Add("closing");
            probe.IsPresented.ShouldBeTrue();
            probe.SurfaceBounds.ShouldBe(new Rect(1, 2, 3, 4));
            child.Visibility.ShouldBe(Visibility.Visible);
            scope!.IsActive.ShouldBeTrue();
        };
        probe.Closed += (_, _) =>
        {
            order.Add("closed");
            probe.IsPresented.ShouldBeFalse();
            probe.SurfaceBounds.ShouldBe(default);
            child.Visibility.ShouldBe(Visibility.Collapsed);
            scope!.IsActive.ShouldBeFalse();
        };

        await surface.UpdateAsync(
            () =>
            {
                probe.PublishBounds(new Rect(1, 2, 3, 4));
                scope = probe.EnterModalForTest(OutsideInteraction.Ignore);
                _ = probe.CloseForTest(
                    () => order.Add("commit-closing"),
                    () =>
                    {
                        order.Add("commit-unavailable");
                        child.Visibility = Visibility.Collapsed;
                    });
            },
            "close floating surface");

        order.ShouldBe(["commit-closing", "closing", "commit-unavailable", "closed"]);
    }

    /// <summary>Verifies repeated closure after committed cleanup is harmless.</summary>
    [Fact]
    public async Task Close_WhenSurfaceIsAlreadyClosed_IsIdempotentAsync()
    {
        using var probe = new FloatingSurfaceProbe();
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        var closed = 0;
        probe.Closed += (_, _) => closed++;

        await surface.UpdateAsync(
            () =>
            {
                probe.PublishBounds(new Rect(1, 2, 3, 4));
                probe.CloseForTest().ShouldBeTrue();
                probe.CloseForTest().ShouldBeFalse();
            },
            "repeat floating surface closure");

        closed.ShouldBe(1);
    }

    /// <summary>Verifies Closed runs after the common transition guard releases, allowing every
    /// floating-surface family to begin a distinct presentation from that completion boundary.</summary>
    [Fact]
    public async Task Close_WhenClosedObserverOpensAgain_StartsDistinctPresentationAsync()
    {
        using var probe = new FloatingSurfaceProbe();
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        probe.Closed += (_, _) => probe.PublishBounds(new Rect(5, 1, 6, 2));

        await surface.UpdateAsync(
            () =>
            {
                probe.PublishBounds(new Rect(1, 2, 3, 4));
                probe.CloseForTest().ShouldBeTrue();
            },
            "reopen floating surface from Closed");

        probe.IsPresented.ShouldBeTrue();
        probe.SurfaceBounds.ShouldBe(new Rect(5, 1, 6, 2));
    }

    /// <summary>Verifies a post-Closing family commit can retain the current presentation without
    /// common cleanup or a Closed notification.</summary>
    [Fact]
    public async Task CloseAfterClosing_WhenFamilyRetainsPresentation_LeavesSurfaceOpenAsync()
    {
        var order = new List<string>();
        using var probe = new FloatingSurfaceProbe();
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        var closed = 0;
        probe.Closing += (_, _) => order.Add("closing");
        probe.Closed += (_, _) => closed++;
        var result = true;

        await surface.UpdateAsync(
            () =>
            {
                probe.PublishBounds(new Rect(1, 2, 3, 4));
                result = probe.CloseAfterClosingForTest(
                    () => order.Add("prepare"),
                    () =>
                    {
                        order.Add("commit");
                        return false;
                    });
            },
            "retain a floating surface from the post-Closing family commit");

        result.ShouldBeFalse();
        order.ShouldBe(["prepare", "closing", "commit"]);
        probe.IsPresented.ShouldBeTrue();
        probe.SurfaceBounds.ShouldBe(new Rect(1, 2, 3, 4));
        closed.ShouldBe(0);
    }

    /// <summary>Verifies a closing callback cannot recursively enter the same transition.</summary>
    [Fact]
    public async Task Close_WhenClosingCallbackReenters_RejectsReentryAfterCompletingCleanupAsync()
    {
        using var probe = new FloatingSurfaceProbe();
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        InvalidOperationException? exception = null;
        probe.Closing += (_, _) => probe.CloseForTest();

        await surface.UpdateAsync(
            () =>
            {
                probe.PublishBounds(new Rect(1, 2, 3, 4));
                exception = Should.Throw<InvalidOperationException>(() => probe.CloseForTest());
            },
            "reject reentrant floating surface closure");

        exception.ShouldNotBeNull().Message.ShouldContain("reentered");
        probe.IsPresented.ShouldBeFalse();
        probe.SurfaceBounds.ShouldBe(default);
    }

    /// <summary>Verifies every close stage runs and the first callback failure remains authoritative.</summary>
    [Fact]
    public async Task Close_WhenCallbacksThrow_CompletesCleanupAndRethrowsEarliestFailureAsync()
    {
        using var probe = new FloatingSurfaceProbe();
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        var expected = new InvalidOperationException("The closing-state commit failed.");
        var order = new List<string>();
        InvalidOperationException? exception = null;
        probe.Closing += (_, _) =>
        {
            order.Add("closing");
            throw new NotSupportedException("The Closing subscriber failed.");
        };
        probe.Closed += (_, _) =>
        {
            order.Add("closed");
            throw new ArgumentException("The Closed subscriber failed.");
        };

        await surface.UpdateAsync(
            () =>
            {
                probe.PublishBounds(new Rect(1, 2, 3, 4));
                exception = Should.Throw<InvalidOperationException>(() => probe.CloseForTest(
                    () =>
                    {
                        order.Add("commit-closing");
                        throw expected;
                    },
                    () =>
                    {
                        order.Add("commit-unavailable");
                        throw new InvalidDataException("The unavailable-state commit failed.");
                    }));
            },
            "aggregate floating surface close failures");

        exception.ShouldBeSameAs(expected);
        order.ShouldBe(["commit-closing", "closing", "commit-unavailable", "closed"]);
        probe.IsPresented.ShouldBeFalse();
        probe.SurfaceBounds.ShouldBe(default);
    }

    /// <summary>Verifies modal entry validates policy and attachment before observable mutation.</summary>
    [Fact]
    public void EnterSurfaceModal_WhenArgumentsOrOwnerAreInvalid_ThrowsBeforeMutation()
    {
        using var surface = new FloatingSurfaceProbe();

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            surface.EnterModalForTest((OutsideInteraction) int.MaxValue));
        _ = Should.Throw<InvalidOperationException>(() =>
            surface.EnterModalForTest(OutsideInteraction.Ignore));

        surface.IsPresented.ShouldBeFalse();
        surface.SurfaceBounds.ShouldBe(default);
    }

    /// <summary>Verifies a mounted but closed surface cannot enter modality.</summary>
    [Fact]
    public async Task EnterSurfaceModal_WhenSurfaceIsNotPresented_RejectsBeforeModalEntryAsync()
    {
        using var probe = new FloatingSurfaceProbe();
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        InvalidOperationException? exception = null;

        await surface.UpdateAsync(
            () => exception = Should.Throw<InvalidOperationException>(() =>
                probe.EnterModalForTest(OutsideInteraction.Ignore)),
            "reject closed floating surface modality");

        exception.ShouldNotBeNull().Message.ShouldContain("presented");
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies closure from modal-entry focus callbacks cannot leave an active scope.</summary>
    [Fact]
    public async Task EnterSurfaceModal_WhenFocusCallbackClosesSurface_ReturnsInactiveScopeAsync()
    {
        using var focusTarget = new ProbeControl { IsFocusable = true };
        using var probe = new FloatingSurfaceProbe { Content = focusTarget };
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        ModalScope? scope = null;
        surface.Application.Focus.Gained += (_, eventArgs) =>
        {
            if (ReferenceEquals(eventArgs.Current, focusTarget))
            {
                probe.CloseForTest().ShouldBeTrue();
            }
        };

        await surface.UpdateAsync(
            () =>
            {
                probe.PublishBounds(new Rect(1, 2, 3, 4));
                scope = probe.EnterModalForTest(OutsideInteraction.Ignore, focusTarget);
            },
            "close floating surface during modal entry");

        scope.ShouldNotBeNull().IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        probe.IsPresented.ShouldBeFalse();
        probe.SurfaceBounds.ShouldBe(default);
    }

    /// <summary>Verifies detachment from modal-entry callbacks leaves no surface or modal lifetime.</summary>
    [Fact]
    public async Task EnterSurfaceModal_WhenFocusCallbackDetachesSurface_ReturnsInactiveScopeAsync()
    {
        using var focusTarget = new ProbeControl { IsFocusable = true };
        using var probe = new FloatingSurfaceProbe { Content = focusTarget };
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        var host = (Overlay) probe.Parent!;
        ModalScope? scope = null;
        surface.Application.Focus.Gained += (_, eventArgs) =>
        {
            if (ReferenceEquals(eventArgs.Current, focusTarget))
            {
                host.Children.Remove(probe).ShouldBeTrue();
            }
        };

        await surface.UpdateAsync(
            () =>
            {
                probe.PublishBounds(new Rect(1, 2, 3, 4));
                scope = probe.EnterModalForTest(OutsideInteraction.Ignore, focusTarget);
            },
            "detach floating surface during modal entry");

        scope.ShouldNotBeNull().IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        probe.Dispatcher.ShouldBeNull();
        probe.IsPresented.ShouldBeFalse();
        probe.SurfaceBounds.ShouldBe(default);
    }

    /// <summary>Verifies one surface cannot retain two active modal lifetimes.</summary>
    [Fact]
    public async Task EnterSurfaceModal_WhenAlreadyModal_RejectsDuplicateEntryAsync()
    {
        using var probe = new FloatingSurfaceProbe();
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        ModalScope? first = null;
        InvalidOperationException? exception = null;

        await surface.UpdateAsync(
            () =>
            {
                probe.PublishBounds(new Rect(1, 2, 3, 4));
                first = probe.EnterModalForTest(OutsideInteraction.Ignore);
                exception = Should.Throw<InvalidOperationException>(() =>
                    probe.EnterModalForTest(OutsideInteraction.Dismiss));
                probe.ExitModalForTest();
            },
            "verify duplicate floating surface modality");

        exception.ShouldNotBeNull().Message.ShouldContain("already modal");
        first.ShouldNotBeNull().IsActive.ShouldBeFalse();
    }

    /// <summary>Verifies removing an attached surface unwinds its owned modal lifetime.</summary>
    [Fact]
    public async Task Detach_WhenSurfaceOwnsModalScope_UnwindsScopeBeforeContextIsClearedAsync()
    {
        using var probe = new FloatingSurfaceProbe();
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        ModalScope? scope = null;

        await surface.UpdateAsync(
            () =>
            {
                probe.PublishBounds(new Rect(1, 2, 3, 4));
                scope = probe.EnterModalForTest(OutsideInteraction.Ignore);
                ((Overlay) probe.Parent!).Children.Remove(probe).ShouldBeTrue();
            },
            "detach modal floating surface");

        scope.ShouldNotBeNull().IsActive.ShouldBeFalse();
        probe.Dispatcher.ShouldBeNull();
        probe.IsPresented.ShouldBeFalse();
        probe.SurfaceBounds.ShouldBe(default);
    }

    /// <summary>Verifies forced detachment clears common state without publishing close events and permits reuse.</summary>
    [Fact]
    public async Task Detach_WhenSurfaceIsReparented_ClearsStateAndAllowsReopenWithoutCloseEventsAsync()
    {
        using var probe = new FloatingSurfaceProbe();
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        var host = (Overlay) probe.Parent!;
        var closing = 0;
        var closed = 0;
        probe.Closing += (_, _) => closing++;
        probe.Closed += (_, _) => closed++;

        await surface.UpdateAsync(
            () =>
            {
                probe.PublishBounds(new Rect(1, 2, 3, 4));
                var presentationVersion = probe.PresentationVersion;
                host.Children.Remove(probe).ShouldBeTrue();
                probe.IsPresented.ShouldBeFalse();
                probe.SurfaceBounds.ShouldBe(default);
                probe.PresentationVersion.ShouldBe(presentationVersion + 1);
                host.Children.Add(probe);
                probe.PublishBounds(new Rect(5, 1, 6, 2));
            },
            "reparent and reopen floating surface");

        probe.IsPresented.ShouldBeTrue();
        probe.SurfaceBounds.ShouldBe(new Rect(5, 1, 6, 2));
        closing.ShouldBe(0);
        closed.ShouldBe(0);
    }

    /// <summary>Verifies disabling ends modality without discarding the still-visible presentation.</summary>
    [Fact]
    public async Task IsEnabled_WhenModalSurfaceIsDisabled_PreservesPresentationAndAllowsModalReentryAsync()
    {
        using var probe = new FloatingSurfaceProbe();
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        ModalScope? first = null;
        ModalScope? second = null;

        await surface.UpdateAsync(
            () =>
            {
                probe.PublishBounds(new Rect(1, 2, 3, 4));
                first = probe.EnterModalForTest(OutsideInteraction.Ignore);
                probe.IsEnabled = false;

                first.IsActive.ShouldBeFalse();
                surface.Application.Modality.Active.ShouldBeNull();
                probe.IsPresented.ShouldBeTrue();
                probe.SurfaceBounds.ShouldBe(new Rect(1, 2, 3, 4));

                probe.IsEnabled = true;
                second = probe.EnterModalForTest(OutsideInteraction.Dismiss);
                second.IsActive.ShouldBeTrue();
                surface.Application.Modality.Active.ShouldBeSameAs(second);
                probe.ExitModalForTest();
            },
            "disable and restore modal floating surface");

        first.ShouldNotBeNull().IsActive.ShouldBeFalse();
        second.ShouldNotBeNull().IsActive.ShouldBeFalse();
        probe.IsPresented.ShouldBeTrue();
        probe.SurfaceBounds.ShouldBe(new Rect(1, 2, 3, 4));
    }

    /// <summary>Verifies disposal unwinds modality and still commits when an exit callback fails.</summary>
    [Fact]
    public async Task Dispose_WhenModalExitCallbackThrows_UnwindsScopeAndPreservesFailureAsync()
    {
        var probe = new FloatingSurfaceProbe();
        await using var surface = await ComponentSurface.MountAsync(
            probe,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        var expected = new InvalidOperationException("The modal exit callback failed.");
        ModalScope? scope = null;
        InvalidOperationException? exception = null;

        await surface.UpdateAsync(
            () =>
            {
                probe.PublishBounds(new Rect(1, 2, 3, 4));
                scope = probe.EnterModalForTest(OutsideInteraction.Ignore);
                scope.Exited += (_, _) => throw expected;
                exception = Should.Throw<InvalidOperationException>(probe.Dispose);
            },
            "dispose modal floating surface");

        exception.ShouldBeSameAs(expected);
        scope.ShouldNotBeNull().IsActive.ShouldBeFalse();
        probe.IsDisposed.ShouldBeTrue();
        probe.IsPresented.ShouldBeFalse();
        probe.SurfaceBounds.ShouldBe(default);
    }
}
