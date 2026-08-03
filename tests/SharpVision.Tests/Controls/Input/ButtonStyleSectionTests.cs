// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using SharpVision.Tests.Styling;

/// <summary>Verifies ButtonStyle resolves Padding from the theme's registrable "button" style
/// section (see #155) when one is authored, and falls back to code defaults otherwise.</summary>
public sealed class ButtonStyleSectionTests
{
    /// <summary>Verifies an authored section's padding is applied.</summary>
    [Fact]
    public void Definition_WhenThemeAuthorsButtonSection_ResolvesPadding()
    {
        var json = ThemeJson.Create(
            extraStyles: ""","button":{"horizontalPadding":3,"verticalPadding":1}""");
        var theme = Themes.Parse(json);

        var style = ButtonStyle.Definition.Resolve(null, theme);

        style.Padding.ShouldBe(new Thickness(horizontal: 3, vertical: 1));
    }

    /// <summary>Verifies an authored section supplying only one axis leaves the other at its default.</summary>
    [Fact]
    public void Definition_WhenSectionSuppliesOnlyOneAxis_PreservesTheOtherDefault()
    {
        var json = ThemeJson.Create(extraStyles: ""","button":{"horizontalPadding":5}""");
        var theme = Themes.Parse(json);

        var style = ButtonStyle.Definition.Resolve(null, theme);

        style.Padding.ShouldBe(new Thickness(horizontal: 5, vertical: ButtonStyle.Standard.Padding.Top));
    }

    /// <summary>Verifies a theme that authors no "button" section falls back to code defaults.</summary>
    [Fact]
    public void Definition_WhenThemeHasNoButtonSection_FallsBackToDefaults()
    {
        var theme = Themes.Parse(ThemeJson.Create());

        var style = ButtonStyle.Definition.Resolve(null, theme);

        style.Padding.ShouldBe(ButtonStyle.Standard.Padding);
    }

    /// <summary>Verifies a local complete style always wins over any authored section.</summary>
    [Fact]
    public void Definition_WhenLocalStyleIsSupplied_IgnoresSection()
    {
        var json = ThemeJson.Create(
            extraStyles: ""","button":{"horizontalPadding":3,"verticalPadding":1}""");
        var theme = Themes.Parse(json);
        var local = ButtonStyle.Filled;

        var style = ButtonStyle.Definition.Resolve(local, theme);

        style.ShouldBe(local);
    }

    /// <summary>Verifies a negative authored padding reports the same ArgumentOutOfRangeException
    /// a hand-authored Thickness would.</summary>
    [Fact]
    public void Definition_WhenPaddingIsNegative_Throws()
    {
        var json = ThemeJson.Create(extraStyles: ""","button":{"horizontalPadding":-1}""");
        var theme = Themes.Parse(json);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => ButtonStyle.Definition.Resolve(null, theme));
    }

    /// <summary>Verifies a plain unqualified "button" section key parses without the
    /// third-party-namespacing rejection that applies to unregistered unqualified keys.</summary>
    [Fact]
    public void Parse_WhenButtonSectionIsPresent_DoesNotThrow()
    {
        var json = ThemeJson.Create(extraStyles: ""","button":{"horizontalPadding":2}""");

        _ = Should.NotThrow(() => Themes.Parse(json));
    }
}
