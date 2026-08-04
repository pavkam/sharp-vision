// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Text.Json.Serialization;

/// <summary>Defines the registrable "checkBox" theme-file style section (see #155). Named enum
/// values are parsed by hand, matching every other theme-JSON enum field in this codebase.</summary>
internal sealed class CheckBoxStyleSection
{
    /// <summary>Gets or sets the named <see cref="CheckBoxMarkStyle"/> value ("square", "brackets", or "tick").</summary>
    [JsonPropertyName("markStyle")]
    public string? MarkStyle { get; set; }

    /// <summary>Gets or sets the complete unchecked, checked, and indeterminate glyph family.</summary>
    [JsonPropertyName("glyphs")]
    public CheckBoxGlyphsSection? Glyphs { get; set; }
}

/// <summary>Defines the "checkBox.glyphs" theme-file style section (see #155). Every member is one
/// printable one-cell Rune string, matching <see cref="CheckBoxGlyphs"/>'s own members.</summary>
internal sealed class CheckBoxGlyphsSection
{
    /// <summary>Gets or sets the glyph rendered when the CheckBox is unchecked.</summary>
    [JsonPropertyName("unchecked")]
    public string? Unchecked { get; set; }

    /// <summary>Gets or sets the glyph rendered when the CheckBox is checked.</summary>
    [JsonPropertyName("checked")]
    public string? Checked { get; set; }

    /// <summary>Gets or sets the glyph rendered when the CheckBox is indeterminate.</summary>
    [JsonPropertyName("indeterminate")]
    public string? Indeterminate { get; set; }
}
