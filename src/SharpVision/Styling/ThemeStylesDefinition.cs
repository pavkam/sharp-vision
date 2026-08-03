// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Defines fixed semantic profiles, plus any registrable style sections, in a theme document.</summary>
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

    /// <summary>Gets or sets every sibling key under <c>styles</c> that is not one of the five
    /// fixed profile names above - retained raw so a registrable style section (see #155) can be
    /// bound lazily by <see cref="Theme.GetStyleSection{TSection}"/> instead of being parsed eagerly
    /// into a fixed shape here.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Sections { get; set; }
}
