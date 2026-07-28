// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json.Serialization;

/// <summary>Defines optional spinner presentation contributions in a theme document.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class SpinnerStyleDefinition
{
    /// <summary>Gets or sets the optional replacement sequence of one-Rune frame strings.</summary>
    [JsonPropertyName("frames")]
    public string[]? Frames { get; set; }

    /// <summary>Gets or sets the optional normal and visual-state appearance contribution.</summary>
    [JsonPropertyName("appearance")]
    public ThemeProfileDefinition? Appearance { get; set; }
}

