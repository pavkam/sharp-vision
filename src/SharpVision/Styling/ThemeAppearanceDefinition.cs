// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json.Serialization;

/// <summary>Defines optional face, border, and shadow members in a theme document.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ThemeAppearanceDefinition
{
    /// <summary>Gets or sets the face contribution.</summary>
    [JsonPropertyName("face")]
    public FaceDefinition? Face { get; set; }

    /// <summary>Gets or sets the border contribution.</summary>
    [JsonPropertyName("border")]
    public BorderDefinition? Border { get; set; }

    /// <summary>Gets or sets the shadow contribution.</summary>
    [JsonPropertyName("shadow")]
    public ShadowDefinition? Shadow { get; set; }
}
