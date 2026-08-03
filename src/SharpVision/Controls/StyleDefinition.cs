// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using Styling;

/// <summary>Defines immutable resolution and invalidation policy for one complete control style.</summary>
/// <typeparam name="TStyle">The small immutable complete style value.</typeparam>
[PublicAPI]
public sealed class StyleDefinition<TStyle>
    where TStyle : struct, IEquatable<TStyle>
{
    internal StyleDefinition(
        ThemeRole? role,
        Func<TStyle?, Theme?, TStyle> resolve,
        Func<TStyle, ThemeProfile>? appearance,
        Func<TStyle, Theme?, TStyle, Theme?, InvalidationImpact> compare)
    {
        Role = role;
        Resolve = resolve;
        Appearance = appearance;
        Compare = compare;
    }

    /// <summary>Gets whether this definition can own a control's semantic appearance.</summary>
    internal bool IsControl => Role.HasValue && Appearance is not null;

    /// <summary>Gets the semantic role, or null for a secondary definition.</summary>
    internal ThemeRole? Role { get; }

    /// <summary>Gets the complete-style resolver.</summary>
    internal Func<TStyle?, Theme?, TStyle> Resolve { get; }

    /// <summary>Gets the appearance selector, or null for a secondary definition.</summary>
    internal Func<TStyle, ThemeProfile>? Appearance { get; }

    /// <summary>Gets the exact structural comparison policy.</summary>
    internal Func<TStyle, Theme?, TStyle, Theme?, InvalidationImpact> Compare { get; }
}
