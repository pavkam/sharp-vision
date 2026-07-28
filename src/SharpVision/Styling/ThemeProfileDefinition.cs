// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json.Serialization;

/// <summary>Defines one reusable partial appearance profile in a theme document.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ThemeProfileDefinition
{
    /// <summary>Gets or sets the partial normal appearance contribution.</summary>
    [JsonPropertyName("normal")]
    public ThemeAppearanceDefinition? Normal { get; set; }

    /// <summary>Gets or sets the pointer-over contribution.</summary>
    [JsonPropertyName("pointerOver")]
    public ThemeAppearanceDefinition? PointerOver { get; set; }

    /// <summary>Gets or sets the descendant-focus contribution.</summary>
    [JsonPropertyName("focusWithin")]
    public ThemeAppearanceDefinition? FocusWithin { get; set; }

    /// <summary>Gets or sets the direct-focus contribution.</summary>
    [JsonPropertyName("focused")]
    public ThemeAppearanceDefinition? Focused { get; set; }

    /// <summary>Gets or sets the current-item contribution.</summary>
    [JsonPropertyName("current")]
    public ThemeAppearanceDefinition? Current { get; set; }

    /// <summary>Gets or sets the selected-item contribution.</summary>
    [JsonPropertyName("selected")]
    public ThemeAppearanceDefinition? Selected { get; set; }

    /// <summary>Gets or sets the checked contribution.</summary>
    [JsonPropertyName("checked")]
    public ThemeAppearanceDefinition? Checked { get; set; }

    /// <summary>Gets or sets the indeterminate contribution.</summary>
    [JsonPropertyName("indeterminate")]
    public ThemeAppearanceDefinition? Indeterminate { get; set; }

    /// <summary>Gets or sets the pressed contribution.</summary>
    [JsonPropertyName("pressed")]
    public ThemeAppearanceDefinition? Pressed { get; set; }

    /// <summary>Gets or sets the disabled contribution.</summary>
    [JsonPropertyName("disabled")]
    public ThemeAppearanceDefinition? Disabled { get; set; }
}
