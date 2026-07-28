// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json.Serialization;

/// <summary>Defines optional checkbox presentation contributions in a theme document.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CheckBoxStyleDefinition
{
    /// <summary>Gets or sets the optional mark-layout family name.</summary>
    [JsonPropertyName("markStyle")]
    public string? MarkStyle { get; set; }

    /// <summary>Gets or sets the optional complete state glyph family.</summary>
    [JsonPropertyName("glyphs")]
    public CheckBoxGlyphsDefinition? Glyphs { get; set; }

    /// <summary>Gets or sets the optional normal and visual-state appearance contribution.</summary>
    [JsonPropertyName("appearance")]
    public ThemeProfileDefinition? Appearance { get; set; }
}

