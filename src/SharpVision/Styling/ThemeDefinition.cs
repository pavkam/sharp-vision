// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json.Serialization;

/// <summary>Deserialization target for a theme JSON file. Validation happens in <see cref="ThemeLoader"/>.</summary>
internal sealed class ThemeDefinition
{
    /// <summary>Gets or sets the display name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Gets or sets the stable catalog slug.</summary>
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    /// <summary>Gets or sets the color-scheme token (<c>dark</c> or <c>light</c>).</summary>
    [JsonPropertyName("colorScheme")]
    public string? ColorScheme { get; set; }

    /// <summary>Gets or sets the deterministic catalog sort key.</summary>
    [JsonPropertyName("order")]
    public int Order { get; set; }

    /// <summary>Gets or sets the attribution author.</summary>
    [JsonPropertyName("author")]
    public string? Author { get; set; }

    /// <summary>Gets or sets the license identifier.</summary>
    [JsonPropertyName("license")]
    public string? License { get; set; }

    /// <summary>Gets or sets the source URL.</summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    /// <summary>Gets or sets the named palette (name to color-value string).</summary>
    [JsonPropertyName("palette")]
    public Dictionary<string, string>? Palette { get; set; }

    /// <summary>Gets or sets the semantic role map (role name to color-value or palette key).</summary>
    [JsonPropertyName("roles")]
    public Dictionary<string, string>? Roles { get; set; }
}
