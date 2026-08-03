// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using SharpVision.Tests.Styling;

/// <summary>Verifies RadioButtonStyle resolves MarkStyle from the theme's registrable
/// "radioButton" style section (see #155) when one is authored, and falls back to code defaults
/// otherwise.</summary>
public sealed class RadioButtonStyleSectionTests
{
    /// <summary>Verifies an authored section's MarkStyle is applied.</summary>
    [Fact]
    public void Definition_WhenThemeAuthorsRadioButtonSection_ResolvesMarkStyle()
    {
        var json = ThemeJson.Create(extraStyles: ""","radioButton":{"markStyle":"circle"}""");
        var theme = Themes.Parse(json);

        var style = RadioButtonStyle.Definition.Resolve(null, theme);

        style.MarkStyle.ShouldBe(RadioButtonMarkStyle.Circle);
    }

    /// <summary>Verifies a theme that authors no "radioButton" section falls back to code defaults.</summary>
    [Fact]
    public void Definition_WhenThemeHasNoRadioButtonSection_FallsBackToDefaults()
    {
        var theme = Themes.Parse(ThemeJson.Create());

        var style = RadioButtonStyle.Definition.Resolve(null, theme);

        style.MarkStyle.ShouldBe(RadioButtonStyle.Default.MarkStyle);
    }

    /// <summary>Verifies a local complete style always wins over any authored section.</summary>
    [Fact]
    public void Definition_WhenLocalStyleIsSupplied_IgnoresSection()
    {
        var json = ThemeJson.Create(extraStyles: ""","radioButton":{"markStyle":"circle"}""");
        var theme = Themes.Parse(json);
        var local = RadioButtonStyle.Parentheses;

        var style = RadioButtonStyle.Definition.Resolve(local, theme);

        style.ShouldBe(local);
    }

    /// <summary>Verifies an unrecognized MarkStyle value reports a source-labelled InvalidDataException.</summary>
    [Fact]
    public void Definition_WhenMarkStyleValueIsUnrecognized_Throws()
    {
        var json = ThemeJson.Create(extraStyles: ""","radioButton":{"markStyle":"bogus"}""");
        var theme = Themes.Parse(json);

        _ = Should.Throw<InvalidDataException>(() => RadioButtonStyle.Definition.Resolve(null, theme));
    }

    /// <summary>Verifies a plain unqualified "radioButton" section key parses without the
    /// third-party-namespacing rejection that applies to unregistered unqualified keys.</summary>
    [Fact]
    public void Parse_WhenRadioButtonSectionIsPresent_DoesNotThrow()
    {
        var json = ThemeJson.Create(extraStyles: ""","radioButton":{"markStyle":"circle"}""");

        _ = Should.NotThrow(() => Themes.Parse(json));
    }
}
