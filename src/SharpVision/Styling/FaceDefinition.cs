// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json.Serialization;

/// <summary>Defines optional face members in a theme document.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class FaceDefinition
{
    /// <summary>Gets or sets the foreground reference.</summary>
    [JsonPropertyName("foreground")]
    public string? Foreground { get; set; }

    /// <summary>Gets or sets the background reference.</summary>
    [JsonPropertyName("background")]
    public string? Background { get; set; }

    /// <summary>Gets or sets the terminal-attribute value or theme-decoration reference.</summary>
    [JsonPropertyName("attributes")]
    public JsonElement? Attributes { get; set; }

    /// <summary>Gets or sets the typed underline name.</summary>
    [JsonPropertyName("underline")]
    public string? Underline { get; set; }

    /// <summary>Gets or sets the underline-color reference.</summary>
    [JsonPropertyName("underlineColor")]
    public string? UnderlineColor { get; set; }
}
