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
                host.Children.Remove(probe).ShouldBeTrue();
                probe.IsPresented.ShouldBeFalse();
                probe.SurfaceBounds.ShouldBe(default);
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
