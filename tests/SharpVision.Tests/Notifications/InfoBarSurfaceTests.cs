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
            new Size(20, 5),
            TestContext.Current.CancellationToken);

        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(body);
        await surface.Keyboard.PressAsync(Code.Tab);
        _ = surface.Application.Focus.Focused.ShouldBeOfType<InfoBarDismissButton>();

        await surface.Keyboard.PressAsync(Code.Enter);

        bar.IsOpen.ShouldBeFalse();
        surface.ShouldHaveFocus(null);
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
}
