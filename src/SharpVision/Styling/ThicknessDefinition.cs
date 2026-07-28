// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json.Serialization;

/// <summary>Defines four required physical box edges in a theme document.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ThicknessDefinition
{
    /// <summary>Gets or sets the required left edge in terminal cells.</summary>
    [JsonPropertyName("left")]
    public required int Left { get; set; }

    /// <summary>Gets or sets the required top edge in terminal cells.</summary>
    [JsonPropertyName("top")]
    public required int Top { get; set; }

    /// <summary>Gets or sets the required right edge in terminal cells.</summary>
    [JsonPropertyName("right")]
    public required int Right { get; set; }

    /// <summary>Gets or sets the required bottom edge in terminal cells.</summary>
    [JsonPropertyName("bottom")]
    public required int Bottom { get; set; }

    /// <summary>Creates the validated physical box value represented by this definition.</summary>
    /// <returns>The complete non-negative physical box edges.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An authored edge is negative.</exception>
    /// <exception cref="OverflowException">An opposing-edge sum exceeds an integer.</exception>
    internal Thickness ToThickness() => new(Left, Top, Right, Bottom);
}
