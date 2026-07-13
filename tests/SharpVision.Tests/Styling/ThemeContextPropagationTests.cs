using SharpVision.Controls;
using SharpVision.Styling;
using SharpVision.Terminal.Protocols;
using SharpVision.Tests.Support;

using Shouldly;

namespace SharpVision.Tests.Styling;

/// <summary>Verifies theme-context propagation as controls attach and detach at runtime.</summary>
public sealed class ThemeContextPropagationTests
{
    private static Theme ForegroundTheme(int index)
    {
        var theme = new Theme();
        var style = new ControlStyle<Control>();
        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(index));
        theme.SetStyle(style);
        return theme;
    }

    /// <summary>Verifies a child added after the theme was applied still inherits it.</summary>
    [Fact]
    public void Add_WhenChildAttachedToThemedParent_InheritsThemeContext()
    {
        var root = new ProbeContainer();
        ThemeTestSupport.ApplyTheme(root, ForegroundTheme(3));

        var child = new ProbeControl();
        root.Children.Add(child);

        child.Foreground.ShouldBe(Color.Indexed(3));
    }

    /// <summary>Verifies a pre-built subtree inherits the theme when its root is attached.</summary>
    [Fact]
    public void Add_WhenSubtreeAttachedToThemedParent_InheritsThemeContext()
    {
        var root = new ProbeContainer();
        ThemeTestSupport.ApplyTheme(root, ForegroundTheme(5));
        var branch = new ProbeContainer();
        var leaf = new ProbeControl();
        branch.Children.Add(leaf);

        root.Children.Add(branch);

        leaf.Foreground.ShouldBe(Color.Indexed(5));
    }

    /// <summary>Verifies detaching a child clears the inherited theme context.</summary>
    [Fact]
    public void Remove_WhenChildDetached_ClearsInheritedThemeContext()
    {
        var root = new ProbeContainer();
        ThemeTestSupport.ApplyTheme(root, ForegroundTheme(3));
        var child = new ProbeControl();
        root.Children.Add(child);

        _ = root.Children.Remove(child);

        child.Foreground.ShouldBeNull();
    }
}
