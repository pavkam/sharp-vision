// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using Styling;

/// <summary>Creates immutable primary and secondary complete-style definitions.</summary>
[PublicAPI]
public static class StyleDefinitions
{
    /// <summary>Creates a primary control-style definition completed by one semantic Theme role.</summary>
    /// <typeparam name="TStyle">The small immutable complete style value.</typeparam>
    /// <param name="role">The defined semantic role.</param>
    /// <param name="complete">Completes code-owned mechanics with the resolved semantic profile.</param>
    /// <param name="appearance">Selects the complete appearance profile from a resolved style.</param>
    /// <param name="compare">Returns the earliest phase affected by structural or directly themed members.</param>
    /// <returns>An immutable primary-style definition.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="role"/> is unknown.</exception>
    /// <exception cref="ArgumentNullException">A delegate is null.</exception>
    public static StyleDefinition<TStyle> Control<TStyle>(
        ThemeRole role,
        Func<ThemeProfile, TStyle> complete,
        Func<TStyle, ThemeProfile> appearance,
        Func<TStyle, Theme?, TStyle, Theme?, InvalidationImpact> compare)
        where TStyle : struct, IEquatable<TStyle>
    {
        role.ValidateDefined();
        ArgumentNullException.ThrowIfNull(complete);
        ArgumentNullException.ThrowIfNull(appearance);
        ArgumentNullException.ThrowIfNull(compare);
        return new StyleDefinition<TStyle>(
            role,
            (local, theme) => local ?? complete((theme ?? Themes.Dark).GetProfile(role)),
            appearance,
            compare);
    }

    /// <summary>Creates a secondary style definition that does not own its control's appearance profile.</summary>
    /// <typeparam name="TStyle">The small immutable complete style value.</typeparam>
    /// <param name="fallback">Resolves the complete fallback for one inherited Theme.</param>
    /// <param name="compare">Returns the earliest phase affected by resolved members.</param>
    /// <returns>An immutable secondary-style definition.</returns>
    /// <exception cref="ArgumentNullException">A delegate is null.</exception>
    public static StyleDefinition<TStyle> Part<TStyle>(
        Func<Theme?, TStyle> fallback,
        Func<TStyle, Theme?, TStyle, Theme?, InvalidationImpact> compare)
        where TStyle : struct, IEquatable<TStyle>
    {
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(compare);
        return new StyleDefinition<TStyle>(null, (local, theme) => local ?? fallback(theme), null, compare);
    }
}
