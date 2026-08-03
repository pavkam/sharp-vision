// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Text.Json.Serialization;

/// <summary>Defines the registrable "slider" theme-file style section (see #155). Colors accept
/// "transparent", "default", a ThemeColor name, a "#RGB"/"#RRGGBB" literal, or a palette key -
/// matching every other theme-JSON color field in this codebase.</summary>
internal sealed class SliderStyleSection
{
    /// <summary>Gets or sets the filled-rail foreground.</summary>
    [JsonPropertyName("fillColor")]
    public string? FillColor { get; set; }

    /// <summary>Gets or sets the unfilled-rail foreground.</summary>
    [JsonPropertyName("trackColor")]
    public string? TrackColor { get; set; }

    /// <summary>Gets or sets the thumb foreground.</summary>
    [JsonPropertyName("thumbColor")]
    public string? ThumbColor { get; set; }
}
