// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using System.Diagnostics.CodeAnalysis;

/// <summary>Models an aggregate third-party style whose only visual member is a semantic color.</summary>
public sealed record SemanticColorStyle: ControlStyle
{
    /// <summary>Gets the default semantic style.</summary>
    public static new SemanticColorStyle Default { get; } = new(SemanticColor.Accent);

    /// <summary>Gets a definition whose custom comparer deliberately owns no color logic.</summary>
    public static StyleDefinition<SemanticColorStyle> Definition { get; } = StyleDefinitions.Aggregate(
        static _ => Default,
        static (_, _, _, _) => InvalidationImpact.None);

    /// <summary>Initializes a semantic-color style.</summary>
    /// <param name="fillColor">The rendered fill foreground.</param>
    [SetsRequiredMembers]
    public SemanticColorStyle(ControlColor fillColor) : base(DefaultFace, NoBorder, NoShadow) =>
        FillColor = fillColor;

    /// <summary>Gets the semantic fill foreground.</summary>
    public required ControlColor FillColor { get; init; }

}
