// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;



/// <summary>Verifies theme JSON deserialization into the definition DTO.</summary>
public sealed class ThemeDeserializeTests
{
    private static readonly string _json = ThemeJson.Create(
        "\"background\":\"bg\",\"foreground\":\"fg\"",
        "\"bg\":\"#000000\",\"fg\":\"#ffffff\"",
        "Sample");

    /// <summary>Verifies a well-formed theme document maps every field onto the definition.</summary>
    [Fact]
    public void Deserialize_WhenValidJson_MapsFields()
    {
        var definition = ThemeLoader.Deserialize(_json, "sample");

        definition.Version.ShouldBe(2);
        definition.Slug.ShouldBe("t");
        definition.Order.ShouldBe(1);
        definition.ColorScheme.ShouldBe("dark");
        definition.Palette!["bg"].ShouldBe("#000000");
        definition.Roles!["foreground"].ShouldBe("fg");
    }

    /// <summary>Verifies malformed JSON is wrapped as <see cref="InvalidDataException"/> naming the source.</summary>
    [Fact]
    public void Deserialize_WhenMalformedJson_Throws()
    {
        var error = Should.Throw<InvalidDataException>(
            () => ThemeLoader.Deserialize("{ not json", "broken"));

        error.Message.ShouldContain("broken");
    }
}
