using System.Text;

using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Styling;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;
using SharpVision.Tests.Support;
using SharpVision.Threading;

using Shouldly;

using UiStyle = SharpVision.Styling.Style;

namespace SharpVision.Tests.Styling;

/// <summary>Verifies mutable resources, state precedence, inheritance, and cells.</summary>
public sealed class StyleTests
{
    /// <summary>Verifies explicit style-event construction rejects unknown state and impact values.</summary>
    [Fact]
    public void Constructor_WhenStyleChangeIsInvalid_ThrowsDocumentedException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new ChangedEventArgs((State) int.MaxValue, Impact.Render));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new ChangedEventArgs(State.Normal, (Impact) int.MaxValue));
    }

    /// <summary>Verifies every state pair resolves by documented field precedence.</summary>
    [Fact]
    public void Resolve_WhenStatesConflict_UsesPairwisePrecedence()
    {
        var style = CreatePrecedenceStyle();
        var states = new[]
        {
            State.Hovered,
            State.Focused,
            State.Checked,
            State.Pressed,
            State.Disabled,
        };

        Resolver.Resolve(style, State.Normal).Foreground.ShouldBe(Color.Indexed(1));

        for (var lower = 0; lower < states.Length; lower++)
        {
            for (var higher = lower + 1; higher < states.Length; higher++)
            {
                var resolved = Resolver.Resolve(style, states[lower] | states[higher]);
                resolved.Foreground.ShouldBe(Color.Indexed(higher + 2));
            }
        }
    }

    /// <summary>Verifies unset fields combine while explicit default overrides inheritance.</summary>
    [Fact]
    public void Resolve_WhenFieldsAreIndependent_CombinesAndPreservesExplicitDefault()
    {
        var style = new UiStyle();
        style.Set(
            State.Normal,
            new Appearance(
                foreground: Color.Indexed(2),
                background: Color.Indexed(4)));
        style.Set(State.Hovered, new Appearance(foreground: Color.Default));
        style.Set(State.Focused, new Appearance(attributes: Attributes.Underline));

        var resolved = Resolver.Resolve(style, State.Hovered | State.Focused);

        resolved.Foreground.ShouldBe(Color.Default);
        resolved.Background.ShouldBe(Color.Indexed(4));
        resolved.Attributes.ShouldBe(Attributes.Underline);
    }

    /// <summary>Verifies decoration fields overlay independently into semantic terminal style.</summary>
    [Fact]
    public void Resolve_WhenDecorationsAreDefined_PreservesTypedUnderlineAndColor()
    {
        var style = new UiStyle();
        style.Set(
            State.Normal,
            new Appearance(
                attributes: Attributes.RapidBlink,
                underline: Underline.Curly,
                underlineColor: Color.Rgb(1, 2, 3)));
        style.Set(State.Focused, new Appearance(underline: Underline.Paired));

        var appearance = Resolver.Resolve(style, State.Focused);
        var terminal = Resolver.ToTerminal(appearance);

        terminal.Attributes.ShouldBe(Attributes.RapidBlink);
        terminal.Underline.ShouldBe(Underline.Paired);
        terminal.UnderlineColor.ShouldBe(Color.Rgb(1, 2, 3));
    }

    /// <summary>Verifies invalid state keys and attributes fail before resource mutation.</summary>
    [Fact]
    public void Set_WhenDefinitionIsInvalid_ThrowsBeforeChange()
    {
        var style = new UiStyle();
        var changed = 0;
        style.Changed += (_, _) => changed++;

        _ = Should.Throw<ArgumentException>(() =>
            style.Set(State.Hovered | State.Focused, new Appearance()));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            style.Set((State) int.MaxValue, new Appearance()));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new Appearance(attributes: (Attributes) int.MaxValue));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new Appearance(underline: (Underline) int.MaxValue));

        changed.ShouldBe(0);
        style.TryGet(State.Normal, out _).ShouldBeFalse();
    }

    /// <summary>Verifies inherited dependencies stop at direct styles and replacement unsubscribes.</summary>
    [Fact]
    public async Task Style_WhenResourcesChange_InvalidatesOnlyCurrentDependentsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var inherited = new UiStyle();
            var direct = new UiStyle();
            var replacement = new UiStyle();
            inherited.Set(State.Normal, new Appearance(foreground: Color.Indexed(1)));
            direct.Set(State.Normal, new Appearance(foreground: Color.Indexed(2)));
            replacement.Set(State.Normal, new Appearance(foreground: Color.Indexed(3)));
            var root = new ProbeContainer { Style = inherited };
            var child = new ProbeControl();
            root.Children.Add(child);
            root.Attach(dispatcher);
            root.Clear(Invalidation.All);
            child.Clear(Invalidation.All);

            inherited.Set(State.Normal, new Appearance(foreground: Color.Indexed(4)));
            child.Pending.ShouldBe(Invalidation.Render);
            child.Appearance.Foreground.ShouldBe(Color.Indexed(4));
            child.Style = direct;
            root.Clear(Invalidation.All);
            child.Clear(Invalidation.All);
            inherited.Set(State.Normal, new Appearance(foreground: Color.Indexed(5)));
            child.Pending.ShouldBe(Invalidation.None);
            child.Style = replacement;
            child.Clear(Invalidation.All);
            direct.Set(State.Normal, new Appearance(foreground: Color.Indexed(6)));
            child.Pending.ShouldBe(Invalidation.None);
            replacement.Set(State.Normal, new Appearance(foreground: Color.Indexed(7)));
            child.Pending.ShouldBe(Invalidation.Render);
            _ = root.Children.Remove(child);
            child.Clear(Invalidation.All);
            replacement.Set(State.Normal, new Appearance(foreground: Color.Indexed(8)));
            child.Pending.ShouldBe(Invalidation.None);
            root.Children.Add(child);
            child.Clear(Invalidation.All);
            replacement.Set(State.Normal, new Appearance(foreground: Color.Indexed(9)));
            child.Pending.ShouldBe(Invalidation.Render);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies color is render-only while padding requests complete layout.</summary>
    [Fact]
    public async Task Set_WhenImpactDiffers_InvalidatesRequiredControlPhaseAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var style = new UiStyle();
            var control = new ProbeControl { Style = style };
            control.Attach(dispatcher);
            control.Clear(Invalidation.All);

            style.Set(State.Normal, new Appearance(foreground: Color.Indexed(2)));
            control.Pending.ShouldBe(Invalidation.Render);
            control.Clear(Invalidation.All);
            style.Set(State.Normal, new Appearance(padding: new Thickness(1)));
            control.Pending.ShouldBe(Invalidation.All);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies appearance reads behavior but never mutates enabled state.</summary>
    [Fact]
    public void Appearance_WhenDisabledOverlayExists_DoesNotControlBehavior()
    {
        var style = new UiStyle();
        style.Set(State.Disabled, new Appearance(foreground: Color.Indexed(8)));
        var control = new ProbeControl { Style = style };

        control.IsEnabled.ShouldBeTrue();
        control.Appearance.Foreground.ShouldBeNull();
        control.IsEnabled = false;
        control.Appearance.Foreground.ShouldBe(Color.Indexed(8));
        control.IsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies combined states reach exact terminal cell metadata.</summary>
    [Fact]
    public void Draw_WhenStatesCombine_WritesResolvedCellStyle()
    {
        var style = new UiStyle();
        style.Set(State.Normal, new Appearance(foreground: Color.Indexed(2)));
        style.Set(State.Focused, new Appearance(attributes: Attributes.Underline));
        style.Set(State.Disabled, new Appearance(foreground: Color.Indexed(8)));
        var control = new ProbeControl
        {
            Bounds = new Rect(0, 0, 1, 1),
            Style = style,
        };
        control.SetFocused(true);
        control.IsEnabled = false;
        using var frame = new Frame(new Size(1, 1));

        control.Draw(frame.Canvas, new Rune('A'));

        var cell = frame.GetCell(default);
        cell.Style.Foreground.ShouldBe(Color.Indexed(8));
        cell.Style.Attributes.ShouldBe(Attributes.Underline);
    }

    private static UiStyle CreatePrecedenceStyle()
    {
        var style = new UiStyle();
        style.Set(State.Normal, new Appearance(foreground: Color.Indexed(1)));
        style.Set(State.Hovered, new Appearance(foreground: Color.Indexed(2)));
        style.Set(State.Focused, new Appearance(foreground: Color.Indexed(3)));
        style.Set(State.Checked, new Appearance(foreground: Color.Indexed(4)));
        style.Set(State.Pressed, new Appearance(foreground: Color.Indexed(5)));
        style.Set(State.Disabled, new Appearance(foreground: Color.Indexed(6)));
        return style;
    }
}
