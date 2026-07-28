// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json.Serialization;

/// <summary>Defines the required complete checkbox glyph family in a theme document.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CheckBoxGlyphsDefinition
{
    /// <summary>Gets or sets the unchecked glyph.</summary>
    [JsonPropertyName("unchecked")]
    public required string Unchecked { get; set; }

    /// <summary>Gets or sets the checked glyph.</summary>
    [JsonPropertyName("checked")]
    public required string Checked { get; set; }

    /// <summary>Gets or sets the indeterminate glyph.</summary>
    [JsonPropertyName("indeterminate")]
    public required string Indeterminate { get; set; }
}
