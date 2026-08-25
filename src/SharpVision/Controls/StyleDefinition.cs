// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using Styling;

/// <summary>Defines immutable resolution and invalidation policy for one complete control style.
/// A leaf control style declares one-hop fallback (via <see cref="StyleDefinitions"/>) to inherit
/// theme customization from one of the six well-known roots; it authors no <c>styles.*</c> theme
/// section of its own.</summary>
/// <typeparam name="TStyle">The immutable complete style value.</typeparam>
[PublicAPI]
public sealed class StyleDefinition<TStyle>
    where TStyle : ControlStyle
{
    internal StyleDefinition(
        Func<TStyle?, Theme?, TStyle> resolve,
        Func<TStyle, Theme?, AppearanceStates>? appearance,
        Func<TStyle, Theme?, TStyle, Theme?, InvalidationImpact> compare,
        StyleDefinitionKind kind = StyleDefinitionKind.Control,
        Func<TStyle, Theme?, AppearanceStates>? localAppearance = null)
    {
        Resolve = resolve;
        Appearance = appearance;
        Compare = compare;
        Kind = kind;
        LocalAppearance = localAppearance ?? appearance;
    }

    /// <summary>Gets whether this definition can own a control's semantic appearance.</summary>
    internal bool IsControl => Kind == StyleDefinitionKind.Control;

    /// <summary>Gets the slot role this definition is valid to initialize.</summary>
    internal StyleDefinitionKind Kind { get; }

    /// <summary>Gets the complete-style resolver.</summary>
    internal Func<TStyle?, Theme?, TStyle> Resolve { get; }

    /// <summary>Gets the appearance selector, or null for a secondary definition.</summary>
    internal Func<TStyle, Theme?, AppearanceStates>? Appearance { get; }

    /// <summary>Gets the appearance selector used after a complete local style wins Theme states.</summary>
    internal Func<TStyle, Theme?, AppearanceStates>? LocalAppearance { get; }

    /// <summary>Gets the exact structural comparison policy.</summary>
    internal Func<TStyle, Theme?, TStyle, Theme?, InvalidationImpact> Compare { get; }
}
