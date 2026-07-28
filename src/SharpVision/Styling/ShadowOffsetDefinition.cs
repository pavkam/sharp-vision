// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json.Serialization;

/// <summary>Defines one signed shadow offset in a theme document.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ShadowOffsetDefinition
{
    /// <summary>Gets or sets the horizontal cell offset.</summary>
    [JsonPropertyName("x")]
    public int X { get; set; }

    /// <summary>Gets or sets the vertical row or half-row offset.</summary>
    [JsonPropertyName("y")]
    public int Y { get; set; }
}
