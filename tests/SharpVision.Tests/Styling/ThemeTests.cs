using SharpVision.Controls;
using SharpVision.Styling;
using SharpVision.Terminal.Protocols;
using SharpVision.Tests.Support;

using Shouldly;

namespace SharpVision.Tests.Styling;

/// <summary>Verifies theme collection, style chains, freezing, and cloning.</summary>
public sealed class ThemeTests
{
    /// <summary>Verifies a theme stores and returns one style per control type.</summary>
    [Fact]
    public void SetStyle_WhenControlStyleIsRegistered_GetStyleReturnsIt()
    {
        var theme = new Theme();
        var style = new ControlStyle<Control>();
        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(7));
        theme.SetStyle(style);

        theme.GetStyle<Control>()!.TryGet(Control.ForegroundProperty, State.Normal, out var value)
            .ShouldBeTrue();
        value.ShouldBe(Color.Indexed(7));
    }

    /// <summary>Verifies sparse type styles inherit through the control hierarchy chain.</summary>
    [Fact]
    public void GetStyleChain_WhenOnlyBaseStyleExists_ResolvesOnDerivedControl()
    {
        var theme = new Theme();
        var baseStyle = new ControlStyle<Control>();
        baseStyle.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(2));
        theme.SetStyle(baseStyle);
        var control = new ProbeControl();
        ThemeTestSupport.ApplyTheme(control, theme);

        ThemeTestSupport.Resolve(control, Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(2));
    }

    /// <summary>Verifies derived-type styles overlay base styles in the chain.</summary>
    [Fact]
    public void GetStyleChain_WhenDerivedStyleExists_OverlaysBaseStyle()
    {
        var theme = new Theme();
        var baseStyle = new ControlStyle<Control>();
        baseStyle.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(1));
        var derivedStyle = new ControlStyle<ProbeControl>();
        derivedStyle.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(4));
        theme.SetStyle(baseStyle);
        theme.SetStyle(derivedStyle);
        var control = new ProbeControl();
        ThemeTestSupport.ApplyTheme(control, theme);

        ThemeTestSupport.Resolve(control, Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(4));
    }

    /// <summary>Verifies frozen themes reject further mutation.</summary>
    [Fact]
    public void SetStyle_WhenThemeIsFrozen_Throws()
    {
        var theme = new Theme();
        theme.Freeze();

        _ = Should.Throw<InvalidOperationException>(() => theme.SetStyle(new ControlStyle<Control>()));
    }

    /// <summary>Verifies cloned themes receive independent style copies.</summary>
    [Fact]
    public void Clone_WhenSourceMutatesAfterCopy_DoesNotAffectClone()
    {
        var theme = new Theme();
        var style = new ControlStyle<Control>();
        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(3));
        theme.SetStyle(style);
        var clone = theme.Clone();
        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(9));

        clone.GetStyle<Control>()!.TryGet(Control.ForegroundProperty, State.Normal, out var value)
            .ShouldBeTrue();
        value.ShouldBe(Color.Indexed(3));
    }
}
