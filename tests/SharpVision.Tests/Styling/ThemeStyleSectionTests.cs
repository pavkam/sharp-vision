// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using System.Text.Json.Serialization;

/// <summary>Verifies the registrable style-section mechanism: unmapped styles.* keys are either
/// retained for deferred binding or rejected, and Theme.GetStyleSection binds and memoizes them
/// (see #155).</summary>
public sealed class ThemeStyleSectionTests
{
    /// <summary>Verifies an unqualified unknown styles key is rejected instead of silently
    /// retained, since it is very likely a typo of one of the five fixed profile names.</summary>
    [Fact]
    public void Parse_WhenStylesKeyIsUnqualifiedAndUnknown_Throws()
    {
        var json = ThemeJson.Create(extraStyles: ""","buton":{}""");

        _ = Should.Throw<InvalidDataException>(() => Themes.Parse(json));
    }

    /// <summary>Verifies a namespaced "vendor.control" styles key is retained and can be bound
    /// through GetStyleSection.</summary>
    [Fact]
    public void Parse_WhenStylesKeyIsNamespaced_RetainsSection()
    {
        var json = ThemeJson.Create(
            extraStyles: ""","acme.widget":{"color":"magenta"}""");

        var theme = Themes.Parse(json);

        var section = theme.GetStyleSection<TestSectionDefinition>("acme.widget");

        _ = section.ShouldNotBeNull();
        section.Color.ShouldBe("magenta");
    }

    /// <summary>Verifies a section this theme never authored resolves to null instead of throwing.</summary>
    [Fact]
    public void GetStyleSection_WhenSectionIsAbsent_ReturnsNull()
    {
        var theme = Themes.Parse(ThemeJson.Create());

        theme.GetStyleSection<TestSectionDefinition>("acme.widget").ShouldBeNull();
    }

    /// <summary>Verifies repeated resolution of the same section returns the same memoized instance.</summary>
    [Fact]
    public void GetStyleSection_WhenCalledTwice_ReturnsSameInstance()
    {
        var json = ThemeJson.Create(
            extraStyles: ""","acme.widget":{"color":"magenta"}""");
        var theme = Themes.Parse(json);

        var first = theme.GetStyleSection<TestSectionDefinition>("acme.widget");
        var second = theme.GetStyleSection<TestSectionDefinition>("acme.widget");

        ReferenceEquals(first, second).ShouldBeTrue();
    }

    /// <summary>Verifies a section whose retained JSON does not match the requested shape reports a
    /// source-labelled InvalidDataException instead of an unhandled JsonException.</summary>
    [Fact]
    public void GetStyleSection_WhenSectionJsonDoesNotMatchShape_Throws()
    {
        var json = ThemeJson.Create(
            extraStyles: ""","acme.widget":{"color":123}""");
        var theme = Themes.Parse(json);

        _ = Should.Throw<InvalidDataException>(() => theme.GetStyleSection<TestSectionDefinition>("acme.widget"));
    }

    /// <summary>Verifies a null section name throws ArgumentNullException.</summary>
    [Fact]
    public void GetStyleSection_WhenSectionNameIsNull_Throws()
    {
        var theme = Themes.Parse(ThemeJson.Create());

        _ = Should.Throw<ArgumentNullException>(() => theme.GetStyleSection<TestSectionDefinition>(null!));
    }

    private sealed class TestSectionDefinition
    {
        [JsonPropertyName("color")]
        public string? Color { get; set; }
    }
}
