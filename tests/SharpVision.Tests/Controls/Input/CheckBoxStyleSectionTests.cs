// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using SharpVision.Tests.Styling;

/// <summary>Verifies CheckBoxStyle resolves MarkStyle from the theme's registrable "checkBox"
/// style section (see #155) when one is authored, and falls back to code defaults otherwise.</summary>
public sealed class CheckBoxStyleSectionTests
{
    /// <summary>Verifies an authored section's MarkStyle is applied.</summary>
    [Fact]
    public void Definition_WhenThemeAuthorsCheckBoxSection_ResolvesMarkStyle()
    {
        var json = ThemeJson.Create(extraStyles: ""","checkBox":{"markStyle":"tick"}""");
        var theme = Themes.Parse(json);

        var style = CheckBoxStyle.Definition.Resolve(null, theme);

        style.MarkStyle.ShouldBe(CheckBoxMarkStyle.Tick);
    }

    /// <summary>Verifies a theme that authors no "checkBox" section falls back to code defaults.</summary>
    [Fact]
    public void Definition_WhenThemeHasNoCheckBoxSection_FallsBackToDefaults()
    {
        var theme = Themes.Parse(ThemeJson.Create());

        var style = CheckBoxStyle.Definition.Resolve(null, theme);

        style.MarkStyle.ShouldBe(CheckBoxStyle.Default.MarkStyle);
    }

    /// <summary>Verifies a local complete style always wins over any authored section.</summary>
    [Fact]
    public void Definition_WhenLocalStyleIsSupplied_IgnoresSection()
    {
        var json = ThemeJson.Create(extraStyles: ""","checkBox":{"markStyle":"tick"}""");
        var theme = Themes.Parse(json);
        var local = CheckBoxStyle.Square;

        var style = CheckBoxStyle.Definition.Resolve(local, theme);

        style.ShouldBe(local);
    }

    /// <summary>Verifies an unrecognized MarkStyle value reports a source-labelled InvalidDataException.</summary>
    [Fact]
    public void Definition_WhenMarkStyleValueIsUnrecognized_Throws()
    {
        var json = ThemeJson.Create(extraStyles: ""","checkBox":{"markStyle":"bogus"}""");
        var theme = Themes.Parse(json);

        _ = Should.Throw<InvalidDataException>(() => CheckBoxStyle.Definition.Resolve(null, theme));
    }

    /// <summary>Verifies a plain unqualified "checkBox" section key parses without the
    /// third-party-namespacing rejection that applies to unregistered unqualified keys.</summary>
    [Fact]
    public void Parse_WhenCheckBoxSectionIsPresent_DoesNotThrow()
    {
        var json = ThemeJson.Create(extraStyles: ""","checkBox":{"markStyle":"square"}""");

        _ = Should.NotThrow(() => Themes.Parse(json));
    }
}
