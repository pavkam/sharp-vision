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
    /// <summary>Verifies each composite starts unassigned and resolves to its documented default.</summary>
    [Fact]
    public void ActualScrollBarStyle_WhenUnassigned_ResolvesTheControlDefault()
    {
        var tree = new TreeView();
        var navigation = new NavigationView();

        tree.ScrollBarStyle.ShouldBeNull();
        tree.ActualScrollBarStyle.ShouldBe(ScrollBarStyle.ThinBlock);
        navigation.ScrollBarStyle.ShouldBeNull();
        navigation.ActualScrollBarStyle.ShouldBe(ScrollBarStyle.ThinLine);
    }

    /// <summary>Verifies a local assignment resolves and publishes both surfaces.</summary>
    [Fact]
    public void ScrollBarStyle_WhenAssigned_ResolvesAndNotifies()
    {
        var tree = new TreeView();
        List<string?> changed = [];
        tree.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        tree.ScrollBarStyle = ScrollBarStyle.Default;

        tree.ScrollBarStyle.ShouldBe(ScrollBarStyle.Default);
        tree.ActualScrollBarStyle.ShouldBe(ScrollBarStyle.Default);
        changed.ShouldContain(nameof(TreeView.ScrollBarStyle));
        changed.ShouldContain(nameof(TreeView.ActualScrollBarStyle));
    }

    /// <summary>Verifies clearing the local value returns the bar to the control default.</summary>
    [Fact]
    public void ScrollBarStyle_WhenReset_ReturnsToTheControlDefault()
    {
        var navigation = new NavigationView { ScrollBarStyle = ScrollBarStyle.Default };

        navigation.ScrollBarStyle = null;

        navigation.ScrollBarStyle.ShouldBeNull();
        navigation.ActualScrollBarStyle.ShouldBe(ScrollBarStyle.ThinLine);
    }

    /// <summary>Verifies assigning the same value publishes nothing.</summary>
    [Fact]
    public void ScrollBarStyle_WhenUnchanged_RaisesNothing()
    {
        var tree = new TreeView { ScrollBarStyle = ScrollBarStyle.Default };
        List<string?> changed = [];
        tree.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        tree.ScrollBarStyle = ScrollBarStyle.Default;

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

        tree.ScrollBarStyle = ScrollBarStyle.Default;
        navigation.ScrollBarStyle = ScrollBarStyle.ThinBlock;

        GeneratedStyle(tree).ShouldBe(ScrollBarStyle.Default);
        GeneratedStyle(navigation).ShouldBe(ScrollBarStyle.ThinBlock);

        tree.ScrollBarStyle = null;

        GeneratedStyle(tree).ShouldBe(ScrollBarStyle.ThinBlock);
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
