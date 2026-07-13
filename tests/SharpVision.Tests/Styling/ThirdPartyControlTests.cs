// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;



/// <summary>Verifies third-party control extensibility through custom style properties.</summary>
public sealed class ThirdPartyControlTests
{
    /// <summary>Verifies change notifications report CLR names for custom and built-in properties.</summary>
    [Fact]
    public void SetValue_RaisesPropertyChangedWithClrName()
    {
        DemoPanel panel = new();
        List<string?> names = [];
        panel.PropertyChanged += (_, args) => names.Add(args.PropertyName);

        panel.LabelPlacement = DemoLabelPlacement.Right;
        panel.Foreground = Color.Indexed(3);

        names.ShouldContain("LabelPlacement");
        names.ShouldContain("Foreground");
    }

    /// <summary>Verifies a custom property inherits themed values from the application chain.</summary>
    [Fact]
    public void Resolve_WhenThemeDefinesCustomProperty_UsesThemedValue()
    {
        Theme theme = new();
        ControlStyle<DemoPanel> style = new();
        style.Set(DemoPanel.LabelPlacementProperty, State.Normal, DemoLabelPlacement.Right);
        theme.SetStyle(style);
        DemoPanel panel = new();
        ThemeTestSupport.ApplyTheme(panel, theme);

        panel.LabelPlacement.ShouldBe(DemoLabelPlacement.Right);
    }

    /// <summary>Verifies local overrides win over themed custom property values.</summary>
    [Fact]
    public void Resolve_WhenLocalOverrideExists_WinsOverTheme()
    {
        Theme theme = new();
        ControlStyle<DemoPanel> style = new();
        style.Set(DemoPanel.LabelPlacementProperty, State.Normal, DemoLabelPlacement.Right);
        theme.SetStyle(style);
        DemoPanel panel = new();
        ThemeTestSupport.ApplyTheme(panel, theme);
        panel.LabelPlacement = DemoLabelPlacement.Left;

        panel.LabelPlacement.ShouldBe(DemoLabelPlacement.Left);
    }

    /// <summary>Verifies clearing a local override restores the themed value.</summary>
    [Fact]
    public void ClearValue_WhenLocalOverrideIsRemoved_RestoresTheme()
    {
        Theme theme = new();
        ControlStyle<DemoPanel> style = new();
        style.Set(DemoPanel.LabelPlacementProperty, State.Normal, DemoLabelPlacement.Right);
        theme.SetStyle(style);
        DemoPanel panel = new();
        ThemeTestSupport.ApplyTheme(panel, theme);
        panel.LabelPlacement = DemoLabelPlacement.Left;
        panel.ClearValue(DemoPanel.LabelPlacementProperty);

        panel.LabelPlacement.ShouldBe(DemoLabelPlacement.Right);
    }

    /// <summary>Verifies republishing a new theme snapshot updates custom property resolution.</summary>
    [Fact]
    public void RefreshTheme_WhenThemeChanges_UpdatesCustomProperty()
    {
        Theme dark = new();
        ControlStyle<DemoPanel> darkStyle = new();
        darkStyle.Set(DemoPanel.LabelPlacementProperty, State.Normal, DemoLabelPlacement.Left);
        dark.SetStyle(darkStyle);
        Theme light = new();
        ControlStyle<DemoPanel> lightStyle = new();
        lightStyle.Set(DemoPanel.LabelPlacementProperty, State.Normal, DemoLabelPlacement.Right);
        light.SetStyle(lightStyle);
        DemoPanel panel = new();
        ThemeTestSupport.ApplyTheme(panel, dark);

        ThemeTestSupport.RefreshTheme(panel, light);

        panel.LabelPlacement.ShouldBe(DemoLabelPlacement.Right);
    }
}
