// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Derives the <c>styles.*</c> section key owned by one of the six well-known base style
/// types from its type name, so the two can never drift apart.</summary>
/// <remarks>Before theme sections closed to exactly the six well-known styles, every style type -
/// leaf and root alike - hand-wrote its key as a string literal at its <see cref="StyleDefinitions"/>
/// call site, and <see cref="ThemeCatalog"/> repeated the same literals in a second hand-maintained
/// registry used to validate theme documents. The two had already diverged: <c>ChartStyle</c>
/// declared <c>"chart"</c> while the registry omitted it, so a theme authoring <c>styles.chart</c>
/// was rejected as unknown. Only the six well-known roots resolve a <c>styles.*</c> section at all
/// now, but this derivation remains the single source of truth for their own keys and for the
/// dotted diagnostic paths built from them.
///
/// <para>The rule is: drop a trailing <c>Style</c>, drop a leading <c>Theme</c>, then lower-case the
/// first character - so <c>ControlStyle</c> owns <c>"control"</c> and <c>ScrollBarStyle</c> would
/// own <c>"scrollBar"</c> were it ever resolved through this path (it is not: a leaf resolves no
/// section of its own). Every key that existed when this was introduced maps exactly, so no theme
/// document changed.</para></remarks>
internal static class StyleKey
{
    /// <summary>Gets the <c>styles.*</c> section key owned by one style type.</summary>
    /// <typeparam name="TStyle">The style type that owns the section.</typeparam>
    /// <returns>The non-empty section key, computed once per type.</returns>
    [Pure]
    internal static string Of<TStyle>()
        where TStyle : ControlStyle => Cache<TStyle>.Key;

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
