// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json.Serialization;

/// <summary>Defines optional shadow members in a theme document.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ShadowDefinition
{
    /// <summary>Gets or sets whether shadow chrome is visible.</summary>
    [JsonPropertyName("visible")]
    public bool? Visible { get; set; }

    /// <summary>Gets or sets the composition-mode name.</summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    /// <summary>Gets or sets the signed shadow translation.</summary>
    [JsonPropertyName("offset")]
    public ShadowOffsetDefinition? Offset { get; set; }

    /// <summary>Gets or sets the block-glyph shadow Rune.</summary>
    [JsonPropertyName("glyph")]
    public string? Glyph { get; set; }

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
