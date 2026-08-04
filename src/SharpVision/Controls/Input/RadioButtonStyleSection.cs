// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Text.Json.Serialization;

/// <summary>Defines the registrable "radioButton" theme-file style section (see #155). Named enum
/// values are parsed by hand, matching every other theme-JSON enum field in this codebase.</summary>
internal sealed class RadioButtonStyleSection
{
    /// <summary>Gets or sets the named <see cref="RadioButtonMarkStyle"/> value ("circle" or "parentheses").</summary>
    [JsonPropertyName("markStyle")]
    public string? MarkStyle { get; set; }

    /// <summary>Gets or sets the complete unchecked and checked glyph pair.</summary>
    [JsonPropertyName("glyphs")]
    public RadioButtonGlyphsSection? Glyphs { get; set; }
}

/// <summary>Defines the "radioButton.glyphs" theme-file style section (see #155). Every member is
/// one printable one-cell Rune string, matching <see cref="RadioButtonGlyphs"/>'s own members.</summary>
internal sealed class RadioButtonGlyphsSection
{
    /// <summary>Gets or sets the glyph rendered when the radio button is unchecked.</summary>
    [JsonPropertyName("unchecked")]
    public string? Unchecked { get; set; }

    /// <summary>Gets or sets the glyph rendered when the radio button is checked.</summary>
    [JsonPropertyName("checked")]
    public string? Checked { get; set; }
}
