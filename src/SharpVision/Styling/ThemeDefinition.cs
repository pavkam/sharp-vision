// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json.Serialization;

/// <summary>Defines the JSON document shape consumed by <see cref="Themes"/>.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ThemeDefinition
{
    /// <summary>Gets or sets the human-readable theme name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Gets or sets the stable catalog slug.</summary>
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    /// <summary>Gets or sets the authored dark or light color scheme.</summary>
    [JsonPropertyName("colorScheme")]
    public string? ColorScheme { get; set; }

    /// <summary>Gets or sets the catalog sort order.</summary>
    [JsonPropertyName("order")]
    public int Order { get; set; }

    /// <summary>Gets or sets the attribution author.</summary>
    [JsonPropertyName("author")]
    public string? Author { get; set; }

    /// <summary>Gets or sets the palette license identifier.</summary>
    [JsonPropertyName("license")]
    public string? License { get; set; }

    /// <summary>Gets or sets the upstream palette source.</summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    /// <summary>Gets or sets named RGB colors.</summary>
    [JsonPropertyName("palette")]
    public Dictionary<string, string>? Palette { get; set; }

    /// <summary>Gets or sets concrete values for every known global color role.</summary>
    [JsonPropertyName("colors")]
    public ThemeColorsDefinition? Colors { get; set; }

    /// <summary>Gets or sets concrete values for every known global terminal-decoration role.</summary>
    [JsonPropertyName("attributes")]
    public ThemeAttributesDefinition? Attributes { get; set; }

    /// <summary>Gets or sets the required semantic style profiles.</summary>
    [JsonPropertyName("styles")]
    public ThemeStylesDefinition? Styles { get; set; }

    /// <summary>Gets or sets cross-cutting status color references.</summary>
    [JsonPropertyName("status")]
    public Dictionary<string, string>? Status { get; set; }
}
