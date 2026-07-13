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

namespace SharpVision.Tests.Styling;

/// <summary>Verifies theme integration with control invalidation and rendering.</summary>
public sealed class StyleTests
{
    /// <summary>Verifies theme and instance-style dependencies invalidate only current dependents.</summary>
    [Fact]
    public async Task Style_WhenResourcesChange_InvalidatesOnlyCurrentDependentsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var theme = new Theme();
            var inherited = ThemeTestSupport.CreateControlStyle();
            inherited.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(1));
            theme.SetStyle(inherited);
            var direct = ThemeTestSupport.OverlayStyle<Control>(
                (State.Normal, new ThemeOverlay(foreground: Color.Indexed(2))));
            var replacement = ThemeTestSupport.OverlayStyle<Control>(
                (State.Normal, new ThemeOverlay(foreground: Color.Indexed(3))));
            var root = new ProbeContainer();
            ThemeTestSupport.ApplyTheme(root, theme);
            theme.Changed += (_, args) =>
            {
                ThemeTestSupport.RefreshTheme(root, theme);
                InvalidateThemeDependents(root, args.Impact);
            };
            var child = new ProbeControl();
            root.Children.Add(child);
            root.Attach(dispatcher);
            root.Clear(Invalidation.All);
            child.Clear(Invalidation.All);

            inherited.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(4));
            child.Pending.ShouldBe(Invalidation.Render);
            child.Foreground.ShouldBe(Color.Indexed(4));
            child.Style = direct;
            root.Clear(Invalidation.All);
            child.Clear(Invalidation.All);
            inherited.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(5));
            child.Pending.ShouldBe(Invalidation.None);
            child.Style = replacement;
            child.Clear(Invalidation.All);
            direct.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(6));
            child.Pending.ShouldBe(Invalidation.None);
            replacement.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(7));
            child.Pending.ShouldBe(Invalidation.Render);
            _ = root.Children.Remove(child);
            child.Clear(Invalidation.All);
            replacement.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(8));
            child.Pending.ShouldBe(Invalidation.None);
            root.Children.Add(child);
            child.Clear(Invalidation.All);
            replacement.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(9));
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
            var style = ThemeTestSupport.CreateStyle<Control>();
            var control = new ProbeControl { Style = style };
            control.Attach(dispatcher);
            control.Clear(Invalidation.All);

            style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(2));
            control.Pending.ShouldBe(Invalidation.Render);
            control.Clear(Invalidation.All);
            style.Set(Control.PaddingProperty, State.Normal, new Thickness(1));
            control.Pending.ShouldBe(Invalidation.All);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies foreground reads behavior but never mutates enabled state.</summary>
    [Fact]
    public void Foreground_WhenDisabledOverlayExists_DoesNotControlBehavior()
    {
        var style = ThemeTestSupport.OverlayStyle<Control>(
            (State.Disabled, new ThemeOverlay(foreground: Color.Indexed(8))));
        var control = new ProbeControl { Style = style };

        control.IsEnabled.ShouldBeTrue();
        control.Foreground.ShouldBeNull();
        control.IsEnabled = false;
        control.Foreground.ShouldBe(Color.Indexed(8));
        control.IsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies combined states reach exact terminal cell metadata.</summary>
    [Fact]
    public void Draw_WhenStatesCombine_WritesResolvedCellStyle()
    {
        var style = ThemeTestSupport.OverlayStyle<Control>(
            (State.Normal, new ThemeOverlay(foreground: Color.Indexed(2))),
            (State.Focused, new ThemeOverlay(attributes: Attributes.Underline)),
            (State.Disabled, new ThemeOverlay(foreground: Color.Indexed(8))));
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

    /// <summary>Verifies assigning a style whose target type does not match the control throws.</summary>
    [Fact]
    public void Style_WhenTargetTypeMismatched_Throws()
    {
        var control = new ProbeControl();
        var foreign = new ControlStyle<Button>();

        _ = Should.Throw<ArgumentException>(() => control.Style = foreign);
    }

    /// <summary>Verifies assigning a base-typed style to a derived control is accepted.</summary>
    [Fact]
    public void Style_WhenTargetTypeIsBase_Accepted()
    {
        var control = new ProbeControl();
        var baseStyle = new ControlStyle<Control>();

        control.Style = baseStyle;

        control.Style.ShouldBeSameAs(baseStyle);
    }

    /// <summary>Verifies the local-override CRUD triad is publicly callable off a control instance.</summary>
    [Fact]
    public void GetValueSetValueClearValue_ArePublicAndConsistent()
    {
        var control = new ProbeControl();

        control.SetValue(Control.ForegroundProperty, Color.Indexed(5));
        control.GetValue(Control.ForegroundProperty).ShouldBe(Color.Indexed(5));

        control.ClearValue(Control.ForegroundProperty);
        control.GetValue(Control.ForegroundProperty).ShouldBeNull();
    }

    private static void InvalidateThemeDependents(Control control, Impact impact)
    {
        var invalidation = impact == Impact.Measure ? Invalidation.Measure : Invalidation.Render;

        if (control.InstanceStyle is null)
        {
            control.Invalidate(invalidation);
        }

        control.VisitChildren(child => InvalidateThemeDependents(child, impact));
    }
}
