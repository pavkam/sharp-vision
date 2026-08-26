// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using System.Diagnostics.CodeAnalysis;

/// <summary>Models a style containing semantic paint inside an ordinary nested record.</summary>
public sealed record NestedSemanticStyle: ControlStyle
{
    /// <summary>Gets the aggregate definition.</summary>
    public static StyleDefinition<NestedSemanticStyle> Definition { get; } = StyleDefinitions.Aggregate(
        static _ => new NestedSemanticStyle(new NestedSwatch(SemanticColor.Accent)),
        static (_, _, _, _) => InvalidationImpact.None);

    /// <summary>Initializes the nested semantic style.</summary>
    /// <param name="swatch">The nested swatch.</param>
    [SetsRequiredMembers]
    public NestedSemanticStyle(NestedSwatch swatch) : base(DefaultFace, NoBorder, NoShadow) => Swatch = swatch;

    /// <summary>Gets the nested swatch.</summary>
    public required NestedSwatch Swatch { get; init; }
}
