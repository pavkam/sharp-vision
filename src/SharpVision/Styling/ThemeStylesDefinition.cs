// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json.Serialization;

/// <summary>Defines fixed semantic profiles in a theme document.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ThemeStylesDefinition
{
    /// <summary>Gets or sets the passive base-control profile.</summary>
    [JsonPropertyName("control")]
    public ThemeProfileDefinition? Control { get; set; }

    /// <summary>Gets or sets the editable or selectable input profile.</summary>
    [JsonPropertyName("input")]
    public ThemeProfileDefinition? Input { get; set; }

    /// <summary>Gets or sets the framed container profile.</summary>
    [JsonPropertyName("container")]
    public ThemeProfileDefinition? Container { get; set; }

    /// <summary>Gets or sets the top-level window profile.</summary>
    [JsonPropertyName("window")]
    public ThemeProfileDefinition? Window { get; set; }

    /// <summary>Gets or sets the transient popup profile.</summary>
    [JsonPropertyName("popup")]
    public ThemeProfileDefinition? Popup { get; set; }

}
