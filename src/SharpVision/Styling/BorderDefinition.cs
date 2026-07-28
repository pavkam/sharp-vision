// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json.Serialization;

/// <summary>Defines optional border members in a theme document.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BorderDefinition
{
    /// <summary>Gets or sets the enabled-side name.</summary>
    [JsonPropertyName("sides")]
    public string? Sides { get; set; }

    /// <summary>Gets or sets the standard border glyph-style name.</summary>
    [JsonPropertyName("glyphStyle")]
    public string? GlyphStyle { get; set; }

    /// <summary>Gets or sets the foreground reference.</summary>
    [JsonPropertyName("foreground")]
    public string? Foreground { get; set; }

    /// <summary>Gets or sets the background reference.</summary>
    [JsonPropertyName("background")]
    public string? Background { get; set; }

    /// <summary>Gets or sets the terminal-attribute value or theme-decoration reference.</summary>
    [JsonPropertyName("attributes")]
    public JsonElement? Attributes { get; set; }
}
