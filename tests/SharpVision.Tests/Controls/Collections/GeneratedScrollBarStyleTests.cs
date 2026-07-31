// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

using System.Reflection;

/// <summary>
/// Verifies the scrolling composites publish and forward the style of the scrollbar they generate.
/// </summary>
/// <remarks>
/// Both hosts retain a private scrolling stack, so without these proxies a consumer cannot reach
/// the generated bar at all and the concrete default stays pinned forever.
/// </remarks>
public sealed class GeneratedScrollBarStyleTests
{
    /// <summary>
    /// Verifies neither composite pins a style onto the bar it owns, so an unassigned control
    /// resolves the active theme (matching what the generated bar itself renders) and leaves the
    /// theming slot free (see #159).
    /// </summary>
    [Fact]
    public void ActualScrollBarStyle_WhenUnassigned_LeavesTheBarUnpinned()
    {
        var tree = new TreeView();
        var navigation = new NavigationView();

        tree.ScrollBarStyle.ShouldBeNull();
        navigation.ScrollBarStyle.ShouldBeNull();
        GeneratedStyle(tree).ShouldBeNull();
        GeneratedStyle(navigation).ShouldBeNull();
        tree.ActualScrollBarStyle.ShouldBe(ScrollBar.ResolveStyle(null, tree.Theme));
        navigation.ActualScrollBarStyle.ShouldBe(ScrollBar.ResolveStyle(null, navigation.Theme));
    }

    /// <summary>Verifies a local assignment resolves and publishes both surfaces.</summary>
    [Fact]
    public void ScrollBarStyle_WhenAssigned_ResolvesAndNotifies()
    {
        var tree = new TreeView();
        List<string?> changed = [];
        tree.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        tree.ScrollBarStyle = ScrollBarStyle.ThinBlock;

        tree.ScrollBarStyle.ShouldBe(ScrollBarStyle.ThinBlock);
        tree.ActualScrollBarStyle.ShouldBe(ScrollBarStyle.ThinBlock);
        changed.ShouldContain(nameof(TreeView.ScrollBarStyle));
        changed.ShouldContain(nameof(TreeView.ActualScrollBarStyle));
    }

    /// <summary>Verifies clearing the local value releases the bar back to the Theme.</summary>
    [Fact]
    public void ScrollBarStyle_WhenReset_ReleasesTheBar()
    {
        var navigation = new NavigationView { ScrollBarStyle = ScrollBarStyle.ThinLine };

        navigation.ScrollBarStyle = null;

        navigation.ScrollBarStyle.ShouldBeNull();
        GeneratedStyle(navigation).ShouldBeNull();
        navigation.ActualScrollBarStyle.ShouldBe(ScrollBar.ResolveStyle(null, navigation.Theme));
    }

    /// <summary>Verifies assigning the same value publishes nothing.</summary>
    [Fact]
    public void ScrollBarStyle_WhenUnchanged_RaisesNothing()
    {
        var tree = new TreeView { ScrollBarStyle = ScrollBarStyle.ThinBlock };
        List<string?> changed = [];
        tree.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        tree.ScrollBarStyle = ScrollBarStyle.ThinBlock;

        changed.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies the resolved style actually reaches the generated bar rather than only being
    /// reported by the composite.
    /// </summary>
    [Fact]
    public void ScrollBarStyle_WhenAssigned_ForwardsToTheGeneratedBar()
    {
        var tree = new TreeView();
        var navigation = new NavigationView();

        tree.ScrollBarStyle = ScrollBarStyle.ThinBlock;
        navigation.ScrollBarStyle = ScrollBarStyle.ThinLine;

        GeneratedStyle(tree).ShouldBe(ScrollBarStyle.ThinBlock);
        GeneratedStyle(navigation).ShouldBe(ScrollBarStyle.ThinLine);

        tree.ScrollBarStyle = null;

        GeneratedStyle(tree).ShouldBeNull();
    }

    private static ScrollBarStyle? GeneratedStyle(Control composite)
    {
        // The scrolling stack is a private retained part on purpose, so the only way to prove the
        // proxy reaches it is to read it back directly.
        var stack = (Container) composite
            .GetType()
            .GetField("_itemsStack", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(composite)!;

        return stack.ScrollBarStyle;
    }
}
