// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

    /// <summary>Verifies the standard themes are frozen, stable singletons distinct from each other.</summary>
    [Fact]
    public void Themes_AreCachedFrozenInstances()
    {
        Themes.Dark.IsFrozen.ShouldBeTrue();
        ReferenceEquals(Themes.Dark, Themes.Dark).ShouldBeTrue();
        ReferenceEquals(Themes.White, Themes.Dark).ShouldBeFalse();
    }
}
