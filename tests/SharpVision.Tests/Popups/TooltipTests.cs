// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Popups;

using SharpVision.Tests.Input;

using ReflectionBindingFlags = System.Reflection.BindingFlags;

/// <summary>Verifies tooltip attachment, triggering, and content display.</summary>
public sealed class TooltipTests
{
    /// <summary>Verifies a new tooltip has expected defaults for all properties.</summary>
    [ComponentUnitEvidence(typeof(Tooltip))]
    [Fact]
    public void Constructor_WhenCreated_HasExpectedDefaults()
    {
        using var tooltip = new Tooltip();
        tooltip.Content.ShouldBeNull();
        tooltip.Text.ShouldBeNull();
        tooltip.Placement.ShouldBe(PopupPlacement.Below);
        tooltip.ShowDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
        tooltip.HideDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
        tooltip.IsOpen.ShouldBeFalse();
        tooltip.FocusOnOpen.ShouldBeFalse();
        tooltip.ModalBehavior.ShouldBe(PopupModalBehavior.None);
        tooltip.SuppressCloseOtherPopups.ShouldBeTrue();
        tooltip.CloseOnEscape.ShouldBeFalse();
        tooltip.HitTestVisible.ShouldBeFalse();
        tooltip.Focusable.ShouldBeFalse();
    }

    /// <summary>Verifies a Tooltip resolves the dedicated, borderless Tooltip role rather than
    /// inheriting Popup's framed appearance, so a passive hint is visually distinct from an
    /// interactive drop-down or menu.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesBorderlessTooltipStyle()
    {
        using var tooltip = new Tooltip();

        tooltip.Face.Background.ShouldBe(SemanticColor.Window);
        tooltip.Face.Foreground.ShouldBe(SemanticColor.WindowText);
        tooltip.Border.Sides.ShouldBe(BorderSide.None);
    }

    /// <summary>Verifies Tooltip is the Popup surface instead of owning a private Popup proxy.</summary>
    [Fact]
    public void Constructor_WhenCreated_IsDirectPopupSurface()
    {
        using var tooltip = new Tooltip();

        tooltip.GetType().BaseType.ShouldBe(typeof(Popup));
        OwnedTree.FindAll<Popup>(tooltip).Single().ShouldBeSameAs(tooltip);
        tooltip.GetType()
            .GetFields(
                ReflectionBindingFlags.Instance |
                ReflectionBindingFlags.NonPublic |
                ReflectionBindingFlags.DeclaredOnly)
            .ShouldNotContain(field => field.FieldType == typeof(Popup) ||
                                       field.FieldType == typeof(OwnedControlSlot));
    }

    /// <summary>Verifies SetText creates and attaches a tooltip with text content.</summary>
    [Fact]
    public void SetText_WhenCalled_CreatesTooltipForAnchor()
    {
        using var anchor = new ProbeControl(new Size(6, 1));
        Tooltip.SetText(anchor, "Save");
        var tooltip = Tooltip.GetTooltip(anchor);
        _ = tooltip.ShouldNotBeNull();
        tooltip.Text.ShouldBe("Save");
        tooltip.Parent.ShouldBeSameAs(anchor);
        tooltip.OwningSlot.ShouldNotBeNull().Options.Layer.ShouldBe(OwnedControlLayer.Popup);
        OwnedTree.FindAll<Popup>(anchor).Single().ShouldBeSameAs(tooltip);
    }

    /// <summary>Verifies the attached Tooltip itself publishes elevated Popup geometry.</summary>
    [Fact]
    public async Task IsOpen_WhenMounted_PublishesBoundsOnTooltipSurfaceAsync()
    {
        var anchor = new Button
        {
            Text = "Anchor",
            Width = Length.Cells(8),
            Height = Length.Cells(1)
        };
        Tooltip.SetText(anchor, "Save");
        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();
        await using var surface = await ComponentSurface.MountAsync(
            anchor,
            new Size(18, 7),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => tooltip.IsOpen = true,
            "open direct Tooltip surface");

        tooltip.SurfaceBounds.Width.ShouldBeGreaterThan(0);
        tooltip.SurfaceBounds.Y.ShouldBeGreaterThanOrEqualTo(anchor.Bounds.Bottom);
        OwnedTree.FindAll<Popup>(anchor).Single().ShouldBeSameAs(tooltip);
    }

    /// <summary>Verifies SetContent creates and attaches a tooltip with rich content.</summary>
    [Fact]
    public void SetContent_WhenCalled_CreatesTooltipForAnchor()
    {
        using var anchor = new ProbeControl(new Size(6, 1));
        using var content = new ProbeControl(new Size(4, 2));
        Tooltip.SetContent(anchor, content);
        var tooltip = Tooltip.GetTooltip(anchor);
        _ = tooltip.ShouldNotBeNull();
        tooltip.Content.ShouldBeSameAs(content);
    }

    /// <summary>Verifies ClearTooltip removes the attached tooltip from a control.</summary>
    [Fact]
    public void ClearTooltip_WhenCalled_RemovesTooltip()
    {
        using var anchor = new ProbeControl(new Size(6, 1));
        Tooltip.SetText(anchor, "Save");
        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();
        Tooltip.ClearTooltip(anchor);

        Tooltip.GetTooltip(anchor).ShouldBeNull();
        tooltip.Parent.ShouldBeNull();
        tooltip.Anchor.ShouldBeNull();
        OwnedTree.FindAll<Popup>(anchor).ShouldBeEmpty();
    }

    /// <summary>Verifies clearing then setting reuses the anchor's empty tooltip ownership part.</summary>
    [Fact]
    public void SetText_WhenTooltipWasCleared_AttachesReplacementToReusablePart()
    {
        using var anchor = new ProbeControl(new Size(6, 1));
        Tooltip.SetText(anchor, "First");
        var first = Tooltip.GetTooltip(anchor).ShouldNotBeNull();
        Tooltip.ClearTooltip(anchor);

        Tooltip.SetText(anchor, "Second");

        var replacement = Tooltip.GetTooltip(anchor).ShouldNotBeNull();
        replacement.ShouldNotBeSameAs(first);
        replacement.Parent.ShouldBeSameAs(anchor);
        replacement.Text.ShouldBe("Second");
        OwnedTree.FindAll<Popup>(anchor).ShouldBe([replacement]);
    }

    /// <summary>Verifies pointer hover shows and hides the passive tooltip through deterministic delays.</summary>
    [Fact]
    public async Task Pointer_WhenHoverDelaysElapse_ShowsThenHidesTooltipAsync()
    {
        var anchor = new Button
        {
            Text = "Anchor",
            Width = Length.Cells(8),
            Height = Length.Cells(3)
        };
        Tooltip.SetText(anchor, "Save");
        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();
        tooltip.ShowDelay = TimeSpan.FromMilliseconds(10);
        tooltip.HideDelay = TimeSpan.FromMilliseconds(10);
        var clock = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            anchor,
            new Size(18, 7),
            clock,
            TestContext.Current.CancellationToken);

        await surface.Pointer.MoveToAsync(anchor);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(10), "show Tooltip after hover delay");

        tooltip.IsOpen.ShouldBeTrue();
        tooltip.SurfaceBounds.Width.ShouldBeGreaterThan(0);
        var text = tooltip.Content.ShouldNotBeNull();
        text.Bounds.Width.ShouldBeGreaterThanOrEqualTo(4);
        surface.Cell(new Point(text.Bounds.X, text.Bounds.Y)).Text.ShouldBe("S");
        surface.Cell(new Point(text.Bounds.X + 3, text.Bounds.Y)).Text.ShouldBe("e");
        tooltip.Focused.ShouldBeFalse();

        await surface.Pointer.LeaveAsync();
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(10), "hide Tooltip after exit delay");

        tooltip.IsOpen.ShouldBeFalse();
        tooltip.SurfaceBounds.ShouldBe(default);
    }

    /// <summary>Verifies a pending show timer does not fire after its anchor detaches (e.g. a
    /// virtualized row being recycled), and that reattaching the anchor does not silently
    /// re-present the tooltip with no actual hover/focus interaction from the user.</summary>
    [Fact]
    public async Task Pointer_WhenAnchorDetachesDuringShowDelay_CancelsTimerAndStaysClosedAsync()
    {
        var anchor = new Button
        {
            Text = "Anchor",
            Width = Length.Cells(8),
            Height = Length.Cells(3)
        };
        Tooltip.SetText(anchor, "Save");
        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();
        tooltip.ShowDelay = TimeSpan.FromMilliseconds(10);
        var container = new Stack { Children = { anchor } };
        var clock = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            container,
            new Size(18, 7),
            clock,
            TestContext.Current.CancellationToken);

        // Started directly rather than through a real hover/focus interaction:
        // PointerManager and FocusManager both re-validate their tracked
        // control on detachment and synthesize an exit that already cancels
        // a timer started through the normal path, which would make this
        // regression pass regardless of the fix under test. Reflection
        // isolates the scenario the issue actually describes — a timer
        // pending when the anchor detaches by whatever means.
        var startShowTimer = typeof(Tooltip).GetMethod(
            "StartShowTimer",
            ReflectionBindingFlags.Instance | ReflectionBindingFlags.NonPublic).ShouldNotBeNull();
        await surface.UpdateAsync(() => startShowTimer.Invoke(tooltip, null), "start the show timer directly");

        var timerField = typeof(Tooltip).GetField(
            "_showTimer",
            ReflectionBindingFlags.Instance | ReflectionBindingFlags.NonPublic).ShouldNotBeNull();
        timerField.GetValue(tooltip).ShouldBeOfType<DispatcherTimer>().Running.ShouldBeTrue();

        await surface.UpdateAsync(
            () => container.Children.Remove(anchor).ShouldBeTrue(),
            "detach the anchor while its show timer is still pending");

        timerField.GetValue(tooltip).ShouldBeOfType<DispatcherTimer>().Running.ShouldBeFalse();

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(10), "let the cancelled show delay elapse");

        tooltip.IsOpen.ShouldBeFalse();

        await surface.UpdateAsync(
            () => container.Children.Add(anchor),
            "reattach the anchor without any new hover/focus interaction");

        tooltip.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies overlapping show and hide requests retain one timer callback per transition.</summary>
    [Fact]
    public async Task PointerAndFocus_WhenRequestsOverlap_UseOneTimerSubscriptionPerTransitionAsync()
    {
        var anchor = new Button
        {
            Text = "Anchor",
            Width = Length.Cells(8),
            Height = Length.Cells(3)
        };
        Tooltip.SetText(anchor, "Save");
        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();
        tooltip.ShowDelay = TimeSpan.FromMilliseconds(10);
        var openTransitions = 0;
        var closingCalls = 0;
        var closedCalls = 0;
        tooltip.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Popup.IsOpen) && tooltip.IsOpen)
            {
                openTransitions++;
            }
        };
        tooltip.Closing += (_, _) => closingCalls++;
        tooltip.Closed += (_, _) => closedCalls++;
        var clock = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            anchor,
            new Size(18, 7),
            clock,
            TestContext.Current.CancellationToken);

        await surface.Pointer.MoveToAsync(anchor);
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(anchor).ShouldBeTrue(),
            "overlap Tooltip hover with focus");

        tooltip.ShowTimerTickSubscribers.ShouldBe(1);

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(10), "deliver one overlapped Tooltip show");

        tooltip.IsOpen.ShouldBeTrue();
        openTransitions.ShouldBe(1);

        await surface.Pointer.LeaveAsync();
        await surface.Pointer.MoveToAsync(anchor);
        await surface.Pointer.LeaveAsync();

        tooltip.HideTimerTickSubscribers.ShouldBe(1);

        await surface.AdvanceAsync(tooltip.HideDelay, "deliver one overlapped Tooltip hide");

        tooltip.IsOpen.ShouldBeFalse();
        closingCalls.ShouldBe(1);
        closedCalls.ShouldBe(1);
    }

    /// <summary>Verifies SetText updates existing tooltip instead of creating a new one.</summary>
    [Fact]
    public void SetText_WhenCalledTwice_UpdatesExistingTooltip()
    {
        using var anchor = new ProbeControl(new Size(6, 1));
        Tooltip.SetText(anchor, "Save");
        Tooltip.SetText(anchor, "Save All");
        var tooltip = Tooltip.GetTooltip(anchor);
        _ = tooltip.ShouldNotBeNull();
        tooltip.Text.ShouldBe("Save All");
    }

    /// <summary>Verifies GetTooltip returns null for controls without attached tooltips.</summary>
    [Fact]
    public void GetTooltip_WhenNoTooltipSet_ReturnsNull()
    {
        using var anchor = new ProbeControl(new Size(6, 1));
        Tooltip.GetTooltip(anchor).ShouldBeNull();
    }

    /// <summary>Verifies a multi-line tooltip anchored near the bottom row flips fully above its
    /// anchor instead of overflowing the surface below it, mirroring
    /// <c>PopupSurfaceTests.Render_WhenNoSpaceBelowAnchor_FlipsPopupToAboveAsync</c> for the
    /// attached-tooltip hosting path.</summary>
    [Fact]
    public async Task IsOpen_WhenAnchoredNearBottomWithMultilineContent_FlipsAboveAnchorAsync()
    {
        // Arrange — anchor sits on the bottom row, leaving no room below for a two-line tooltip.
        var anchor = new Button
        {
            Text = "Bottom",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(10),
            Height = Length.Cells(1)
        };
        Overlay.SetTop(anchor, Length.Cells(7));
        Tooltip.SetText(anchor, "First line\nSecond line");
        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();
        var root = new Overlay
        {
            Width = Length.Cells(12),
            Height = Length.Cells(8),
            Children = { anchor }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 8),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => tooltip.IsOpen = true, "open Tooltip near bottom");

        // Assert — the tooltip should flip above the anchor, not render below/over the footer.
        tooltip.IsOpen.ShouldBeTrue();
        tooltip.SurfaceBounds.Height.ShouldBeGreaterThan(1);
        tooltip.SurfaceBounds.Bottom.ShouldBeLessThanOrEqualTo(anchor.Bounds.Y);
    }

    /// <summary>Verifies growing an open tooltip's text past the room left below its anchor
    /// re-resolves placement instead of leaving it pinned to the smaller bounds measured when
    /// it first opened. Tooltip.LayoutPopup only ever runs from OnContentAvailable at open time;
    /// SetText mutating the shared text child in place afterward raises no Content-changed
    /// notification the framework's normal layout walk would ever observe.</summary>
    [Fact]
    public async Task Text_WhenChangedWhileOpenGrowsPastBottom_RecomputesFlipAsync()
    {
        var anchor = new Button
        {
            Text = "Bottom",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(10),
            Height = Length.Cells(1)
        };
        Overlay.SetTop(anchor, Length.Cells(6));
        Tooltip.SetText(anchor, "One");
        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();
        var root = new Overlay
        {
            Width = Length.Cells(12),
            Height = Length.Cells(8),
            Children = { anchor }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 8),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => tooltip.IsOpen = true, "open Tooltip with content that still fits below");
        tooltip.SurfaceBounds.Bottom.ShouldBeLessThanOrEqualTo(8);

        await surface.UpdateAsync(() => tooltip.Text = "One\nTwo", "grow Tooltip content while open");

        tooltip.SurfaceBounds.Height.ShouldBeGreaterThan(1);
        tooltip.SurfaceBounds.Bottom.ShouldBeLessThanOrEqualTo(anchor.Bounds.Y);
    }

    /// <summary>Verifies an open tooltip re-resolves placement after the surface it is presented
    /// on shrinks, rather than continuing to render past the new bottom edge. Because a Tooltip
    /// lives in its anchor's Popup-layer owned slot rather than as a normal tree child, the
    /// framework's cascading resize-driven Measure/Arrange walk never reaches it the way it
    /// reaches a ComboBox or DateInput's own popup child.</summary>
    [Fact]
    public async Task IsOpen_WhenSurfaceShrinksWhileOpenNearBottom_RecomputesFlipAsync()
    {
        var anchor = new Button
        {
            Text = "Bottom",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(10),
            Height = Length.Cells(1)
        };
        Overlay.SetTop(anchor, Length.Cells(6));
        Tooltip.SetText(anchor, "One");
        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();
        var root = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { anchor }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 8),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => tooltip.IsOpen = true, "open tooltip that fits below at initial size");
        tooltip.SurfaceBounds.Bottom.ShouldBeLessThanOrEqualTo(8);

        await surface.ResizeAsync(new Size(12, 7));

        tooltip.SurfaceBounds.Bottom.ShouldBeLessThanOrEqualTo(anchor.Bounds.Y);
    }

    /// <summary>Verifies an open tooltip re-resolves placement after its anchor reflows to a new
    /// position (a preceding sibling growing above it), rather than continuing to render at the
    /// anchor's old location. Distinct from the resize path above: here the mounted root's own
    /// Bounds never change, only the anchor's.</summary>
    [Fact]
    public async Task IsOpen_WhenAnchorReflowsPastBottomWhileOpen_RecomputesFlipAsync()
    {
        var spacer = new Button
        {
            Text = string.Empty,
            Width = Length.Cells(1),
            Height = Length.Cells(4)
        };
        var anchor = new Button
        {
            Text = "Bottom",
            Width = Length.Cells(10),
            Height = Length.Cells(1)
        };
        Tooltip.SetText(anchor, "One");
        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();
        var root = new Stack
        {
            Orientation = Orientation.Vertical,
            Width = Length.Cells(12),
            Height = Length.Cells(8),
            Children = { spacer, anchor }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 8),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => tooltip.IsOpen = true, "open tooltip that fits below at initial anchor position");
        tooltip.SurfaceBounds.Bottom.ShouldBeLessThanOrEqualTo(8);

        await surface.UpdateAsync(() => spacer.Height = Length.Cells(7), "reflow the anchor toward the bottom while open");

        tooltip.SurfaceBounds.Bottom.ShouldBeLessThanOrEqualTo(anchor.Bounds.Y);
    }
}
