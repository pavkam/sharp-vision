// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

using System.Text.Json.Serialization;

/// <summary>Defines the registrable "progressBar" theme-file style section (see #155). Colors
/// accept "transparent", "default", a ThemeColor name, a "#RGB"/"#RRGGBB" literal, or a palette
/// key - matching every other theme-JSON color field in this codebase.</summary>
internal sealed class ProgressBarStyleSection
{
    /// <summary>Gets or sets the completed-progress foreground.</summary>
    [JsonPropertyName("fillColor")]
    public string? FillColor { get; set; }

    /// <summary>Gets or sets the incomplete-track foreground.</summary>
    [JsonPropertyName("trackColor")]
    public string? TrackColor { get; set; }

    /// <summary>Gets or sets the indeterminate-state foreground.</summary>
    [JsonPropertyName("indeterminateColor")]
    public string? IndeterminateColor { get; set; }

    /// <summary>Gets or sets the complete fill, track, and indeterminate glyph family.</summary>
    [JsonPropertyName("glyphs")]
    public ProgressBarGlyphsSection? Glyphs { get; set; }
}

/// <summary>Defines the "progressBar.glyphs" theme-file style section (see #155). Every member is
/// one printable one-cell Rune string, matching <see cref="ProgressBarGlyphs"/>'s own members.</summary>
internal sealed class ProgressBarGlyphsSection
{
    /// <summary>Gets or sets the fully filled glyph.</summary>
    [JsonPropertyName("fill")]
    public string? Fill { get; set; }

    /// <summary>Gets or sets the empty-track glyph.</summary>
    [JsonPropertyName("track")]
    public string? Track { get; set; }

    /// <summary>Gets or sets the indeterminate-state glyph.</summary>
    [JsonPropertyName("indeterminate")]
    public string? Indeterminate { get; set; }
}
