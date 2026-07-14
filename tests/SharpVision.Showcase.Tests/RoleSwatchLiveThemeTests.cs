// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

using SharpVision.Runtime;
using SharpVision.Showcase.Controls;
using SharpVision.Styling;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;
using SharpVision.Terminal.Runtime;

using TerminalOptions = Terminal.Runtime.Options;

/// <summary>Verifies the Theming page's role swatches track the live application theme instead of a snapshot.</summary>
public sealed class RoleSwatchLiveThemeTests
{
    private const ColorRole ProbeRole = ColorRole.Accent;

    /// <summary>Verifies one role swatch paints the active theme's color and repaints the same instance after Application.Theme changes.</summary>
    [Fact]
    public async Task RoleSwatch_WhenApplicationThemeChanges_RepaintsSameInstanceWithNewColorAsync()
    {
        await using FakeTerminal terminal = new();
        Size size = new(120, 260);
        terminal.QueueResize(new Dimensions(size));
        using Gallery gallery = new();
        await using Application application = new(
            gallery,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        gallery.Attach(application);
        await application.StartAsync(TestContext.Current.CancellationToken);

        int themingIndex = gallery.Pages.ToList().FindIndex(static page => page == "Theming");
        themingIndex.ShouldBeGreaterThan(-1);

        await application.Dispatcher.InvokeAsync(
            () => gallery.Select(themingIndex),
            TestContext.Current.CancellationToken);

        RoleSwatch swatch = await WaitForLaidOutSwatchAsync(application, gallery);

        Themes.Dark.TryGetColor(ProbeRole, out Color expectedDark).ShouldBeTrue();
        Color initialCell = await RenderAndReadSwatchCellAsync(application, size, swatch);
        initialCell.ShouldBe(expectedDark);

        Theme dracula = ThemeCatalog.Default.Load("dracula");
        dracula.TryGetColor(ProbeRole, out Color expectedDracula).ShouldBeTrue();
        expectedDracula.ShouldNotBe(expectedDark);

        await application.Dispatcher.InvokeAsync(
            () => { application.Theme = dracula; },
            TestContext.Current.CancellationToken);

        await WaitUntilAsync(
            () => ReferenceEquals(application.Theme, dracula),
            application,
            "Dracula theme selection");

        Color updatedCell = await RenderAndReadSwatchCellAsync(application, size, swatch);
        updatedCell.ShouldBe(expectedDracula);

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<RoleSwatch> WaitForLaidOutSwatchAsync(Application application, Gallery gallery)
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            RoleSwatch? found = await application.Dispatcher.InvokeAsync(
                () => Find<RoleSwatch>(gallery.Content, static value => value.Role == ProbeRole && value.Bounds.Width > 0),
                TestContext.Current.CancellationToken);

            if (found is not null)
            {
                return found;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("Timed out waiting for the Accent role swatch to lay out.");
    }

    private static async Task<Color> RenderAndReadSwatchCellAsync(Application application, Size size, RoleSwatch swatch)
    {
        return await application.Dispatcher.InvokeAsync(
            () =>
            {
                using Frame frame = new(size);
                application.Root.Render(frame.Canvas);
                Rect bounds = swatch.Bounds;

                // The probe terminal is sized taller than the Theming page so the swatch never scrolls
                // out of view; this catches the assumption breaking instead of silently reading a stale cell.
                bounds.Bottom.ShouldBeLessThanOrEqualTo(size.Height);
                return frame.GetCell(new Point(bounds.X, bounds.Y)).Style.Background;
            },
            TestContext.Current.CancellationToken);
    }

    private static T? Find<T>(Control root, Func<T, bool> predicate)
        where T : Control
    {
        if (root is T match && predicate(match))
        {
            return match;
        }

        foreach (Control child in Visit(root))
        {
            if (child is T typed && predicate(typed))
            {
                return typed;
            }
        }

        return null;
    }

    private static IEnumerable<Control> Visit(Control control)
    {
        if (control is Container container)
        {
            foreach (Control child in container.Children)
            {
                yield return child;

                foreach (Control descendant in Visit(child))
                {
                    yield return descendant;
                }
            }
        }
    }

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        Application application,
        string description)
    {
        for (int attempt = 0; attempt < 200; attempt++)
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
