// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using Styling;

/// <summary>Defines immutable resolution and invalidation policy for one complete control style.
/// A style declares its own
/// <c>styles.*</c> key (via <see cref="StyleDefinitions"/>) and, for every type but the six
/// well-known roots, a one-hop fallback to inherit theme customization from.</summary>
/// <typeparam name="TStyle">The immutable complete style value.</typeparam>
[PublicAPI]
public sealed class StyleDefinition<TStyle>: IStyleDefinition
    where TStyle : ControlStyle
{
    internal StyleDefinition(
        Func<TStyle?, Theme?, TStyle> resolve,
        Func<TStyle, Theme?, AppearanceStates>? appearance,
        Func<TStyle, Theme?, TStyle, Theme?, InvalidationImpact> compare)
    {
        Resolve = resolve;
        Appearance = appearance;
        Compare = compare;
    }

    /// <summary>Gets whether this definition can own a control's semantic appearance.</summary>
    internal bool IsControl => Appearance is not null;

    // Explicit because IsControl is internal, and an implicit implementation would have to be
    // public - widening the surface for the sake of one reflective reader.
    bool IStyleDefinition.IsControl => IsControl;

    /// <summary>Gets the complete-style resolver.</summary>
    internal Func<TStyle?, Theme?, TStyle> Resolve { get; }

    /// <summary>Gets the appearance selector, or null for a secondary definition.</summary>
    internal Func<TStyle, Theme?, AppearanceStates>? Appearance { get; }

    /// <summary>Gets the exact structural comparison policy.</summary>
    internal Func<TStyle, Theme?, TStyle, Theme?, InvalidationImpact> Compare { get; }
}
