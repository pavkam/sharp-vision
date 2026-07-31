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
}
