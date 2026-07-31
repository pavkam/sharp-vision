// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>
/// Verifies every generated-scrollbar host reports an ActualScrollBarStyle that merges the active
/// theme, matching what ScrollBar.ResolveStyle (and therefore the generated bar itself) resolves,
/// instead of the code-owned static default (see #159).
/// </summary>
public sealed class ActualScrollBarStyleThemeTests
{
    /// <summary>Verifies each host resolves the active theme's Control profile when no local
    /// style is assigned, rather than the library's code-default appearance.</summary>
    [Fact]
    public void ActualScrollBarStyle_WhenThemedAndUnassigned_ResolvesTheActiveThemeNotTheCodeDefault()
    {
        var expected = ScrollBar.ResolveStyle(null, Themes.White);
        expected.Appearance.ShouldBe(Themes.White.Control);
        expected.Appearance.ShouldNotBe(ScrollBarStyle.Default.Appearance);

        var stack = new Stack();
        stack.SetTheme(Themes.White);
        stack.ActualScrollBarStyle.ShouldBe(expected);

        var textInput = new TextInput();
        textInput.SetTheme(Themes.White);
        textInput.ActualScrollBarStyle.ShouldBe(expected);

        var comboBox = new ComboBox();
        comboBox.SetTheme(Themes.White);
        comboBox.ActualScrollBarStyle.ShouldBe(expected);

        var listView = new ListView();
        listView.SetTheme(Themes.White);
        listView.ActualScrollBarStyle.ShouldBe(expected);

        var table = new Table();
        table.SetTheme(Themes.White);
        table.ActualScrollBarStyle.ShouldBe(expected);
    }

    /// <summary>Verifies TreeView and NavigationView, which delegate to a private Container part,
    /// pick up the same theme-resolved value without their own fix.</summary>
    [Fact]
    public void ActualScrollBarStyle_WhenThemedAndUnassigned_DelegatingHostsMatchToo()
    {
        var expected = ScrollBar.ResolveStyle(null, Themes.White);

        var tree = new TreeView();
        tree.SetTheme(Themes.White);
        tree.ActualScrollBarStyle.ShouldBe(expected);

        var navigation = new NavigationView();
        navigation.SetTheme(Themes.White);
        navigation.ActualScrollBarStyle.ShouldBe(expected);
    }

    /// <summary>Verifies a local assignment still takes precedence over the theme.</summary>
    [Fact]
    public void ActualScrollBarStyle_WhenLocallyAssigned_TakesPrecedenceOverTheTheme()
    {
        var stack = new Stack { ScrollBarStyle = ScrollBarStyle.ThinBlock };
        stack.SetTheme(Themes.White);

        stack.ActualScrollBarStyle.ShouldBe(ScrollBarStyle.ThinBlock);
    }

    /// <summary>Verifies a Theme swap with a differing scrollbar presentation invalidates a
    /// container's generated bars and publishes the host's theme-resolved style.</summary>
    [Fact]
    public void PropagateTheme_WhenScrollBarPresentationDiffers_InvalidatesContainerBarsAndPublishes()
    {
        var previousTheme = ThemeWithControl(Color.Rgb(1, 2, 3));
        var currentTheme = ThemeWithControl(Color.Rgb(4, 5, 6));
        var container = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.Always
        };
        container.Children.Add(new ProbeControl(new Size(20, 10)));
        container.PropagateTheme(previousTheme);
        new LayoutEngine().Layout(container, new Size(8, 4));
        var bars = OwnedTree.FindAll<ScrollBar>(container);
        bars.Count.ShouldBe(2);
        var notifications = new List<string?>();
        container.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        container.Clear(Invalidation.All);
        bars.ForEach(bar => bar.Clear(Invalidation.All));

        container.PropagateTheme(currentTheme);

        bars.ForEach(bar => bar.Pending.ShouldBe(Invalidation.Render));
        notifications.ShouldContain(nameof(Container.ActualScrollBarStyle));
        container.ActualScrollBarStyle.ShouldBe(ScrollBar.ResolveStyle(null, currentTheme));
        (container.Pending & Invalidation.Measure).ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies a Theme swap with a differing scrollbar presentation invalidates the
    /// editor bars and publishes TextInput's theme-resolved style.</summary>
    [Fact]
    public void PropagateTheme_WhenScrollBarPresentationDiffers_InvalidatesEditorBarsAndPublishes()
    {
        var previousTheme = ThemeWithControl(Color.Rgb(1, 2, 3));
        var currentTheme = ThemeWithControl(Color.Rgb(4, 5, 6));
        var control = new TextInput();
        control.PropagateTheme(previousTheme);
        var bars = OwnedTree.FindAll<ScrollBar>(control);
        bars.Count.ShouldBe(2);
        var notifications = new List<string?>();
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        control.Clear(Invalidation.All);
        bars.ForEach(bar => bar.Clear(Invalidation.All));

        control.PropagateTheme(currentTheme);

        bars.ForEach(bar => bar.Pending.ShouldBe(Invalidation.Render));
        notifications.ShouldContain(nameof(TextInput.ActualScrollBarStyle));
        control.ActualScrollBarStyle.ShouldBe(ScrollBar.ResolveStyle(null, currentTheme));
        (control.Pending & Invalidation.Measure).ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies a Theme swap never publishes ActualScrollBarStyle while a local complete
    /// style owns the resolved value.</summary>
    [Fact]
    public void PropagateTheme_WhenLocalStyleIsAssigned_DoesNotPublishActualScrollBarStyle()
    {
        var previousTheme = ThemeWithControl(Color.Rgb(1, 2, 3));
        var currentTheme = ThemeWithControl(Color.Rgb(4, 5, 6));
        var container = new Stack { ScrollBarStyle = ScrollBarStyle.ThinBlock };
        var editor = new TextInput { ScrollBarStyle = ScrollBarStyle.ThinBlock };
        container.PropagateTheme(previousTheme);
        editor.PropagateTheme(previousTheme);
        var notifications = new List<string?>();
        container.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        editor.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        container.PropagateTheme(currentTheme);
        editor.PropagateTheme(currentTheme);

        notifications.ShouldNotContain(nameof(Container.ActualScrollBarStyle));
        container.ActualScrollBarStyle.ShouldBe(ScrollBarStyle.ThinBlock);
        editor.ActualScrollBarStyle.ShouldBe(ScrollBarStyle.ThinBlock);
    }

    private static Theme ThemeWithControl(Color foreground)
    {
        var theme = new Theme();
        var appearance = Theme.CreateDefaultAppearance(ThemeRole.Control);
        theme.SetProfiles(
            new ThemeProfile(
                new ThemeAppearance(
                    new Face(
                        foreground,
                        appearance.Face.Background,
                        appearance.Face.Attributes,
                        appearance.Face.Underline,
                        appearance.Face.UnderlineColor),
                    appearance.Border,
                    appearance.Shadow)),
            theme.Input,
            theme.Container,
            theme.Window,
            theme.Popup);
        theme.Freeze();
        return theme;
    }
}
