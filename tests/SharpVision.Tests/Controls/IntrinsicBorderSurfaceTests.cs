// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies intrinsic border glyph families through mounted terminal surfaces.</summary>
public sealed class IntrinsicBorderSurfaceTests
{
    /// <summary>Verifies a focused framed input shows its authored flat "focusedBorder" color on
    /// every edge. Nord frames its input line the way most bundled themes do (Turbo Vision no
    /// longer does - Borland's <c>TInputLine</c> is a bare strip, so that theme is not a framed
    /// specimen any more); no bundled "input" section authors a per-state Relief, so this test
    /// alone does not exercise the border-relief-vs-authored-Foreground bypass - <see
    /// cref="Render_WhenTurboVisionTreeViewReceivesFocus_ShowsFlatActiveFrameAsync"/> is the
    /// bypass's only remaining observable-effect coverage, via "container.normal.border.relief":
    /// "sunken", the sole non-Flat relief left in any bundled theme.</summary>
    [Fact]
    public async Task Render_WhenNordInputReceivesFocus_ShowsFlatActiveFrameAsync()
    {
        var control = new TextInput
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor }
        };
        await using var surface = await ComponentSurface.MountAsync(
            control,
            new Size(6, 3),
            options,
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () =>
            {
                surface.Application.Theme = ThemeCatalog.Load("nord");
                surface.Application.Focus.Focus(control).ShouldBeTrue();
            },
            "apply Nord and focus the input");

        var focusedBorder = surface.Application.Theme.ResolveColor(SemanticColor.FocusedBorder);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(focusedBorder);
        surface.Cell(new Point(5, 0)).Style.Foreground.ShouldBe(focusedBorder);
        surface.Cell(new Point(0, 1)).Style.Foreground.ShouldBe(focusedBorder);
        surface.Cell(new Point(5, 1)).Style.Foreground.ShouldBe(focusedBorder);
        surface.Cell(new Point(0, 2)).Style.Foreground.ShouldBe(focusedBorder);
        surface.Cell(new Point(5, 2)).Style.Foreground.ShouldBe(focusedBorder);
    }

    /// <summary>Verifies a focused Turbo Vision TreeView shows the authored flat "focusedBorder"
    /// color on every edge instead of the Sunken shade/highlight split its own geometry - inherited
    /// from "container.normal.border.relief": "sunken", the only non-Flat relief left in any
    /// bundled theme - would otherwise substitute. <see cref="Theme.GetFocusableContainerStyleSet"/>
    /// rebases Input's Focused "border.foreground": "focusedBorder" delta onto Container's Normal
    /// geometry (Sunken relief included), so this exercises the border-relief-vs-authored-Foreground
    /// bypass at the one bundled-theme site where it is still observable.</summary>
    [Fact]
    public async Task Render_WhenTurboVisionTreeViewReceivesFocus_ShowsFlatActiveFrameAsync()
    {
        var control = new TreeView
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Items = { new TreeViewItem { Header = "Node" } }
        };
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor }
        };
        await using var surface = await ComponentSurface.MountAsync(
            control,
            new Size(10, 3),
            options,
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () =>
            {
                surface.Application.Theme = ThemeCatalog.Load("turbo-vision");
                surface.Application.Focus.Focus(control).ShouldBeTrue();
            },
            "apply Turbo Vision and focus the tree view");

        var focusedBorder = surface.Application.Theme.ResolveColor(SemanticColor.FocusedBorder);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(focusedBorder);
        surface.Cell(new Point(9, 0)).Style.Foreground.ShouldBe(focusedBorder);
        surface.Cell(new Point(0, 1)).Style.Foreground.ShouldBe(focusedBorder);
        surface.Cell(new Point(9, 1)).Style.Foreground.ShouldBe(focusedBorder);
        surface.Cell(new Point(0, 2)).Style.Foreground.ShouldBe(focusedBorder);
        surface.Cell(new Point(9, 2)).Style.Foreground.ShouldBe(focusedBorder);
    }

    /// <summary>Verifies the Turbo Vision container role renders its exact sunken edge colors.</summary>
    [Fact]
    public async Task Render_WhenTurboVisionContainerIsMounted_DrawsSunkenFrameAsync()
    {
        var control = new GroupBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor }
        };
        await using var surface = await ComponentSurface.MountAsync(
            control,
            new Size(6, 3),
            options,
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () => surface.Application.Theme = ThemeCatalog.Load("turbo-vision"),
            "apply the Turbo Vision theme");

        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(Color.FromHex("#000000"));
        surface.Cell(new Point(5, 0)).Style.Foreground.ShouldBe(Color.FromHex("#000000"));
        surface.Cell(new Point(0, 1)).Style.Foreground.ShouldBe(Color.FromHex("#000000"));
        surface.Cell(new Point(5, 1)).Style.Foreground.ShouldBe(Color.FromHex("#ffffff"));
        surface.Cell(new Point(0, 2)).Style.Foreground.ShouldBe(Color.FromHex("#ffffff"));
        surface.Cell(new Point(5, 2)).Style.Foreground.ShouldBe(Color.FromHex("#ffffff"));
    }

    /// <summary>Verifies the half-block preset draws every physical edge and corner exactly.</summary>
    [Fact]
    public async Task Render_WhenHalfBlockBorderIsMounted_DrawsSculptedFrameAsync()
    {
        // Arrange
        var control = new Dock
        {
            Border = AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.HalfBlock),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { new ControlText("Half") }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            control,
            new Size(7, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                             ▛▀▀▀▀▀▜
                             ▌Half ▐
                             ▙▄▄▄▄▄▟
                             """);
    }
}
