// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;



/// <summary>Verifies application theme switching through the running showcase gallery.</summary>
public sealed class ThemeGalleryTests
{
    /// <summary>Verifies the theming page includes controls whose drawing glyphs have no local overrides.</summary>
    [Fact]
    public void ThemingPane_WhenBuilt_DemonstratesThemeOwnedGlyphs()
    {
        using var page = new ThemingPane();

        var progress = Find<ProgressBar>(page, static _ => true).ShouldNotBeNull();
        var comboBox = Find<ComboBox>(page, static value => value.Items.Contains("Theme glyphs")).ShouldNotBeNull();
        var expander = Find<Expander>(page, static value => value.Header == "Disclosure").ShouldNotBeNull();

        progress.FillGlyph.ShouldBe(Themes.Dark.Glyphs.Progress.Full.Value);
        comboBox.DropDownGlyph.ShouldBe(Themes.Dark.Glyphs.Disclosure.DropDown.Value);
        expander.CollapsedGlyph.ShouldBe(Themes.Dark.Glyphs.Disclosure.Collapsed.Value);
    }

    /// <summary>Verifies the type-style specimen compares a baseline with concise semantic styling.</summary>
    [Fact]
    public void ThemingPane_WhenTypeStyleBuilds_UsesSemanticPreviewWithoutRawRecords()
    {
        using var page = new ThemingPane();
        new Engine().Layout(page, new Size(100, 140));
        var baseline = Find<Button>(page, static value =>
            value.Content is ControlText { Content: "Baseline theme" }).ShouldNotBeNull();
        var semantic = Find<Button>(page, static value =>
            value.Content is ControlText { Content: "Semantic accent" }).ShouldNotBeNull();
        var content = ControlTree.Text(page);

        baseline.HasShadow.ShouldBeFalse();
        semantic.HasShadow.ShouldBeFalse();
        content.ShouldContain("Background: Accent · Border: Heavy · Shadow: Off");
        content.ShouldNotContain("Glyphs {");
        content.ShouldNotContain("ThemeResolver.Resolve(theme");
    }

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

        var picker = await application.Dispatcher.InvokeAsync(
            () => Find<ComboBox>(gallery.Sidebar, static _ => true),
            TestContext.Current.CancellationToken);
        var themePicker = picker.ShouldNotBeNull();

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                var light = themePicker.Items.ToList().IndexOf("Light");
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

        var picker = await application.Dispatcher.InvokeAsync(
            () => Find<ComboBox>(gallery.Sidebar, static _ => true),
            TestContext.Current.CancellationToken);
        var themePicker = picker.ShouldNotBeNull();

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                var dracula = themePicker.Items.ToList().IndexOf("Dracula");
                dracula.ShouldBeGreaterThanOrEqualTo(0);
                themePicker.SelectedIndex = dracula;
            },
            TestContext.Current.CancellationToken);

        var expected = ThemeCatalog.Default.Load("dracula");

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

        var picker = await application.Dispatcher.InvokeAsync(
            () => Find<ComboBox>(gallery.Sidebar, static _ => true),
            TestContext.Current.CancellationToken);
        var themePicker = picker.ShouldNotBeNull();

        var entries = ThemeCatalog.Default.Entries;
        var schemesByName = new Dictionary<string, ColorScheme>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            schemesByName[entry.Name] = entry.ColorScheme;
        }

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                themePicker.Items.Count.ShouldBe(entries.Count);

                var sawLight = false;
                foreach (var item in themePicker.Items)
                {
                    var name = item.ShouldBeOfType<string>();
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

        var themingIndex = gallery.Pages.ToList().FindIndex(static page => page == "Theming");
        themingIndex.ShouldBeGreaterThan(-1);

        await application.Dispatcher.InvokeAsync(
            () => gallery.Select(themingIndex),
            TestContext.Current.CancellationToken);

        await WaitUntilAsync(
            () => Find<ShowcasePanel>(gallery.CurrentPage, static _ => true) is not null,
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

        foreach (var child in Visit(root))
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
        var count = control.OwnedControlCount;

        for (var index = 0; index < count; index++)
        {
            var child = control.OwnedControlAt(index);
            yield return child;

            foreach (var descendant in Visit(child))
            {
                yield return descendant;
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
