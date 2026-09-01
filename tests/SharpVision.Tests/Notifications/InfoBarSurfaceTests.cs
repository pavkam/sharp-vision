// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Notifications;

using System.ComponentModel;

/// <summary>Verifies mounted InfoBar layout, rendering, navigation, and dismissal input.</summary>
public sealed class InfoBarSurfaceTests
{
    /// <summary>Verifies header, semantic chrome, retained body, and dismiss glyph render together.</summary>
    [Fact]
    public async Task Render_WhenHeaderAndContentExist_PaintsCompleteInfoBarAsync()
    {
        var bar = new InfoBar
        {
            Title = "Status",
            Adornment = new Affix("!"),
            Style = InfoBarStyle.Success,
            Content = new ControlText("Ready")
        };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(24, 7),
            TestContext.Current.CancellationToken);
        var inner = bar.ActualStyle.Padding.Deflate(bar.ContentBounds);
        var expectedAccent = ThemeCatalog.Dark.ResolveColor(SemanticColor.Success);

        surface.Cell(new Point(bar.Bounds.X, bar.Bounds.Y)).Style.Foreground.ShouldBe(expectedAccent);
        surface.Cell(new Point(inner.X, inner.Y)).Text.ShouldBe("!");
        surface.Cell(new Point(inner.X + 2, inner.Y)).Text.ShouldBe("S");
        surface.Cell(new Point(inner.Right - 1, inner.Y)).Text.ShouldBe("■");
        surface.Cell(new Point(inner.X, inner.Y + 2)).Text.ShouldBe("R");
    }

    /// <summary>Verifies a closed bar leaks no chrome, child rendering, hit target, focus, or extent.</summary>
    [Fact]
    public async Task IsOpen_WhenClosedInsideNonEmptySlot_RendersAndTargetsNothingAsync()
    {
        var body = new Button("Retry");
        var bar = new InfoBar { Title = "Alert", Content = body };
        var root = new Overlay { Children = { bar } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(body);

        await surface.UpdateAsync(() => bar.IsOpen = false, "close InfoBar");

        bar.DesiredSize.ShouldBe(default);
        body.Bounds.ShouldBe(default);
        surface.ShouldHaveFocus(null);
        bar.HitTest(new Point(1, 1)).ShouldBeNull();
        surface.ShouldRender("                    \n                    \n                    \n                    \n                    ");
    }

    /// <summary>Verifies body navigation is followed by the private dismiss part and Enter closes.</summary>
    [Fact]
    public async Task Keyboard_WhenTabReachesDismissPart_EnterDismissesAsync()
    {
        var body = new Button("Retry");
        var bar = new InfoBar { Title = "Alert", Content = body };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(20, 7),
            TestContext.Current.CancellationToken);

        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(body);
        await surface.Keyboard.PressAsync(Code.Tab);
        _ = surface.Application.Focus.Focused.ShouldBeOfType<InfoBarDismissButton>();

        await surface.Keyboard.PressAsync(Code.Enter);

        bar.IsOpen.ShouldBeFalse();
        surface.ShouldHaveFocus(null);
    }

    /// <summary>Verifies Space activates the retained dismiss part through ordinary press behavior.</summary>
    [Fact]
    public async Task Keyboard_WhenDismissPartHasFocus_SpaceDismissesAsync()
    {
        var bar = new InfoBar { Title = "Alert", Content = new Button("Retry") };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Tab);

        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));

        bar.IsOpen.ShouldBeFalse();
        surface.ShouldHaveFocus(null);
    }

    /// <summary>Verifies Escape remains available to an owning surface instead of dismissing the bar.</summary>
    [Fact]
    public async Task Keyboard_WhenEscapeArrives_DoesNotDismissAsync()
    {
        var bar = new InfoBar { Title = "Alert", Content = new Button("Retry") };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Tab);

        await surface.Keyboard.PressAsync(Code.Escape);

        bar.IsOpen.ShouldBeTrue();
        _ = surface.Application.Focus.Focused.ShouldBeOfType<InfoBarDismissButton>();
    }

    /// <summary>Verifies the retained dismiss part owns capture-aware pointer activation.</summary>
    [Fact]
    public async Task Pointer_WhenDismissGlyphIsClicked_DismissesAsync()
    {
        var bar = new InfoBar { Title = "Alert", Content = new ControlText("Body") };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        var inner = bar.ActualStyle.Padding.Deflate(bar.ContentBounds);

        await surface.Pointer.ClickAsync(
            bar,
            new Point(inner.Right - bar.Bounds.X - 1, inner.Y - bar.Bounds.Y));

        bar.IsOpen.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies a failing dismissibility observer cannot strand focus on the unavailable private part.</summary>
    [Fact]
    public async Task IsDismissible_WhenObserverThrows_StillCleansDismissPartAsync()
    {
        var body = new Button("Retry");
        var bar = new InfoBar { Title = "Alert", Content = body };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Tab);
        _ = surface.Application.Focus.Focused.ShouldBeOfType<InfoBarDismissButton>();
        bar.PropertyChanged += ThrowOnDismissibility;

        await surface.UpdateAsync(
            () => _ = Should.Throw<InvalidOperationException>(() => bar.IsDismissible = false),
            "disable dismissal with failing observer");

        bar.IsDismissible.ShouldBeFalse();
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveCapture(null);

        static void ThrowOnDismissibility(object? sender, PropertyChangedEventArgs eventArgs)
        {
            _ = sender;

            if (eventArgs.PropertyName == nameof(InfoBar.IsDismissible))
            {
                throw new InvalidOperationException("observer");
            }
        }
    }

    /// <summary>Verifies removing dismissal during a held press clears capture and suppresses activation.</summary>
    [Fact]
    public async Task IsDismissible_WhenChangedDuringPress_CancelsInteractionAsync()
    {
        var bar = new InfoBar { Title = "Alert", Content = new ControlText("Body") };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        var dismiss = OwnedTree.Find<InfoBarDismissButton>(bar).ShouldNotBeNull();
        await surface.Pointer.MoveToAsync(dismiss);
        await surface.Pointer.PressAsync();
        dismiss.IsPressed.ShouldBeTrue();
        surface.ShouldHaveCapture(dismiss);

        await surface.UpdateAsync(() => bar.IsDismissible = false, "disable dismissal during press");

        dismiss.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        await surface.Pointer.ReleaseAsync();
        bar.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies disabling the bar during a held press clears capture and suppresses activation.</summary>
    [Fact]
    public async Task IsEnabled_WhenChangedDuringPress_CancelsInteractionAsync()
    {
        var bar = new InfoBar { Title = "Alert", Content = new ControlText("Body") };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        var dismiss = OwnedTree.Find<InfoBarDismissButton>(bar).ShouldNotBeNull();
        await surface.Pointer.MoveToAsync(dismiss);
        await surface.Pointer.PressAsync();

        await surface.UpdateAsync(() => bar.IsEnabled = false, "disable InfoBar during press");

        dismiss.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        await surface.Pointer.ReleaseAsync();
        bar.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies hiding the bar during a held press clears capture and suppresses activation.</summary>
    [Fact]
    public async Task Visibility_WhenChangedDuringPress_CancelsInteractionAsync()
    {
        var bar = new InfoBar { Title = "Alert", Content = new ControlText("Body") };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        var dismiss = OwnedTree.Find<InfoBarDismissButton>(bar).ShouldNotBeNull();
        await surface.Pointer.MoveToAsync(dismiss);
        await surface.Pointer.PressAsync();

        await surface.UpdateAsync(() => bar.Visibility = Visibility.Hidden, "hide InfoBar during press");

        dismiss.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        await surface.Pointer.ReleaseAsync();
        bar.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies detaching the bar during a held press clears capture and suppresses activation.</summary>
    [Fact]
    public async Task Parent_WhenDetachedDuringPress_CancelsInteractionAsync()
    {
        var bar = new InfoBar { Title = "Alert", Content = new ControlText("Body") };
        var root = new Overlay { Children = { bar } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        var dismiss = OwnedTree.Find<InfoBarDismissButton>(bar).ShouldNotBeNull();
        await surface.Pointer.MoveToAsync(dismiss);
        await surface.Pointer.PressAsync();

        await surface.UpdateAsync(
            () => root.Children.Remove(bar).ShouldBeTrue(),
            "detach InfoBar during press");

        dismiss.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        await surface.Pointer.ReleaseAsync();
        bar.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies disposal during a held press clears capture without publishing dismissal.</summary>
    [Fact]
    public async Task Dispose_WhenCalledDuringPress_CancelsInteractionAsync()
    {
        var bar = new InfoBar { Title = "Alert", Content = new ControlText("Body") };
        var root = new Overlay { Children = { bar } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        var dismiss = OwnedTree.Find<InfoBarDismissButton>(bar).ShouldNotBeNull();
        var dismissed = 0;
        bar.Dismissed += (_, _) => dismissed++;
        await surface.Pointer.MoveToAsync(dismiss);
        await surface.Pointer.PressAsync();

        await surface.UpdateAsync(bar.Dispose, "dispose InfoBar during press");

        dismiss.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        dismissed.ShouldBe(0);
    }

    /// <summary>Verifies terminal leave cancels a held dismiss press without closing.</summary>
    [Fact]
    public async Task Pointer_WhenTerminalIsLeftDuringPress_CancelsInteractionAsync()
    {
        var bar = new InfoBar { Title = "Alert", Content = new ControlText("Body") };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(20, 5),
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel },
            TestContext.Current.CancellationToken);
        var dismiss = OwnedTree.Find<InfoBarDismissButton>(bar).ShouldNotBeNull();
        await surface.Pointer.MoveToAsync(dismiss);
        await surface.Pointer.PressAsync();

        await surface.Pointer.LeaveAsync();

        dismiss.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        bar.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies a failing body availability callback cannot strand the dismiss part after close.</summary>
    [Fact]
    public async Task Dismiss_WhenBodyAvailabilityObserverThrows_StillCleansDismissPartAsync()
    {
        var body = new Button("Retry");
        var bar = new InfoBar { Title = "Alert", Content = body };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Tab);
        var dismiss = surface.Application.Focus.Focused.ShouldBeOfType<InfoBarDismissButton>();
        var failure = new InvalidOperationException("body availability");
        var propertyPublished = false;
        var dismissed = false;
        body.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ControlBase.Visibility))
            {
                throw failure;
            }
        };
        bar.PropertyChanged += (_, eventArgs) => propertyPublished |=
            eventArgs.PropertyName == nameof(InfoBar.IsOpen);
        bar.Dismissed += (_, _) => dismissed = true;

        InvalidOperationException? exception = null;

        await surface.UpdateAsync(
            () => exception = Should.Throw<InvalidOperationException>(bar.Dismiss),
            "dismiss with failing body availability observer");

        exception.ShouldBeSameAs(failure);
        bar.IsOpen.ShouldBeFalse();
        dismiss.Visibility.ShouldBe(Visibility.Collapsed);
        propertyPublished.ShouldBeTrue();
        dismissed.ShouldBeTrue();
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies close cleanup completes before the open-state property publication.</summary>
    [Fact]
    public async Task Dismiss_WhenPressed_CleansFocusAndCaptureBeforePropertyChangedAsync()
    {
        var bar = new InfoBar { Title = "Alert", Content = new ControlText("Body") };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        var dismiss = OwnedTree.Find<InfoBarDismissButton>(bar).ShouldNotBeNull();
        await surface.Pointer.MoveToAsync(dismiss);
        await surface.Pointer.PressAsync();
        var observedClean = false;
        bar.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(InfoBar.IsOpen))
            {
                observedClean = !dismiss.IsPressed &&
                    surface.Application.Focus.Focused is null &&
                    surface.Application.Capture.Captured is null;
            }
        };

        await surface.UpdateAsync(bar.Dismiss, "dismiss held InfoBar");

        observedClean.ShouldBeTrue();
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveCapture(null);
        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies a retained body action remains ordinary and controls whether to dismiss.</summary>
    [Fact]
    public async Task BodyAction_WhenActivated_OnlyDismissesWhenItsHandlerRequestsItAsync()
    {
        var body = new Button("Retry");
        var activations = 0;
        body.Click += (_, _) => activations++;
        var bar = new InfoBar { Title = "Alert", Content = body };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(20, 7),
            TestContext.Current.CancellationToken);

        await surface.Pointer.ClickAsync(body);
        bar.IsOpen.ShouldBeTrue();
        body.Click += (_, _) => bar.Dismiss();
        await surface.Pointer.ClickAsync(body);

        activations.ShouldBe(2);
        bar.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies opening and closing never introduce modality or disturb unrelated focus.</summary>
    [Fact]
    public async Task IsOpen_WhenExternalControlHasFocus_PreservesOrdinaryFocusAndNavigationAsync()
    {
        var outside = new Button("Outside");
        var body = new Button("Retry");
        var bar = new InfoBar { Title = "Alert", Content = body, IsOpen = false };
        var root = new Stack { Children = { outside, bar } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(outside);

        await surface.UpdateAsync(() => bar.IsOpen = true, "open InfoBar beside focused control");
        surface.ShouldHaveFocus(outside);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(body);
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(outside).ShouldBeTrue(),
            "return focus outside InfoBar");

        await surface.UpdateAsync(() => bar.IsOpen = false, "close InfoBar beside focused control");

        surface.ShouldHaveFocus(outside);
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies all semantic presets paint their exact accent on border, adornment, title, and dismiss glyph.</summary>
    [Theory]
    [InlineData(SemanticColor.Info)]
    [InlineData(SemanticColor.Success)]
    [InlineData(SemanticColor.Warning)]
    [InlineData(SemanticColor.Error)]
    public async Task Render_WhenSemanticPresetIsUsed_PaintsExactAccentAsync(SemanticColor accent)
    {
        var style = accent == SemanticColor.Info
            ? InfoBarStyle.Info
            : accent == SemanticColor.Success
                ? InfoBarStyle.Success
                : accent == SemanticColor.Warning
                    ? InfoBarStyle.Warning
                    : InfoBarStyle.Error;
        var bar = new InfoBar
        {
            Title = "State",
            Adornment = new Affix("!"),
            Style = style,
            Content = new ControlText("Body")
        };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        var inner = bar.ActualStyle.Padding.Deflate(bar.ContentBounds);
        var expected = surface.Application.Theme.ResolveColor(accent);

        surface.Cell(new Point(bar.Bounds.X, bar.Bounds.Y)).Style.Foreground.ShouldBe(expected);
        surface.Cell(new Point(inner.X, inner.Y)).Style.Foreground.ShouldBe(expected);
        surface.Cell(new Point(inner.X + 2, inner.Y)).Style.Foreground.ShouldBe(expected);
        surface.Cell(new Point(inner.Right - 1, inner.Y)).Style.Foreground.ShouldBe(expected);
    }

    /// <summary>Verifies the complete box model places header, retained body, and dismiss part exactly.</summary>
    [Fact]
    public async Task Layout_WhenCompleteBarIsMounted_ArrangesExactGeometryAsync()
    {
        var body = new ProbeControl(new Size(6, 2));
        var bar = new InfoBar
        {
            Title = "Status",
            Adornment = new Affix("!"),
            Content = body,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(18, 8),
            TestContext.Current.CancellationToken);
        var dismiss = OwnedTree.Find<InfoBarDismissButton>(bar).ShouldNotBeNull();

        bar.Bounds.ShouldBe(new Rect(0, 0, 18, 8));
        bar.ContentBounds.ShouldBe(new Rect(1, 1, 16, 6));
        body.Bounds.ShouldBe(new Rect(2, 4, 14, 2));
        dismiss.Bounds.ShouldBe(new Rect(15, 2, 1, 1));
    }

    /// <summary>Verifies title, adornment, and body preserve complete Unicode clusters.</summary>
    [Fact]
    public async Task Render_WhenUnicodeClustersAreUsed_PreservesWholeClustersAsync()
    {
        var body = new ControlText("e\u0301 👩‍💻") { Overflow = Overflow.WrapAnywhere };
        var bar = new InfoBar
        {
            Title = "界",
            Adornment = new Affix("◆"),
            Content = body
        };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(18, 7),
            TestContext.Current.CancellationToken);
        var inner = bar.ActualStyle.Padding.Deflate(bar.ContentBounds);

        surface.Cell(new Point(inner.X, inner.Y)).Text.ShouldBe("◆");
        surface.Cell(new Point(inner.X + 2, inner.Y)).Text.ShouldBe("界");
        surface.Cell(new Point(inner.X + 3, inner.Y)).Continuation.ShouldBeTrue();
        surface.Cell(new Point(body.Bounds.X, body.Bounds.Y)).Text.ShouldBe("é");
        surface.Cell(new Point(body.Bounds.X + 2, body.Bounds.Y)).Text.ShouldBe("👩‍💻");
    }

    /// <summary>Verifies retained wrapping content remeasures at the width remaining inside chrome.</summary>
    [Fact]
    public async Task Layout_WhenBodyWraps_UsesAvailableInnerWidthAsync()
    {
        var body = new ControlText("one two three four") { Overflow = Overflow.Wrap };
        var bar = new InfoBar { Title = "Notice", Content = body };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(12, 8),
            TestContext.Current.CancellationToken);

        body.Bounds.Width.ShouldBe(8);
        body.Bounds.Height.ShouldBeGreaterThan(1);
        surface.Cell(new Point(body.Bounds.X, body.Bounds.Y + 1)).Text.ShouldNotBe(" ");
    }

    /// <summary>Verifies closing reclaims layout and reopening restores the same retained instances.</summary>
    [Fact]
    public async Task IsOpen_WhenToggled_ReclaimsAndRestoresLayoutWithSamePartsAsync()
    {
        var body = new Button("Retry");
        var bar = new InfoBar { Title = "Alert", Content = body };
        var root = new Stack { Children = { bar, new ControlText("After") } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 8),
            TestContext.Current.CancellationToken);
        var dismiss = OwnedTree.Find<InfoBarDismissButton>(bar).ShouldNotBeNull();
        var bodyBounds = body.Bounds;
        var dismissBounds = dismiss.Bounds;

        await surface.UpdateAsync(() => bar.IsOpen = false, "close InfoBar");
        bar.DesiredSize.ShouldBe(default);
        body.Bounds.ShouldBe(default);
        dismiss.Bounds.ShouldBe(default);
        await surface.UpdateAsync(() => bar.IsOpen = true, "reopen InfoBar");

        bar.Content.ShouldBeSameAs(body);
        OwnedTree.Find<InfoBarDismissButton>(bar).ShouldBeSameAs(dismiss);
        body.Bounds.ShouldBe(bodyBounds);
        dismiss.Bounds.ShouldBe(dismissBounds);
    }

    /// <summary>Verifies tiny geometry never emits a partial wide adornment over the dismiss cell.</summary>
    [Fact]
    public async Task Render_WhenWideAdornmentHasNoRoom_DropsItAndKeepsDismissGlyphWholeAsync()
    {
        var bar = new InfoBar
        {
            Adornment = new Affix("界", "#"),
            Content = new ControlText("B")
        };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(3, 3),
            TestContext.Current.CancellationToken);
        var inner = bar.ActualStyle.Padding.Deflate(bar.ContentBounds);

        if (inner.Width != 0 && inner.Height != 0)
        {
            var dismiss = surface.Cell(new Point(inner.Right - 1, inner.Y));
            dismiss.Text.ShouldBe("■");
            dismiss.Continuation.ShouldBeFalse();
        }
    }

    /// <summary>Verifies one usable inner cell is reserved for dismissal while the title and adornment drop.</summary>
    [Fact]
    public async Task Render_WhenOnlyOneInnerCellExists_KeepsDismissGlyphAsync()
    {
        var style = InfoBarStyle.Info with
        {
            Border = InfoBarStyle.Info.Border with { Sides = BorderSide.None },
            Padding = default,
            ContentGap = 0
        };
        var bar = new InfoBar
        {
            Title = "Long",
            Adornment = new Affix("界", "#"),
            Style = style
        };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(1, 1),
            TestContext.Current.CancellationToken);

        surface.ShouldRender("■");
        OwnedTree.Find<InfoBarDismissButton>(bar).ShouldNotBeNull().Bounds.ShouldBe(new Rect(0, 0, 1, 1));
    }

    /// <summary>Verifies a zero-sized slot exposes no cells, hit target, or dismiss geometry.</summary>
    [Fact]
    public void Layout_WhenSlotIsZero_HasNoGeometryOrHitTarget()
    {
        var bar = new InfoBar { Title = "Alert", Content = new ControlText("Body") };

        new LayoutEngine().Layout(bar, default);

        bar.Bounds.ShouldBe(default);
        bar.HitTest(default).ShouldBeNull();
        OwnedTree.Find<InfoBarDismissButton>(bar).ShouldNotBeNull().Bounds.ShouldBe(default);
    }

    /// <summary>Verifies fixed-seed title, adornment, body, and viewport permutations remain bounded.</summary>
    [Fact]
    public async Task Layout_WhenGeometryIsRandomized_RemainsWithinCommittedBoundsAsync()
    {
        var random = new Random(0x1F0BA2);
        var body = new ControlText("Body") { Overflow = Overflow.WrapAnywhere };
        var bar = new InfoBar
        {
            Title = "Title",
            Adornment = new Affix("界", "#"),
            Content = body,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(20, 8),
            TestContext.Current.CancellationToken);
        var dismiss = OwnedTree.Find<InfoBarDismissButton>(bar).ShouldNotBeNull();

        for (var iteration = 0; iteration < 32; iteration++)
        {
            var width = random.Next(1, 33);
            var height = random.Next(1, 13);
            var title = random.Next(3) switch
            {
                0 => null,
                1 => "T",
                2 => "Unicode 界 👩‍💻",
                _ => throw new UnreachableException()
            };
            Affix? adornment = random.Next(3) switch
            {
                0 => null,
                1 => new Affix("!"),
                2 => new Affix("界", "#"),
                _ => throw new UnreachableException()
            };
            var text = new string('x', random.Next(0, 49));
            await surface.UpdateAsync(
                () =>
                {
                    bar.Title = title;
                    bar.Adornment = adornment;
                    bar.IsDismissible = random.Next(2) == 0;
                    body.Content = text;
                },
                $"apply randomized InfoBar case {iteration}");
            await surface.ResizeAsync(new Size(width, height));

            bar.Bounds.Width.ShouldBe(width);
            bar.Bounds.Height.ShouldBe(height);
            body.Bounds.Width.ShouldBeGreaterThanOrEqualTo(0);
            body.Bounds.Height.ShouldBeGreaterThanOrEqualTo(0);
            body.Bounds.X.ShouldBeGreaterThanOrEqualTo(bar.ContentBounds.X);
            body.Bounds.Y.ShouldBeGreaterThanOrEqualTo(bar.ContentBounds.Y);
            body.Bounds.Right.ShouldBeLessThanOrEqualTo(bar.ContentBounds.Right);
            body.Bounds.Bottom.ShouldBeLessThanOrEqualTo(bar.ContentBounds.Bottom);
            dismiss.Bounds.Width.ShouldBeInRange(0, 1);
            dismiss.Bounds.Height.ShouldBeInRange(0, 1);

            if (dismiss.Bounds.Width != 0)
            {
                bar.ContentBounds.Contains(new Point(dismiss.Bounds.X, dismiss.Bounds.Y)).ShouldBeTrue();
                surface.Cell(new Point(dismiss.Bounds.X, dismiss.Bounds.Y)).Continuation.ShouldBeFalse();
            }
        }
    }
}
