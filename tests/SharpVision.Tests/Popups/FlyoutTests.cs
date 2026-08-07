// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Popups;

using ReflectionBindingFlags = System.Reflection.BindingFlags;

/// <summary>Verifies flyout visibility, light dismiss, and anchor placement.</summary>
public sealed class FlyoutTests
{
    /// <summary>Verifies a newly constructed flyout has expected defaults for all properties.</summary>
    [ComponentUnitEvidence(typeof(Flyout))]
    [Fact]
    public void Constructor_WhenCreated_HasExpectedDefaults()
    {
        using var flyout = new Flyout();
        flyout.Anchor.ShouldBeNull();
        flyout.Placement.ShouldBe(PopupPlacement.Below);
        flyout.IsOpen.ShouldBeFalse();
        flyout.CloseOnEscape.ShouldBeTrue();
        flyout.FocusOnOpen.ShouldBeTrue();
        flyout.Content.ShouldBeNull();
    }

    /// <summary>Verifies Flyout is the Popup surface instead of owning a private Popup proxy.</summary>
    [Fact]
    public void Constructor_WhenCreated_IsDirectPopupSurface()
    {
        using var flyout = new Flyout();

        flyout.GetType().BaseType.ShouldBe(typeof(Popup));
        OwnedTree.FindAll<Popup>(flyout).Single().ShouldBeSameAs(flyout);
        flyout.GetType()
            .GetFields(
                ReflectionBindingFlags.Instance |
                ReflectionBindingFlags.NonPublic |
                ReflectionBindingFlags.DeclaredOnly)
            .ShouldNotContain(field => field.FieldType == typeof(Popup));
    }

    /// <summary>Verifies the mounted Flyout itself publishes elevated Popup geometry.</summary>
    [Fact]
    public async Task IsOpen_WhenMounted_PublishesBoundsOnFlyoutSurfaceAsync()
    {
        var anchor = new Button
        {
            Text = "Anchor",
            Width = Length.Cells(8),
            Height = Length.Cells(1)
        };
        var flyout = new Flyout
        {
            Anchor = anchor,
            Content = new ControlText("Action")
        };
        var root = new Overlay { Children = { anchor, flyout } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(18, 7),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => flyout.IsOpen = true, "open direct Flyout surface");

        flyout.SurfaceBounds.Width.ShouldBeGreaterThan(0);
        flyout.SurfaceBounds.Y.ShouldBeGreaterThanOrEqualTo(anchor.Bounds.Bottom);
        flyout.IntrinsicLayer.ShouldBe(OwnedControlLayer.Popup);
        OwnedTree.FindAll<Popup>(flyout).Single().ShouldBeSameAs(flyout);
    }

    /// <summary>Verifies a Button hosted as flyout content shows its own hovered appearance -
    /// both PointerOver and the rendered face/border - once the pointer moves over it while the
    /// flyout is open, matching an ordinary page-hosted Button.</summary>
    [Fact]
    public async Task Pointer_WhenMovedOverFlyoutHostedButton_ShowsHoveredAppearanceAsync()
    {
        // Arrange
        var anchor = new Button
        {
            Text = "Anchor",
            Width = Length.Cells(8),
            Height = Length.Cells(3)
        };
        var action = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Text = "Save"
        };
        var flyout = new Flyout { Anchor = anchor, Content = action, FocusOnOpen = false };
        var root = new Overlay { Children = { anchor, flyout } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(18, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => flyout.IsOpen = true, "open Flyout");

        // Act
        await surface.Pointer.MoveToAsync(action);

        // Assert
        action.PointerOver.ShouldBeTrue();
        surface.ShouldHaveState(action, VisualState.PointerOver);
        var borderColor = TerminalPalette.Project(ThemeColorHelper.HoveredBorder(ThemeCatalog.Dark), ColorDepth.Basic16);
        var hoveredForeground = TerminalPalette.Project(ThemeColorHelper.HoveredForeground(ThemeCatalog.Dark), ColorDepth.Basic16);
        var origin = new Point(action.Bounds.X, action.Bounds.Y);
        surface.Cell(origin).Style.Foreground.ShouldBe(borderColor);
        var content = new Point(action.Bounds.X + 2, action.Bounds.Y + 1);
        surface.Cell(content).Style.Foreground.ShouldBe(hoveredForeground);
    }

    /// <summary>Verifies opening a flyout does not raise Closing or Closed events.</summary>
    [Fact]
    public void IsOpen_WhenSetTrue_RaisesNoEventsBeforeClose()
    {
        using var flyout = new Flyout { Content = new ProbeControl(new Size(4, 2)) };
        var closingRaised = false;
        var closedRaised = false;
        flyout.Closing += (_, _) => closingRaised = true;
        flyout.Closed += (_, _) => closedRaised = true;

        flyout.IsOpen = true;

        closingRaised.ShouldBeFalse();
        closedRaised.ShouldBeFalse();
    }

    /// <summary>Verifies closing a flyout raises Closing then Closed in sequence.</summary>
    [Fact]
    public void IsOpen_WhenSetFalse_RaisesClosingThenClosed()
    {
        using var flyout = new Flyout { Content = new ProbeControl(new Size(4, 2)) };
        var events = new List<string>();
        flyout.Closing += (_, _) => events.Add("Closing");
        flyout.Closed += (_, _) => events.Add("Closed");

        flyout.IsOpen = true;
        flyout.IsOpen = false;

        events.ShouldBe(["Closing", "Closed"]);
    }

    /// <summary>Verifies ShowAt sets the anchor and opens the flyout.</summary>
    [Fact]
    public void ShowAt_WhenCalled_SetsAnchorAndOpens()
    {
        using var anchor = new ProbeControl(new Size(6, 1));
        using var flyout = new Flyout { Content = new ProbeControl(new Size(4, 2)) };

        flyout.ShowAt(anchor);

        flyout.Anchor.ShouldBeSameAs(anchor);
        flyout.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies opening one flyout closes an already open sibling flyout.</summary>
    [Fact]
    public async Task IsOpen_WhenSiblingFlyoutOpens_ClosesPreviousFlyoutAsync()
    {
        var firstAnchor = new Button { Text = "First" };
        var secondAnchor = new Button { Text = "Second" };
        var first = new Flyout { Anchor = firstAnchor, Content = new ControlText("One") };
        var second = new Flyout { Anchor = secondAnchor, Content = new ControlText("Two") };
        var root = new Overlay { Children = { firstAnchor, secondAnchor, first, second } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 7),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => first.IsOpen = true, "open first Flyout");
        await surface.UpdateAsync(() => second.IsOpen = true, "open second Flyout");

        first.IsOpen.ShouldBeFalse();
        second.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies setting Content retains it directly on the flyout surface.</summary>
    [Fact]
    public void Content_WhenSet_IsRetainedByFlyout()
    {
        using var content = new ProbeControl(new Size(4, 2));
        using var flyout = new Flyout { Content = content };

        flyout.Content.ShouldBeSameAs(content);
    }

    /// <summary>Verifies setting an invalid placement value throws ArgumentOutOfRangeException.</summary>
    [Fact]
    public void Placement_WhenInvalid_Throws()
    {
        using var flyout = new Flyout();
        _ = Should.Throw<ArgumentOutOfRangeException>(() => flyout.Placement = (PopupPlacement) 99);
    }

    /// <summary>Verifies closing an already closed flyout is a no-op without raising events.</summary>
    [Fact]
    public void IsOpen_WhenClosedWhileClosed_IsNoOp()
    {
        using var flyout = new Flyout { Content = new ProbeControl(new Size(4, 2)) };
        var closingRaised = false;
        flyout.Closing += (_, _) => closingRaised = true;

        flyout.IsOpen = false;

        closingRaised.ShouldBeFalse();
    }

    /// <summary>Verifies pressing outside the flyout surface and anchor triggers light dismiss.</summary>
    [Fact]
    public async Task OutsidePress_WhenOpen_ClosesFlyoutAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            var anchor = new ProbeControl(new Size(6, 1));
            var content = new ProbeControl(new Size(4, 2));
            var flyout = new Flyout { Anchor = anchor, Content = content };
            var background = new ProbeControl(new Size(20, 10));
            var root = new Overlay();
            root.Children.Add(background);
            root.Children.Add(anchor);
            root.Children.Add(flyout);
            root.Attach(dispatcher);

            flyout.IsOpen = true;
            flyout.IsOpen.ShouldBeTrue();

            // Simulate a press on the background (outside flyout surface)
            var outsidePoint = new Point(19, 9);
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

            flyout.IsOpen.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies pressing Escape closes an open flyout when CloseOnEscape is true.</summary>
    [Fact]
    public async Task Escape_WhenOpen_ClosesFlyoutAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            var anchor = new ProbeControl(new Size(6, 1));
            var content = new ProbeControl(new Size(4, 2));
            var flyout = new Flyout { Anchor = anchor, Content = content };
            var root = new Overlay();
            root.Children.Add(anchor);
            root.Children.Add(flyout);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            flyout.IsOpen = true;
            flyout.IsOpen.ShouldBeTrue();

            var escape = new KeyEventArgs(new Stroke(Code.Escape, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(content, Events.Key, escape);

            flyout.IsOpen.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies FocusOnOpen transfers focus to the flyout content when opened.</summary>
    [Fact]
    public async Task FocusOnOpen_WhenTrue_TransfersFocusToContentAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            var anchor = new ProbeControl(new Size(6, 1));
            var list = new UiListView { Items = ["first", "second"] };
            var flyout = new Flyout { Anchor = anchor, Content = list };
            var root = new Overlay();
            root.Children.Add(anchor);
            root.Children.Add(flyout);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            flyout.IsOpen = true;

            focus.Focused.ShouldBeSameAs(list);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an open flyout dismisses rather than re-positions once its anchor
    /// reflows (a preceding sibling growing above it), matching light dismiss's assumption that
    /// its captured pointer geometry stays valid only for a stationary anchor.</summary>
    [Fact]
    public async Task IsOpen_WhenAnchorReflowsWhileOpen_DismissesAsync()
    {
        var spacer = new Button
        {
            Text = string.Empty,
            Width = Length.Cells(1),
            Height = Length.Cells(4)
        };
        var anchor = new Button
        {
            Text = "Anchor",
            Width = Length.Cells(10),
            Height = Length.Cells(1)
        };
        var stack = new Stack
        {
            Orientation = Orientation.Vertical,
            Width = Length.Cells(12),
            Height = Length.Cells(8),
            Children = { spacer, anchor }
        };
        var flyout = new Flyout { Anchor = anchor, Content = new ControlText("Action") };
        var root = new Overlay { Children = { stack, flyout } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 8),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => flyout.IsOpen = true, "open flyout anchored inside the reflowing stack");
        flyout.IsOpen.ShouldBeTrue();

        await surface.UpdateAsync(() => spacer.Height = Length.Cells(7), "reflow the anchor while the flyout is open");

        flyout.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies clearing Anchor while a flyout is open drops its reflow subscription
    /// instead of leaking it - the former anchor reflowing afterward must not spuriously dismiss
    /// a flyout that no longer considers that control its anchor.</summary>
    [Fact]
    public async Task IsOpen_WhenAnchorClearedThenPreviousAnchorReflows_StaysOpenAsync()
    {
        var spacer = new Button
        {
            Text = string.Empty,
            Width = Length.Cells(1),
            Height = Length.Cells(4)
        };
        var anchor = new Button
        {
            Text = "Anchor",
            Width = Length.Cells(10),
            Height = Length.Cells(1)
        };
        var stack = new Stack
        {
            Orientation = Orientation.Vertical,
            Width = Length.Cells(12),
            Height = Length.Cells(8),
            Children = { spacer, anchor }
        };
        var flyout = new Flyout { Anchor = anchor, Content = new ControlText("Action") };
        var root = new Overlay { Children = { stack, flyout } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 8),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => flyout.IsOpen = true, "open flyout anchored inside the reflowing stack");
        await surface.UpdateAsync(() => flyout.Anchor = null, "clear the anchor while still open");

        await surface.UpdateAsync(() => spacer.Height = Length.Cells(7), "reflow the former anchor while open");

        flyout.IsOpen.ShouldBeTrue();
    }
}
