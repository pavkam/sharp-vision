// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Verifies screen ownership and startup hooks.</summary>
public sealed class ScreenTests
{
    /// <summary>Verifies the authored root is owned directly by Screen without a reachable framework container.</summary>
    [Fact]
    public void Constructor_WhenCompositionIsInitialized_ParentsAuthoredRootDirectlyToScreen()
    {
        using var screen = new ProbeScreen();

        screen.ContentRoot.Parent.ShouldBeSameAs(screen);
        screen.ContentRoot.Parent.ShouldNotBeAssignableTo<Container>();
    }

    /// <summary>Verifies an application root leaves local appearance unset for type-theme resolution.</summary>
    [Fact]
    public void Constructor_WhenCreated_LeavesAppearancePropertiesForThemeResolution()
    {
        using var screen = new ProbeScreen();

        screen.Face.Background.ShouldBe(SemanticColor.Control);
        screen.ActualBorder.Foreground.ShouldBe(Color.Default);
    }

    /// <summary>Verifies retained composition precedes attach, first layout, and started hooks.</summary>
    [Fact]
    public async Task Attach_WhenApplicationStarts_RunsRetainedLifecycleInOrderAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        using ProbeScreen screen = new();
        await using Application application = new(
            screen,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        screen.Order.ShouldBe(["compose"]);
        screen.Attach(application);

        await application.StartAsync(TestContext.Current.CancellationToken);

        screen.Order.ShouldBe(["compose", "attach", "measure", "started"]);
        screen.ContentRoot.MeasureConstraints.ShouldNotBeEmpty();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies temporary application surfaces use Screen's private presentation slot.</summary>
    [Fact]
    public async Task ShowAsync_WhenOwnerBelongsToScreen_ParentsTemporarySurfaceDirectlyToScreenAsync()
    {
        using var screen = new ProbeScreen();
        await using var surface = await ComponentSurface.MountScreenAsync(
            screen,
            new Size(40, 12),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;

        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(screen.ContentRoot, "Saved successfully.", "Status"),
            "show Screen-owned MessageBox");
        var messageBox = OwnedTree.Find<MessageBox>(screen).ShouldNotBeNull();

        _ = messageBox.Parent.ShouldBeAssignableTo<Overlay>();
        messageBox.Parent!.Parent.ShouldBeSameAs(screen);
        screen.GetType().GetProperty(nameof(Container.Children)).ShouldBeNull();

        await surface.Keyboard.PressAsync(Code.Escape);
        (await pending!).ShouldBe(MessageBoxResult.Cancel);
    }

    /// <summary>Verifies a floating Window is mounted on Screen's clipped private presentation Overlay.</summary>
    [Fact]
    public async Task AddPresentation_WhenWindowIsMounted_ParentsItDirectlyToPrivateOverlayAsync()
    {
        using var screen = new ProbeScreen();
        var window = new Window
        {
            Width = Length.Cells(10),
            Height = Length.Cells(4)
        };
        Overlay.SetLeft(window, Length.Cells(2));
        Overlay.SetTop(window, Length.Cells(1));
        await using var surface = await ComponentSurface.MountScreenAsync(
            screen,
            new Size(40, 12),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => screen.AddPresentation(window), "present Window");

        var presentation = window.Parent as Overlay;
        _ = presentation.ShouldNotBeNull();
        presentation.Parent.ShouldBeSameAs(screen);
        presentation.ClipToBounds.ShouldBeTrue();
        window.SurfaceBounds.ShouldBe(window.Bounds);

        await surface.Pointer.MoveToAsync(window, new Point(2, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(presentation, new Point(8, 4));
        await surface.Pointer.ReleaseAsync();

        Overlay.GetLeft(window).ShouldBe(Length.Cells(6));
        Overlay.GetTop(window).ShouldBe(Length.Cells(4));
    }

    /// <summary>Verifies an empty private presentation plane passes pointer input to authored content.</summary>
    [Fact]
    public async Task Pointer_WhenPresentationPlaneIsEmpty_ReachesAuthoredContentAsync()
    {
        using var screen = new ProbeScreen();
        screen.ContentRoot.HorizontalAlignment = HorizontalAlignment.Stretch;
        screen.ContentRoot.VerticalAlignment = VerticalAlignment.Stretch;
        var presses = 0;
        screen.ContentRoot.PointerPressed += (_, _) => presses++;
        await using var surface = await ComponentSurface.MountScreenAsync(
            screen,
            new Size(40, 12),
            TestContext.Current.CancellationToken);

        await surface.Pointer.ClickAsync(screen.ContentRoot, new Point(2, 2));

        presses.ShouldBe(1);
    }

    /// <summary>Verifies authored Screen content and its private presentation plane share the committed bounds.</summary>
    [Fact]
    public void Layout_WhenPresentationExists_ArrangesAuthoredContentBeforePresentationPlane()
    {
        using var screen = new ProbeScreen();
        var window = new Window { Width = Length.Cells(10), Height = Length.Cells(4) };
        screen.ContentRoot.HorizontalAlignment = HorizontalAlignment.Stretch;
        screen.AddPresentation(window);

        new LayoutEngine().Layout(screen, new Size(40, 12));

        screen.ContentRoot.Bounds.ShouldBe(new Rect(0, 0, 40, 12));
        var presentation = window.Parent as Overlay;
        _ = presentation.ShouldNotBeNull();
        presentation.Bounds.ShouldBe(new Rect(0, 0, 40, 12));
        window.Bounds.ShouldBe(new Rect(0, 0, 10, 4));
    }

    /// <summary>Verifies only Screen and explicit Overlay ownership can host floating presentation.</summary>
    [Fact]
    public void Resolve_WhenOwnerUsesUnrelatedLayoutContainer_RejectsItAsPresentationHost()
    {
        var owner = new ProbeControl();
        var stack = new Stack { Children = { owner } };
        var overlay = new Overlay();

        PresentationHost.Resolve(owner).ShouldBeNull();
        PresentationHost.Resolve(stack).ShouldBeNull();
        _ = PresentationHost.Resolve(overlay).ShouldNotBeNull();
    }

    /// <summary>Verifies attach rejects an uninitialized composition before binding the application.</summary>
    [Fact]
    public async Task Attach_WhenCompositionIsMissing_RejectsBeforeApplicationMutationAsync()
    {
        await using FakeTerminal terminal = new();
        using ProbeScreen screen = new(initializeContent: false);
        await using Application application = new(
            screen,
            terminal,
            terminal,
            TerminalOptions.Minimal);

        var exception = Should.Throw<InvalidOperationException>(() => screen.Attach(application));

        exception.Message.ShouldContain("composition");
        screen.BoundApplication.ShouldBeNull();
        screen.Order.ShouldBeEmpty();
    }

    /// <summary>Verifies a failed attach hook clears the binding and never subscribes the started hook.</summary>
    [Fact]
    public async Task Attach_WhenHookThrows_RollsBackBindingAndStartedSubscriptionAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        using ProbeScreen screen = new(throwOnAttach: true);
        await using Application application = new(
            screen,
            terminal,
            terminal,
            TerminalOptions.Minimal);

        var exception = Should.Throw<InvalidOperationException>(() => screen.Attach(application));
        await application.StartAsync(TestContext.Current.CancellationToken);

        exception.Message.ShouldBe("The screen attach hook failed.");
        screen.BoundApplication.ShouldBeNull();
        screen.Order.ShouldBe(["compose", "attach", "measure"]);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a failed disposal hook cannot retain the application or skip owned cleanup.</summary>
    [Fact]
    public async Task Dispose_WhenHooksThrow_CompletesCleanupAndRethrowsEarliestFailureAsync()
    {
        await using FakeTerminal terminal = new();
        using ProbeScreen screen = new(throwOnDispose: true, throwOnContentDisposing: true);
        await using Application application = new(
            screen,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        screen.Attach(application);

        var exception = Should.Throw<InvalidOperationException>(screen.Dispose);

        exception.Message.ShouldBe("The screen dispose hook failed.");
        screen.BoundApplication.ShouldBeNull();
        screen.ContentRoot.IsDisposed.ShouldBeTrue();
        screen.IsDisposed.ShouldBeTrue();
        screen.Order.ShouldBe(["compose", "attach", "dispose"]);
    }
}
