// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using SharpVision.Styling;

using Shouldly;

/// <summary>Verifies theme JSON deserialization into the definition DTO.</summary>
public sealed class ThemeDeserializeTests
{
    private const string _json = """
        {
          "name": "Sample", "slug": "sample", "colorScheme": "dark", "order": 5,
          "author": "A", "license": "MIT", "source": "https://example.test",
          "palette": { "bg": "#000000", "fg": "#ffffff" },
          "roles": { "background": "bg", "foreground": "fg" }
        }
        """;

    /// <summary>Verifies a well-formed theme document maps every field onto the definition.</summary>
    [Fact]
    public void Deserialize_WhenValidJson_MapsFields()
    {
        ThemeDefinition definition = ThemeLoader.Deserialize(_json, "sample");

        definition.Slug.ShouldBe("sample");
        definition.Order.ShouldBe(5);
        definition.ColorScheme.ShouldBe("dark");
        definition.Palette!["bg"].ShouldBe("#000000");
        definition.Roles!["foreground"].ShouldBe("fg");
    }

    /// <summary>Verifies malformed JSON is wrapped as <see cref="InvalidDataException"/> naming the source.</summary>
    [Fact]
    public void Deserialize_WhenMalformedJson_Throws()
    {
        InvalidDataException error = Should.Throw<InvalidDataException>(
            () => ThemeLoader.Deserialize("{ not json", "broken"));

        error.Message.ShouldContain("broken");
    }
}
