// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;



/// <summary>
/// Verifies the v2 role-color swatch mechanism (a plain control with <c>Background</c> set to a
/// <see cref="ThemeColors"/> value, as used by the Theming page) tracks the live application theme
/// instead of a snapshot, with no custom theme-reading control involved.
/// </summary>
public sealed class ThemeSwatchLiveThemeTests
{
    /// <summary>
    /// Verifies an intrinsic surface with <c>Background = ThemeColors.Accent</c> paints the active theme's
    /// accent color and repaints the same instance with the new color after <c>Application.Theme</c>
    /// changes, proving resolution alone (no <c>OnRender</c> theme lookup) keeps it live.
    /// </summary>
    [Fact]
    public async Task Chip_WhenApplicationThemeChanges_RepaintsSameInstanceWithNewColorAsync()
    {
        await using FakeTerminal terminal = new();
        var size = new Size(20, 4);
        terminal.QueueResize(new Dimensions(size));

        // The same kind of intrinsic surface ThemingPane.BuildRoleSwatches builds: a Dock whose Background
        // is a deferred role color. It never reads ThemeContext itself; resolution paints it live.
        var chip = new Dock()
        {
            Width = Length.Cells(6),
            Height = Length.Cells(1),
            FillMode = FillMode.Opaque,
            Background = ThemeColors.Accent,
        };

        await using Application application = new(
            chip,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        Themes.Dark.TryGetColor(ColorRole.Accent, out var expectedDark).ShouldBeTrue();
        var initialCell = await RenderAndReadChipCellAsync(application, size, chip);
        initialCell.ShouldBe(expectedDark);

        var dracula = ThemeCatalog.Default.Load("dracula");
        dracula.TryGetColor(ColorRole.Accent, out var expectedDracula).ShouldBeTrue();
        expectedDracula.ShouldNotBe(expectedDark);

        await application.Dispatcher.InvokeAsync(
            () => { application.Theme = dracula; },
            TestContext.Current.CancellationToken);

        await WaitUntilAsync(
            () => ReferenceEquals(application.Theme, dracula),
            application,
            "Dracula theme selection");

        var updatedCell = await RenderAndReadChipCellAsync(application, size, chip);
        updatedCell.ShouldBe(expectedDracula);

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Color> RenderAndReadChipCellAsync(Application application, Size size, Dock chip)
    {
        return await application.Dispatcher.InvokeAsync(
            () =>
            {
                using Frame frame = new(size);
                application.Root.Render(frame.Canvas);
                var bounds = chip.Bounds;

                // The terminal is sized to cover the chip's fixed footprint so this never reads a
                // clipped or unlaid-out cell; that assumption breaks loudly instead of silently passing.
                bounds.Bottom.ShouldBeLessThanOrEqualTo(size.Height);
                bounds.Right.ShouldBeLessThanOrEqualTo(size.Width);
                return frame.GetCell(new Point(bounds.X, bounds.Y)).Style.Background;
            },
            TestContext.Current.CancellationToken);
    }

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        Application application,
        string description)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (await application.Dispatcher.InvokeAsync(predicate, TestContext.Current.CancellationToken))
            {
                return;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Timed out waiting for {description}.");
    }
}
