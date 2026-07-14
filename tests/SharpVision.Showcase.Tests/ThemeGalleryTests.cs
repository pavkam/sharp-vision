// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

using SharpVision.Runtime;
using SharpVision.Showcase.Panes;
using SharpVision.Styling;
using SharpVision.Terminal.Runtime;

using TerminalOptions = Terminal.Runtime.Options;

/// <summary>Verifies application theme switching through the running showcase gallery.</summary>
public sealed class ThemeGalleryTests
{
    /// <summary>Verifies the gallery publishes a new theme snapshot when Light is activated.</summary>
    [Fact]
    public async Task Theme_WhenLightIsSelected_PublishesWhiteThemeAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(100, 30)));
        using Gallery gallery = new();
        await using Application application = new(
            gallery,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        gallery.Attach(application);
        await application.StartAsync(TestContext.Current.CancellationToken);

        ComboBox? picker = await application.Dispatcher.InvokeAsync(
            () => Find<ComboBox>(gallery.Sidebar, static _ => true),
            TestContext.Current.CancellationToken);
        ComboBox themePicker = picker.ShouldNotBeNull();

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                int light = themePicker.Items.ToList().IndexOf("Light");
                light.ShouldBeGreaterThanOrEqualTo(0);
                themePicker.SelectedIndex = light;
            },
            TestContext.Current.CancellationToken);

        await WaitUntilAsync(
            () => ReferenceEquals(application.Theme, Themes.White),
            application,
            "Light theme selection");

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the gallery publishes a curated catalog theme when it is selected by name.</summary>
    [Fact]
    public async Task Theme_WhenDraculaIsSelected_PublishesCatalogThemeAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(100, 30)));
        using Gallery gallery = new();
        await using Application application = new(
            gallery,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        gallery.Attach(application);
        await application.StartAsync(TestContext.Current.CancellationToken);

        ComboBox? picker = await application.Dispatcher.InvokeAsync(
            () => Find<ComboBox>(gallery.Sidebar, static _ => true),
            TestContext.Current.CancellationToken);
        ComboBox themePicker = picker.ShouldNotBeNull();

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                int dracula = themePicker.Items.ToList().IndexOf("Dracula");
                dracula.ShouldBeGreaterThanOrEqualTo(0);
                themePicker.SelectedIndex = dracula;
            },
            TestContext.Current.CancellationToken);

        Theme expected = ThemeCatalog.Default.Load("dracula");

        await WaitUntilAsync(
            () => ReferenceEquals(application.Theme, expected),
            application,
            "Dracula theme selection");

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the picker lists the full embedded catalog with every dark theme before any light theme.</summary>
    [Fact]
    public async Task ThemePicker_WhenGalleryStarts_ListsFullCatalogDarkGroupFirstAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(100, 30)));
        using Gallery gallery = new();
        await using Application application = new(
            gallery,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        gallery.Attach(application);
        await application.StartAsync(TestContext.Current.CancellationToken);

        ComboBox? picker = await application.Dispatcher.InvokeAsync(
            () => Find<ComboBox>(gallery.Sidebar, static _ => true),
            TestContext.Current.CancellationToken);
        ComboBox themePicker = picker.ShouldNotBeNull();

        IReadOnlyList<ThemeCatalogEntry> entries = ThemeCatalog.Default.Entries;
        Dictionary<string, ColorScheme> schemesByName = new(StringComparer.Ordinal);
        foreach (ThemeCatalogEntry entry in entries)
        {
            schemesByName[entry.Name] = entry.ColorScheme;
        }

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                themePicker.Items.Count.ShouldBe(entries.Count);

                bool sawLight = false;
                foreach (object? item in themePicker.Items)
                {
                    string name = item.ShouldBeOfType<string>();
                    schemesByName.ShouldContainKey(name);

                    if (schemesByName[name] == ColorScheme.Light)
                    {
                        sawLight = true;
                    }
                    else
                    {
                        sawLight.ShouldBeFalse($"Dark theme '{name}' appeared after a light theme.");
                    }
                }
            },
            TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies selecting the Theming page exposes the third-party showcase panel specimen.</summary>
    [Fact]
    public async Task Navigation_WhenThemingPageIsSelected_ShowsShowcasePanelAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(100, 30)));
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
