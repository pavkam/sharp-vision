// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Scrolling;

using System.Text.Json.Serialization;

/// <summary>Defines the registrable "scrollBar" theme-file style section (see #155). Named enum
/// values are parsed by hand, matching every other theme-JSON enum field in this codebase.</summary>
internal sealed class ScrollBarStyleSection
{
    /// <summary>Gets or sets the named <see cref="ScrollBarChrome"/> value ("thin" or "full").</summary>
    [JsonPropertyName("chrome")]
    public string? Chrome { get; set; }

    /// <summary>Gets or sets the named <see cref="ScrollBarFill"/> value ("line" or "block").</summary>
    [JsonPropertyName("fill")]
    public string? Fill { get; set; }

    /// <summary>Gets or sets the complete button, track, and thumb glyph family.</summary>
    [JsonPropertyName("glyphs")]
    public ScrollBarGlyphsSection? Glyphs { get; set; }
}

/// <summary>Defines the "scrollBar.glyphs" theme-file style section (see #155). Every member is one
/// printable one-cell Rune string, matching <see cref="ScrollBarGlyphs"/>'s own members.</summary>
internal sealed class ScrollBarGlyphsSection
{
    /// <summary>Gets or sets the vertical decrement button.</summary>
    [JsonPropertyName("verticalDecrement")]
    public string? VerticalDecrement { get; set; }

    /// <summary>Gets or sets the vertical increment button.</summary>
    [JsonPropertyName("verticalIncrement")]
    public string? VerticalIncrement { get; set; }

    /// <summary>Gets or sets the horizontal decrement button.</summary>
    [JsonPropertyName("horizontalDecrement")]
    public string? HorizontalDecrement { get; set; }

    /// <summary>Gets or sets the horizontal increment button.</summary>
    [JsonPropertyName("horizontalIncrement")]
    public string? HorizontalIncrement { get; set; }

    /// <summary>Gets or sets the block-fill track.</summary>
    [JsonPropertyName("blockTrack")]
    public string? BlockTrack { get; set; }

    /// <summary>Gets or sets the block-fill thumb.</summary>
    [JsonPropertyName("blockThumb")]
    public string? BlockThumb { get; set; }

    /// <summary>Gets or sets the horizontal line track.</summary>
    [JsonPropertyName("horizontalLineTrack")]
    public string? HorizontalLineTrack { get; set; }

    /// <summary>Gets or sets the horizontal line thumb.</summary>
    [JsonPropertyName("horizontalLineThumb")]
    public string? HorizontalLineThumb { get; set; }

    /// <summary>Gets or sets the vertical line track.</summary>
    [JsonPropertyName("verticalLineTrack")]
    public string? VerticalLineTrack { get; set; }

    /// <summary>Gets or sets the vertical line thumb.</summary>
    [JsonPropertyName("verticalLineThumb")]
    public string? VerticalLineThumb { get; set; }
}
