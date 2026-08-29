// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies intrinsic border glyph families through mounted terminal surfaces.</summary>
public sealed class IntrinsicBorderSurfaceTests
{
    /// <summary>Verifies a focused Turbo Vision input shows its authored flat "focusedBorder"
    /// color on every edge instead of the passive Sunken bezel a Sunken/Raised relief would
    /// otherwise substitute - "input.focused.border.foreground": "focusedBorder" (brightCyan,
    /// `#55ffff`), restored to visibility by the border-relief-vs-authored-Foreground fix.</summary>
    [Fact]
    public async Task Render_WhenTurboVisionInputReceivesFocus_ShowsFlatActiveFrameAsync()
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
                surface.Application.Theme = ThemeCatalog.Load("turbo-vision");
                surface.Application.Focus.Focus(control).ShouldBeTrue();
            },
            "apply Turbo Vision and focus the input");

        var focusedBorder = surface.Application.Theme.ResolveColor(SemanticColor.FocusedBorder);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(focusedBorder);
        surface.Cell(new Point(5, 0)).Style.Foreground.ShouldBe(focusedBorder);
        surface.Cell(new Point(0, 1)).Style.Foreground.ShouldBe(focusedBorder);
        surface.Cell(new Point(5, 1)).Style.Foreground.ShouldBe(focusedBorder);
        surface.Cell(new Point(0, 2)).Style.Foreground.ShouldBe(focusedBorder);
        surface.Cell(new Point(5, 2)).Style.Foreground.ShouldBe(focusedBorder);
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
