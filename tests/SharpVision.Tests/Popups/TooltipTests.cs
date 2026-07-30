// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Popups;

using SharpVision.Tests.Input;

using ReflectionBindingFlags = System.Reflection.BindingFlags;

/// <summary>Verifies tooltip attachment, triggering, and content display.</summary>
public sealed class TooltipTests
{
    /// <summary>Verifies a new tooltip has expected defaults for all properties.</summary>
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
        tooltip.IsHitTestVisible.ShouldBeFalse();
        tooltip.Focusable.ShouldBeFalse();
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
            Content = new ControlText("Anchor"),
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
            Content = new ControlText("Anchor"),
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
        tooltip.IsFocused.ShouldBeFalse();

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
            Content = new ControlText("Anchor"),
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
        timerField.GetValue(tooltip).ShouldBeOfType<DispatcherTimer>().IsRunning.ShouldBeTrue();

        await surface.UpdateAsync(
            () => container.Children.Remove(anchor).ShouldBeTrue(),
            "detach the anchor while its show timer is still pending");

        timerField.GetValue(tooltip).ShouldBeOfType<DispatcherTimer>().IsRunning.ShouldBeFalse();

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
            Content = new ControlText("Anchor"),
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

        var timerField = typeof(Tooltip).GetField(
            "_showTimer",
            ReflectionBindingFlags.Instance | ReflectionBindingFlags.NonPublic).ShouldNotBeNull();
        var timer = timerField.GetValue(tooltip).ShouldBeOfType<DispatcherTimer>();
        var tickField = typeof(DispatcherTimer).GetField(
            nameof(DispatcherTimer.Tick),
            ReflectionBindingFlags.Instance | ReflectionBindingFlags.NonPublic).ShouldNotBeNull();
        var tick = tickField.GetValue(timer).ShouldBeOfType<EventHandler>();

        tick.GetInvocationList().Length.ShouldBe(1);

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(10), "deliver one overlapped Tooltip show");

        tooltip.IsOpen.ShouldBeTrue();
        openTransitions.ShouldBe(1);

        await surface.Pointer.LeaveAsync();
        await surface.Pointer.MoveToAsync(anchor);
        await surface.Pointer.LeaveAsync();
        var hideTimerField = typeof(Tooltip).GetField(
            "_hideTimer",
            ReflectionBindingFlags.Instance | ReflectionBindingFlags.NonPublic).ShouldNotBeNull();
        var hideTimer = hideTimerField.GetValue(tooltip).ShouldBeOfType<DispatcherTimer>();
        var hideTick = tickField.GetValue(hideTimer).ShouldBeOfType<EventHandler>();

        hideTick.GetInvocationList().Length.ShouldBe(1);

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
}
