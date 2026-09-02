// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Popups;

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
        tooltip.IsFocusable.ShouldBeFalse();
    }

    /// <summary>Verifies Tooltip proves direct and ancestor-inherited disabled state at the
    /// detached unit level, and that clearing IsEnabled on each recovers EffectiveIsEnabled - the
    /// same disabled contract exercised on a live mounted terminal surface.</summary>
    [Fact]
    public void EffectiveIsEnabled_WhenTooltipIsDisabledDirectlyOrByAncestor_ReportsDisabledAndRecovers()
    {
        using var tooltip = new Tooltip();
        using var host = new Overlay { Children = { tooltip } };

        tooltip.IsEnabled = false;
        tooltip.EffectiveIsEnabled.ShouldBeFalse();

        tooltip.IsEnabled = true;
        tooltip.EffectiveIsEnabled.ShouldBeTrue();

        host.IsEnabled = false;
        tooltip.IsEnabled.ShouldBeTrue();
        tooltip.EffectiveIsEnabled.ShouldBeFalse();

        host.IsEnabled = true;
        tooltip.EffectiveIsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies a Tooltip resolves the dedicated Tooltip role, framed on the same window
    /// plane as Popup but with a light border rather than Popup's rounded frame, so a passive
    /// hint stays visually contained while remaining distinct from an interactive drop-down or
    /// menu.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesLightFramedTooltipStyle()
    {
        using var tooltip = new Tooltip();

        tooltip.Face.Background.ShouldBe(SemanticColor.Window);
        tooltip.Face.Foreground.ShouldBe(SemanticColor.WindowText);
        tooltip.Border.Sides.ShouldBe(BorderSide.All);
        tooltip.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Light);
    }

    /// <summary>Verifies a Tooltip opts out of ambient text-appearance inheritance by
    /// construction - inherited from Popup's own constructor, which runs before Tooltip's, and
    /// never reset afterward - so a passive hint always starts fresh regardless of which theme is
    /// active.</summary>
    [Fact]
    public void Constructor_WhenCreated_IsAppearanceBoundary()
    {
        using var tooltip = new Tooltip();

        tooltip.IsAppearanceBoundary.ShouldBeTrue();
    }

    /// <summary>Verifies a Tooltip does not inherit an ambient parent's Foreground even when the
    /// active theme leaves "control" (and every well-known style section) entirely unauthored -
    /// the one condition under which the code-owned <see cref="ControlStyle.DefaultFace"/>
    /// (transparent background, no LocalFace) would otherwise satisfy AppearanceResolver's
    /// ambient-inheritance gate. Every bundled theme, and every other <see cref="ThemeJson.Create"/>
    /// call, authors "control" with a face - which "window"/"popup"/"tooltip" cascade onto their
    /// own Normal regardless of whether they author a "face" of their own - so this only
    /// reproduces with a theme whose "styles" object is empty.</summary>
    [Fact]
    public void ResolveAppearance_WhenThemeLeavesTooltipFaceUnauthored_DoesNotInheritAmbientForeground()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(stylesOverride: "{}"));
        var ambientForeground = Color.Rgb(200, 30, 40);
        var parent = new ProbeContainer
        {
            Face = AppearanceTestValues.Face(foreground: ambientForeground, background: Color.Rgb(1, 1, 1))
        };
        using var tooltip = new Tooltip();
        parent.Children.Add(tooltip);

        var resolved = tooltip.ResolveAppearance(theme);

        resolved.Face.Background.Literal.ShouldBe(Color.Transparent);
        resolved.Face.Foreground.Literal.ShouldBe(Color.Default);
        resolved.Face.Foreground.Literal.ShouldNotBe(ambientForeground);
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

    /// <summary>Verifies a too-small or too-large ShowDelay is rejected before the previous value
    /// changes, matching DispatcherTimer's own interval contract.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ShowDelay_WhenIntervalIsBelowOneMillisecond_ThrowsArgumentOutOfRangeException(int milliseconds)
    {
        using var tooltip = new Tooltip();

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => tooltip.ShowDelay = TimeSpan.FromMilliseconds(milliseconds));

        tooltip.ShowDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
    }

    /// <summary>Verifies a too-small or too-large HideDelay is rejected before the previous value
    /// changes, matching DispatcherTimer's own interval contract.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void HideDelay_WhenIntervalIsBelowOneMillisecond_ThrowsArgumentOutOfRangeException(int milliseconds)
    {
        using var tooltip = new Tooltip();

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => tooltip.HideDelay = TimeSpan.FromMilliseconds(milliseconds));

        tooltip.HideDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
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

    /// <summary>Verifies the placement overload of SetText applies both text and placement.</summary>
    [Fact]
    public void SetText_WhenGivenPlacement_SetsTextAndPlacement()
    {
        using var anchor = new ProbeControl(new Size(6, 1));

        Tooltip.SetText(anchor, "Save", PopupPlacement.Right);

        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();
        tooltip.Text.ShouldBe("Save");
        tooltip.Placement.ShouldBe(PopupPlacement.Right);
    }

    /// <summary>Verifies an unknown placement passed to the placement overload of SetText is rejected.</summary>
    [Fact]
    public void SetText_WhenGivenUnknownPlacement_ThrowsArgumentOutOfRangeException()
    {
        using var anchor = new ProbeControl(new Size(6, 1));
        Tooltip.SetText(anchor, "Original", PopupPlacement.Above);
        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => Tooltip.SetText(anchor, "Replacement", (PopupPlacement) 99));

        tooltip.Text.ShouldBe("Original");
        tooltip.Placement.ShouldBe(PopupPlacement.Above);
    }

    /// <summary>Verifies the placement-and-delay overload of SetText applies text, placement, and
    /// ShowDelay together.</summary>
    [Fact]
    public void SetText_WhenGivenPlacementAndShowDelay_SetsAllThree()
    {
        using var anchor = new ProbeControl(new Size(6, 1));

        Tooltip.SetText(anchor, "Save", PopupPlacement.Above, TimeSpan.FromMilliseconds(250));

        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();
        tooltip.Text.ShouldBe("Save");
        tooltip.Placement.ShouldBe(PopupPlacement.Above);
        tooltip.ShowDelay.ShouldBe(TimeSpan.FromMilliseconds(250));
    }

    /// <summary>Verifies an invalid ShowDelay passed to the placement-and-delay overload of SetText
    /// is rejected, matching the ShowDelay property's own validation.</summary>
    [Fact]
    public void SetText_WhenGivenInvalidShowDelay_ThrowsArgumentOutOfRangeException()
    {
        using var anchor = new ProbeControl(new Size(6, 1));
        Tooltip.SetText(anchor, "Original", PopupPlacement.Above, TimeSpan.FromMilliseconds(250));
        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => Tooltip.SetText(anchor, "Replacement", PopupPlacement.Below, TimeSpan.Zero));

        tooltip.Text.ShouldBe("Original");
        tooltip.Placement.ShouldBe(PopupPlacement.Above);
        tooltip.ShowDelay.ShouldBe(TimeSpan.FromMilliseconds(250));
    }

    /// <summary>Verifies the placement overload of SetContent applies both content and placement.</summary>
    [Fact]
    public void SetContent_WhenGivenPlacement_SetsContentAndPlacement()
    {
        using var anchor = new ProbeControl(new Size(6, 1));
        using var content = new ProbeControl(new Size(4, 2));

        Tooltip.SetContent(anchor, content, PopupPlacement.Left);

        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();
        tooltip.Content.ShouldBeSameAs(content);
        tooltip.Placement.ShouldBe(PopupPlacement.Left);
    }

    /// <summary>Verifies an unknown placement passed to the placement overload of SetContent is rejected.</summary>
    [Fact]
    public void SetContent_WhenGivenUnknownPlacement_ThrowsArgumentOutOfRangeException()
    {
        using var anchor = new ProbeControl(new Size(6, 1));
        using var original = new ProbeControl(new Size(3, 1));
        using var content = new ProbeControl(new Size(4, 2));
        Tooltip.SetContent(anchor, original, PopupPlacement.Above);
        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => Tooltip.SetContent(anchor, content, (PopupPlacement) 99));

        tooltip.Content.ShouldBeSameAs(original);
        tooltip.Placement.ShouldBe(PopupPlacement.Above);
    }

    /// <summary>Verifies every static attached-data method rejects a null anchor.</summary>
    [Fact]
    public void StaticMethods_WhenAnchorIsNull_ThrowArgumentNullException()
    {
        using var content = new ProbeControl(new Size(4, 2));

        _ = Should.Throw<ArgumentNullException>(() => Tooltip.SetText(null!, "Save"));
        _ = Should.Throw<ArgumentNullException>(() => Tooltip.SetText(null!, "Save", PopupPlacement.Below));
        _ = Should.Throw<ArgumentNullException>(
            () => Tooltip.SetText(null!, "Save", PopupPlacement.Below, TimeSpan.FromMilliseconds(100)));
        _ = Should.Throw<ArgumentNullException>(() => Tooltip.SetContent(null!, content));
        _ = Should.Throw<ArgumentNullException>(() => Tooltip.SetContent(null!, content, PopupPlacement.Below));
        _ = Should.Throw<ArgumentNullException>(() => Tooltip.GetTooltip(null!));
        _ = Should.Throw<ArgumentNullException>(() => Tooltip.ClearTooltip(null!));
    }

    /// <summary>Verifies SetText and SetContent reject a null text or content argument.</summary>
    [Fact]
    public void SetTextAndSetContent_WhenValueIsNull_ThrowArgumentNullException()
    {
        using var anchor = new ProbeControl(new Size(6, 1));

        _ = Should.Throw<ArgumentNullException>(() => Tooltip.SetText(anchor, null!));
        _ = Should.Throw<ArgumentNullException>(() => Tooltip.SetContent(anchor, null!));
    }

    /// <summary>Verifies clearing a control with no attached tooltip is a no-op rather than throwing.</summary>
    [Fact]
    public void ClearTooltip_WhenNoTooltipIsAttached_IsNoOp()
    {
        using var anchor = new ProbeControl(new Size(6, 1));

        Should.NotThrow(() => Tooltip.ClearTooltip(anchor));

        Tooltip.GetTooltip(anchor).ShouldBeNull();
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

    /// <summary>Verifies a failing close observer cannot abort attached-tooltip association,
    /// ownership, presentation, timer, or base disposal cleanup.</summary>
    [Fact]
    public async Task ClearTooltip_WhenCloseRequestedObserverFails_CompletesCleanupBeforeRethrowAsync()
    {
        var anchor = new Button { Text = "Anchor" };
        Tooltip.SetText(anchor, "Save");
        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();
        await using var surface = await ComponentSurface.MountAsync(
            anchor,
            new Size(18, 7),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => tooltip.IsOpen = true, "show attached Tooltip");
        var expected = new InvalidOperationException("close request failed");
        tooltip.CloseRequested += (_, _) => throw expected;

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
            surface.UpdateAsync(() => Tooltip.ClearTooltip(anchor), "clear failing Tooltip"));

        thrown.ShouldBeSameAs(expected);
        Tooltip.GetTooltip(anchor).ShouldBeNull();
        tooltip.Parent.ShouldBeNull();
        tooltip.Anchor.ShouldBeNull();
        tooltip.SurfaceBounds.ShouldBe(default);
        OwnedTree.FindAll<Popup>(anchor).ShouldBeEmpty();
        tooltip.Dispose();
        tooltip.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies direct disposal completes the same association and base cleanup when its
    /// attempted close callback fails.</summary>
    [Fact]
    public async Task Dispose_WhenCloseRequestedObserverFails_CompletesCleanupBeforeRethrowAsync()
    {
        var anchor = new Button { Text = "Anchor" };
        Tooltip.SetText(anchor, "Save");
        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();
        await using var surface = await ComponentSurface.MountAsync(
            anchor,
            new Size(18, 7),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => tooltip.IsOpen = true, "show attached Tooltip");
        var expected = new InvalidOperationException("close request failed");
        tooltip.CloseRequested += (_, _) => throw expected;

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
            surface.UpdateAsync(tooltip.Dispose, "dispose failing Tooltip"));

        thrown.ShouldBeSameAs(expected);
        Tooltip.GetTooltip(anchor).ShouldBeNull();
        tooltip.IsDisposed.ShouldBeTrue();
        tooltip.Parent.ShouldBeNull();
        tooltip.SurfaceBounds.ShouldBe(default);
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
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel },
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

    /// <summary>Verifies an open tooltip visually contains itself over already-occupied backdrop
    /// content: its perimeter renders the light border glyphs rather than leaving the backdrop's
    /// text showing through, and its interior shows the tooltip's own content rather than the
    /// backdrop underneath. A borderless tooltip floating over busy content used to blend directly
    /// into whatever was already there; the frame is what keeps it legible.</summary>
    [Fact]
    public async Task Pointer_WhenShownOverOccupiedCells_RendersContainedFrameAsync()
    {
        const int surfaceWidth = 24;
        const int surfaceHeight = 10;
        var backdropLine = new string('#', surfaceWidth);
        var backdrop = new ControlText(string.Join('\n', Enumerable.Repeat(backdropLine, surfaceHeight)))
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var anchor = new Button
        {
            Text = "Anchor",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3)
        };
        Overlay.SetTop(anchor, Length.Cells(1));
        Overlay.SetLeft(anchor, Length.Cells(2));
        Tooltip.SetText(anchor, "First line\nSecond line");
        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();
        tooltip.ShowDelay = TimeSpan.FromMilliseconds(10);

        var root = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { backdrop, anchor }
        };
        var clock = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(surfaceWidth, surfaceHeight),
            clock,
            TestContext.Current.CancellationToken);

        await surface.Pointer.MoveToAsync(anchor);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(10), "show Tooltip over occupied backdrop cells");

        tooltip.IsOpen.ShouldBeTrue();
        var bounds = tooltip.SurfaceBounds;
        bounds.Width.ShouldBeGreaterThan(2);
        bounds.Height.ShouldBeGreaterThan(2);

        // The frame's four corners and two sampled edges render the light glyphs, not the '#'
        // backdrop that would otherwise show through a borderless tooltip.
        surface.Cell(new Point(bounds.X, bounds.Y)).Text.ShouldBe("┌");
        surface.Cell(new Point(bounds.Right - 1, bounds.Y)).Text.ShouldBe("┐");
        surface.Cell(new Point(bounds.X, bounds.Bottom - 1)).Text.ShouldBe("└");
        surface.Cell(new Point(bounds.Right - 1, bounds.Bottom - 1)).Text.ShouldBe("┘");
        surface.Cell(new Point(bounds.X + 1, bounds.Y)).Text.ShouldBe("─");
        surface.Cell(new Point(bounds.X, bounds.Y + 1)).Text.ShouldBe("│");

        // The interior shows the tooltip's own content, never the backdrop underneath it.
        surface.Cell(new Point(bounds.X + 1, bounds.Y + 1)).Text.ShouldBe("F");
        surface.Cell(new Point(bounds.X + 1, bounds.Y + 1)).Text.ShouldNotBe("#");

        // Just outside the frame - to its right, on a row below the anchor - the backdrop is
        // untouched, showing containment does not bleed past the border.
        bounds.Right.ShouldBeLessThan(surfaceWidth);
        surface.Cell(new Point(bounds.Right, bounds.Y + 1)).Text.ShouldBe("#");
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
        // PointerManager and FocusManager both re-validate their tracked control on detachment
        // and synthesize an exit that already cancels a timer started through the normal path.
        await surface.UpdateAsync(tooltip.StartShowTimerForLifecycleTest, "start the show timer directly");

        tooltip.HasShowTimer.ShouldBeTrue();
        tooltip.IsShowTimerRunning.ShouldBeTrue();

        await surface.UpdateAsync(
            () => container.Children.Remove(anchor).ShouldBeTrue(),
            "detach the anchor while its show timer is still pending");

        tooltip.HasShowTimer.ShouldBeFalse();

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(10), "let the cancelled show delay elapse");

        tooltip.IsOpen.ShouldBeFalse();

        await surface.UpdateAsync(
            () => container.Children.Add(anchor),
            "reattach the anchor without any new hover/focus interaction");

        tooltip.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies a tooltip force-closed by its own Visibility being set to Hidden - the
    /// same release reason Popup already force-closes identically alongside Detached and Disposed
    /// - releases its dispatcher-owned show/hide timers and drops its presented-surface relayout
    /// subscription, matching what Detached (via OnDetached) and Disposed (via OnUnavailable)
    /// already did. Before this fix, a merely-hidden tooltip released neither: Hidden never
    /// cascades OnDetached (it is a pure visibility change), and OnUnavailable only special-cased
    /// Disposed.</summary>
    [Fact]
    public async Task Visibility_WhenSetToHiddenWhileOpen_ReleasesTimersAndRelayoutSubscriptionAsync()
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
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel },
            TestContext.Current.CancellationToken);

        await surface.Pointer.MoveToAsync(anchor);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(10), "show Tooltip after hover delay");

        tooltip.IsOpen.ShouldBeTrue();
        tooltip.HasSurfaceRelayoutSubscription.ShouldBeTrue();

        await surface.Pointer.LeaveAsync();

        // A pending hide timer (and the already-ticked-but-not-yet-released show timer) prove
        // real timer objects exist to release, not just an absence the fix could accidentally
        // pass by doing nothing.
        tooltip.HasShowTimer.ShouldBeTrue();
        tooltip.HideTimerTickSubscribers.ShouldBe(1);

        await surface.UpdateAsync(() => tooltip.Visibility = Visibility.Hidden, "hide the open Tooltip surface directly");

        tooltip.HasShowTimer.ShouldBeFalse();
        tooltip.ShowTimerTickSubscribers.ShouldBe(0);
        tooltip.HideTimerTickSubscribers.ShouldBe(0);
        tooltip.HasSurfaceRelayoutSubscription.ShouldBeFalse();
    }

    /// <summary>Verifies a tooltip migrated between dispatchers creates its next delay on the new
    /// dispatcher instead of restarting the stopped timer owned by the former attachment.</summary>
    [Fact]
    public async Task Pointer_WhenAnchorReattachesToNewDispatcher_UsesNewDispatcherTimerAsync()
    {
        var previousClock = new ManualTimeProvider();
        var currentClock = new ManualTimeProvider();
        await using var previousDispatcher = Dispatcher.Start(timeProvider: previousClock);
        await using var currentDispatcher = Dispatcher.Start(timeProvider: currentClock);
        var anchor = new Button { Text = "Anchor" };
        Tooltip.SetText(anchor, "Save");
        var tooltip = Tooltip.GetTooltip(anchor).ShouldNotBeNull();
        tooltip.ShowDelay = TimeSpan.FromMilliseconds(10);
        await previousDispatcher.InvokeAsync(
            () =>
            {
                anchor.Attach(previousDispatcher);
                anchor.SetPointerOver(value: true, directlyOver: true);
                anchor.Detach();
            },
            TestContext.Current.CancellationToken);

        await currentDispatcher.InvokeAsync(
            () =>
            {
                anchor.Attach(currentDispatcher);
                anchor.SetPointerOver(value: false, directlyOver: false);
                anchor.SetPointerOver(value: true, directlyOver: true);
            },
            TestContext.Current.CancellationToken);
        previousClock.Advance(TimeSpan.FromMilliseconds(10));
        await previousDispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        tooltip.IsOpen.ShouldBeFalse();

        currentClock.Advance(TimeSpan.FromMilliseconds(10));
        await currentDispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        tooltip.IsOpen.ShouldBeTrue();
        await currentDispatcher.InvokeAsync(anchor.Detach, TestContext.Current.CancellationToken);
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
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel },
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
