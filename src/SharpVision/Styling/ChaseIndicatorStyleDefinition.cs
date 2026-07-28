// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json.Serialization;

/// <summary>Defines optional chase-indicator presentation contributions in a theme document.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ChaseIndicatorStyleDefinition
{
    /// <summary>Gets or sets the optional active-position glyph string.</summary>
    [JsonPropertyName("active")]
    public string? Active { get; set; }

    /// <summary>Gets or sets the optional inactive-position glyph string.</summary>
    [JsonPropertyName("inactive")]
    public string? Inactive { get; set; }

    /// <summary>Gets or sets the optional normal and visual-state appearance contribution.</summary>
    [JsonPropertyName("appearance")]
    public ThemeProfileDefinition? Appearance { get; set; }
}

