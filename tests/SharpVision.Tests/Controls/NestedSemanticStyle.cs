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

    /// <summary>Gets a computed self-reference that must never participate in stored-style comparison.</summary>
    public NestedSemanticStyle Self => this;

    /// <summary>Gets a computed value that proves style comparison does not invoke arbitrary getters.</summary>
    public int Throwing
    {
        get
        {
            _ = Swatch;
            throw new InvalidOperationException("Computed style getters are not comparison data.");
        }
    }

    /// <summary>Gets a computed indexed value that proves comparison never invokes indexers.</summary>
    public int this[int index]
    {
        get
        {
            _ = Swatch;
            throw new InvalidOperationException($"Indexer {index} is not comparison data.");
        }
    }
}
