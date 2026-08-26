// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

/// <summary>Provides a custom ordered collection member for style equality tests.</summary>
internal sealed record SequenceStyle: ControlStyle
{
    /// <summary>Gets the aggregate definition that reallocates equivalent content per Theme.</summary>
    internal static StyleDefinition<SequenceStyle> Definition { get; } = StyleDefinitions.Aggregate(
        static _ => new SequenceStyle(
            DefaultFace,
            NoBorder,
            NoShadow,
            [1, 2, 3]),
        static (_, _, _, _) => InvalidationImpact.None);

    /// <summary>Initializes the sequence style.</summary>
    /// <param name="face">The normal face.</param>
    /// <param name="border">The normal border.</param>
    /// <param name="shadow">The normal shadow.</param>
    /// <param name="values">The ordered visual values.</param>
    [SetsRequiredMembers]
    public SequenceStyle(
        Face face,
        Border border,
        Shadow shadow,
        ImmutableArray<int> values) : base(face, border, shadow) =>
        Values = values;

    /// <summary>Gets the ordered visual values.</summary>
    public required ImmutableArray<int> Values { get; init; }
}
