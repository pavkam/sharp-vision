// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json.Serialization;

/// <summary>Defines optional radio-button presentation contributions in a theme document.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class RadioButtonStyleDefinition
{
    /// <summary>Gets or sets the optional mark-layout family name.</summary>
    [JsonPropertyName("markStyle")]
    public string? MarkStyle { get; set; }

    /// <summary>Gets or sets the optional complete glyph pair.</summary>
    [JsonPropertyName("glyphs")]
    public RadioButtonGlyphsDefinition? Glyphs { get; set; }

    /// <summary>Gets or sets the optional normal and visual-state appearance contribution.</summary>
    [JsonPropertyName("appearance")]
    public ThemeProfileDefinition? Appearance { get; set; }
}

