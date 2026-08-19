// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Derives a theme document's <c>styles.*</c> section key from the style type that owns
/// that section, so the two can never drift apart.</summary>
/// <remarks>Before this existed, every style type hand-wrote its key as a string literal at its
/// <see cref="StyleDefinitions"/> call site, and <see cref="ThemeCatalog"/> repeated the same
/// literals in a second hand-maintained registry used to validate theme documents. The two had
/// already diverged: <c>ChartStyle</c> declared <c>"chart"</c> while the registry omitted it, so a
/// theme authoring <c>styles.chart</c> was rejected as unknown. Deriving the key from the type name
/// makes the type the single source of truth and removes the registry as a thing to maintain.
///
/// The rule is: drop a trailing <c>Style</c>, drop a leading <c>Theme</c>, then lower-case the first
/// character - so <c>ButtonStyle</c> owns <c>"button"</c>, <c>ScrollBarStyle</c> owns
/// <c>"scrollBar"</c>, and the well-known root <c>ControlStyle</c> owns <c>"control"</c>. Every
/// key that existed when this was introduced maps exactly, so no theme document changed.</remarks>
[PublicAPI]
public static class StyleKey
{
    /// <summary>Gets the <c>styles.*</c> section key owned by one style type.</summary>
    /// <typeparam name="TStyle">The style type that owns the section.</typeparam>
    /// <returns>The non-empty section key, computed once per type.</returns>
    [Pure]
    public static string Of<TStyle>()
        where TStyle : ControlStyle => Cache<TStyle>.Key;

    /// <summary>Gets the <c>styles.*</c> section key owned by one style type.</summary>
    /// <param name="styleType">The style type that owns the section.</param>
    /// <returns>The non-empty section key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="styleType"/> is <see langword="null"/>.</exception>
    [Pure]
    internal static string Of(Type styleType)
    {
        ArgumentNullException.ThrowIfNull(styleType);
        return Derive(styleType.Name);
    }

    // Both affixes are stripped only when something is left over, so a type named exactly "Style"
    // or exactly "ThemeStyle" still yields a usable key instead of an empty one.
    private static string Derive(string typeName)
    {
        var name = typeName.AsSpan();

        if (name.Length > "Style".Length && name.EndsWith("Style", StringComparison.Ordinal))
        {
            name = name[..^"Style".Length];
        }

        if (name.Length > "Theme".Length && name.StartsWith("Theme", StringComparison.Ordinal))
        {
            name = name["Theme".Length..];
        }

        return string.Create(name.Length, name.ToString(), static (destination, source) =>
        {
            source.AsSpan().CopyTo(destination);
            destination[0] = char.ToLowerInvariant(destination[0]);
        });
    }

    private static class Cache<TStyle>
        where TStyle : ControlStyle
    {
        internal static readonly string Key = Derive(typeof(TStyle).Name);
    }
}
