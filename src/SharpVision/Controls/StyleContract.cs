// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using Styling;

/// <summary>
/// Describes how one styled control resolves, structurally compares, and selects the
/// appearance profile of its complete style, so <see cref="Control"/> can supply the whole
/// theming contract from one instance instead of each control hand-wiring the same overrides.
/// </summary>
/// <typeparam name="TStyle">The complete immutable style value.</typeparam>
internal sealed class StyleContract<TStyle>
    where TStyle : struct, IEquatable<TStyle>
{
    /// <summary>Initializes a complete style-resolution contract for one control.</summary>
    /// <param name="role">The semantic Theme role a default style falls back to.</param>
    /// <param name="withFallbackProfile">Builds the complete default style from the fallback profile.</param>
    /// <param name="compareStructure">
    /// Compares non-appearance structural and directly-resolved visual members of two styles,
    /// each under its own resolving Theme.
    /// </param>
    /// <param name="appearance">Selects the complete appearance profile owned by a style.</param>
    /// <exception cref="ArgumentNullException">A delegate is null.</exception>
    public StyleContract(
        ThemeRole role,
        Func<ThemeProfile, TStyle> withFallbackProfile,
        Func<TStyle, Theme?, TStyle, Theme?, InvalidationImpact> compareStructure,
        Func<TStyle, ThemeProfile> appearance)
    {
        ArgumentNullException.ThrowIfNull(withFallbackProfile);
        ArgumentNullException.ThrowIfNull(compareStructure);
        ArgumentNullException.ThrowIfNull(appearance);
        Role = role;
        WithFallbackProfile = withFallbackProfile;
        CompareStructure = compareStructure;
        Appearance = appearance;
    }

    /// <summary>Gets the semantic Theme role a default style falls back to.</summary>
    public ThemeRole Role { get; }

    /// <summary>Gets the delegate building the complete default style from a fallback profile.</summary>
    public Func<ThemeProfile, TStyle> WithFallbackProfile { get; }

    /// <summary>Gets the delegate comparing two resolved styles, each under its own Theme.</summary>
    public Func<TStyle, Theme?, TStyle, Theme?, InvalidationImpact> CompareStructure { get; }

    /// <summary>Gets the delegate selecting the complete appearance profile owned by a style.</summary>
    public Func<TStyle, ThemeProfile> Appearance { get; }

    /// <summary>Resolves the complete style from an explicit local value and an inherited Theme.</summary>
    /// <param name="local">The explicit local style, or null for Theme ownership.</param>
    /// <param name="theme">The inherited Theme, or null for the library fallback.</param>
    /// <returns>The complete resolved style.</returns>
    public TStyle Resolve(TStyle? local, Theme? theme) =>
        local ?? WithFallbackProfile((theme ?? Themes.Dark).GetProfile(Role));
}
