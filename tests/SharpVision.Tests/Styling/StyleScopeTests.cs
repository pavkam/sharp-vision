namespace SharpVision.Tests.Styling;

using SharpVision.Controls;
using SharpVision.Styling;
using SharpVision.Terminal.Protocols;
using SharpVision.Tests.Support;
using SharpVision.Threading;

using Shouldly;

/// <summary>Verifies generic ancestor style-scope cascade and its cache invalidation.</summary>
public sealed class StyleScopeTests
{
    private static ControlStyle<Control> Foreground(int index)
    {
        var style = new ControlStyle<Control>();
        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(index));
        return style;
    }

    /// <summary>Verifies a descendant inherits a non-list ancestor scope's per-instance style.</summary>
    [Fact]
    public void Resolve_WhenAncestorIsStyleScope_DescendantInheritsScopeInstanceStyle()
    {
        var scope = new ProbeScope { Style = Foreground(7) };
        var child = new ProbeControl();
        scope.Children.Add(child);

        child.Foreground.ShouldBe(Color.Indexed(7));
    }

    /// <summary>Verifies the nearest scope wins over a farther scope for the same property.</summary>
    [Fact]
    public void Resolve_WhenNestedScopes_NearestScopeWins()
    {
        var outer = new ProbeScope { Style = Foreground(1) };
        var inner = new ProbeScope { Style = Foreground(2) };
        var child = new ProbeControl();
        outer.Children.Add(inner);
        inner.Children.Add(child);

        child.Foreground.ShouldBe(Color.Indexed(2));
    }

    /// <summary>Verifies a descendant's own value still wins over an ancestor scope.</summary>
    [Fact]
    public void Resolve_WhenDescendantHasLocalValue_WinsOverScope()
    {
        var scope = new ProbeScope { Style = Foreground(1) };
        var child = new ProbeControl { Foreground = Color.Indexed(9) };
        scope.Children.Add(child);

        child.Foreground.ShouldBe(Color.Indexed(9));
    }

    /// <summary>Verifies reparenting to a different scope updates the resolved value.</summary>
    [Fact]
    public void Resolve_WhenReparentedToDifferentScope_UpdatesResolvedValue()
    {
        var scopeA = new ProbeScope { Style = Foreground(1) };
        var scopeB = new ProbeScope { Style = Foreground(2) };
        var child = new ProbeControl();
        scopeA.Children.Add(child);
        child.Foreground.ShouldBe(Color.Indexed(1));

        _ = scopeA.Children.Remove(child);
        scopeB.Children.Add(child);

        child.Foreground.ShouldBe(Color.Indexed(2));
    }

    /// <summary>Verifies mutating an attached scope's style invalidates descendant resolution.</summary>
    [Fact]
    public async Task Resolve_WhenScopeStyleMutates_InvalidatesDescendantAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(
            () =>
            {
                var style = Foreground(1);
                var scope = new ProbeScope { Style = style };
                var child = new ProbeControl();
                scope.Children.Add(child);
                scope.Attach(dispatcher);
                child.Foreground.ShouldBe(Color.Indexed(1));

                style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(2));

                child.Foreground.ShouldBe(Color.Indexed(2));
            },
            TestContext.Current.CancellationToken);
    }
}
