using SharpVision.Controls;
using SharpVision.Styling;
using SharpVision.Terminal.Protocols;
using SharpVision.Tests.Support;

using Shouldly;

namespace SharpVision.Tests.Styling;

/// <summary>Verifies frozen standard theme semantic values.</summary>
public sealed class StandardThemeTests
{
    /// <summary>Verifies the dark theme supplies indexed foreground and background defaults.</summary>
    [Fact]
    public void Dark_WhenResolvedOnControl_UsesIndexedSemanticCells()
    {
        var control = new ProbeControl();
        ThemeTestSupport.ApplyTheme(control, Themes.Dark);

        ThemeTestSupport.Resolve(control, Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(15));
        ThemeTestSupport.Resolve(control, Control.BackgroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(0));
        ThemeTestSupport.Resolve(control, Control.BorderColorProperty, State.Normal)
            .ShouldBe(Color.Indexed(8));
    }

    /// <summary>Verifies the white theme supplies inverted indexed defaults.</summary>
    [Fact]
    public void White_WhenResolvedOnControl_UsesIndexedSemanticCells()
    {
        var control = new ProbeControl();
        ThemeTestSupport.ApplyTheme(control, Themes.White);

        ThemeTestSupport.Resolve(control, Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(0));
        ThemeTestSupport.Resolve(control, Control.BackgroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(15));
    }
}
