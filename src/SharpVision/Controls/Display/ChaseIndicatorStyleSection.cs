// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

using System.Text.Json.Serialization;

/// <summary>Defines the registrable "chaseIndicator" theme-file style section (see #155).</summary>
internal sealed class ChaseIndicatorStyleSection
{
    /// <summary>Gets or sets the one-Rune active-position glyph.</summary>
    [JsonPropertyName("active")]
    public string? Active { get; set; }

    /// <summary>Gets or sets the one-Rune inactive-position glyph.</summary>
    [JsonPropertyName("inactive")]
    public string? Inactive { get; set; }
}
