// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json.Serialization;

/// <summary>Defines the required complete scrollbar glyph family in a theme document.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ScrollBarGlyphsDefinition
{
    /// <summary>Gets or sets the vertical decrement glyph.</summary>
    [JsonPropertyName("verticalDecrement")]
    public required string VerticalDecrement { get; set; }

    /// <summary>Gets or sets the vertical increment glyph.</summary>
    [JsonPropertyName("verticalIncrement")]
    public required string VerticalIncrement { get; set; }

    /// <summary>Gets or sets the horizontal decrement glyph.</summary>
    [JsonPropertyName("horizontalDecrement")]
    public required string HorizontalDecrement { get; set; }

    /// <summary>Gets or sets the horizontal increment glyph.</summary>
    [JsonPropertyName("horizontalIncrement")]
    public required string HorizontalIncrement { get; set; }

    /// <summary>Gets or sets the block-track glyph.</summary>
    [JsonPropertyName("blockTrack")]
    public required string BlockTrack { get; set; }

    /// <summary>Gets or sets the block-thumb glyph.</summary>
    [JsonPropertyName("blockThumb")]
    public required string BlockThumb { get; set; }

    /// <summary>Gets or sets the horizontal line-track glyph.</summary>
    [JsonPropertyName("horizontalLineTrack")]
    public required string HorizontalLineTrack { get; set; }

    /// <summary>Gets or sets the horizontal line-thumb glyph.</summary>
    [JsonPropertyName("horizontalLineThumb")]
    public required string HorizontalLineThumb { get; set; }

    /// <summary>Gets or sets the vertical line-track glyph.</summary>
    [JsonPropertyName("verticalLineTrack")]
    public required string VerticalLineTrack { get; set; }

    /// <summary>Gets or sets the vertical line-thumb glyph.</summary>
    [JsonPropertyName("verticalLineThumb")]
    public required string VerticalLineThumb { get; set; }
}
