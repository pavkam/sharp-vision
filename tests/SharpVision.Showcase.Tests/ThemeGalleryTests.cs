// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

using SharpVision.Controls;
using SharpVision.Runtime;
using SharpVision.Showcase.Panes;
using SharpVision.Styling;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Runtime;

using Shouldly;

using ControlText = Controls.Text;
using TerminalOptions = Terminal.Runtime.Options;

/// <summary>Verifies application theme switching through the running showcase gallery.</summary>
public sealed class ThemeGalleryTests
{
    /// <summary>Verifies the gallery publishes a new theme snapshot when Light is activated.</summary>
    [Fact]
    public async Task Theme_WhenLightIsSelected_PublishesWhiteThemeAsync()
    {
        await using FakeTerminal terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(100, 30)));
        using Gallery gallery = new Gallery();
        await using Application application = new Application(
            gallery,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        gallery.Attach(application);
        await application.StartAsync(TestContext.Current.CancellationToken);

        Button? light = await application.Dispatcher.InvokeAsync(
            () => Find<Button>(gallery.Sidebar, static value => value.Content is ControlText { Content: "Light" }),
            TestContext.Current.CancellationToken);
        Button button = light.ShouldNotBeNull();

        await application.Dispatcher.InvokeAsync(
            button.PerformClick,
            TestContext.Current.CancellationToken);

        await WaitUntilAsync(
            () => ReferenceEquals(application.Theme, Themes.White),
            application,
            "Light theme selection");

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies selecting the Theming page exposes the third-party showcase panel specimen.</summary>
    [Fact]
    public async Task Navigation_WhenThemingPageIsSelected_ShowsShowcasePanelAsync()
    {
        await using FakeTerminal terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(100, 30)));
        using Gallery gallery = new Gallery();
        await using Application application = new Application(
            gallery,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        gallery.Attach(application);
        await application.StartAsync(TestContext.Current.CancellationToken);

        var themingIndex = gallery.Pages.ToList().FindIndex(static page => page.Name == "Theming");
        themingIndex.ShouldBeGreaterThan(-1);

        await application.Dispatcher.InvokeAsync(
            () => gallery.Select(themingIndex),
            TestContext.Current.CancellationToken);

        await WaitUntilAsync(
            () => Find<ShowcasePanel>(gallery.Content, static _ => true) is not null,
            application,
            "Theming page content");

        await application.StopAsync(TestContext.Current.CancellationToken);
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
