// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Models an ordinary immutable nested style value with semantic paint.</summary>
public sealed record NestedSwatch
{
    /// <summary>Initializes the nested swatch.</summary>
    /// <param name="color">The semantic or literal swatch color.</param>
    public NestedSwatch(ControlColor color) => Color = color;

    /// <summary>Gets the swatch color.</summary>
    public ControlColor Color { get; }
}
