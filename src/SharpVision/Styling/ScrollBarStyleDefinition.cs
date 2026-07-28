// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json.Serialization;

/// <summary>Defines optional scrollbar presentation contributions in a theme document.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ScrollBarStyleDefinition
{
    /// <summary>Gets or sets the optional chrome name.</summary>
    [JsonPropertyName("chrome")]
    public string? Chrome { get; set; }

    /// <summary>Gets or sets the optional fill-treatment name.</summary>
    [JsonPropertyName("fill")]
    public string? Fill { get; set; }

    /// <summary>Gets or sets the optional complete glyph family.</summary>
    [JsonPropertyName("glyphs")]
    public ScrollBarGlyphsDefinition? Glyphs { get; set; }

    /// <summary>Gets or sets the optional track foreground reference.</summary>
    [JsonPropertyName("trackColor")]
    public string? TrackColor { get; set; }

    /// <summary>Gets or sets the optional thumb foreground reference.</summary>
    [JsonPropertyName("thumbColor")]
    public string? ThumbColor { get; set; }

    /// <summary>Gets or sets the optional directional-button foreground reference.</summary>
    [JsonPropertyName("buttonColor")]
    public string? ButtonColor { get; set; }

    /// <summary>Gets or sets the optional normal and visual-state appearance contribution.</summary>
    [JsonPropertyName("appearance")]
    public ThemeProfileDefinition? Appearance { get; set; }
}

