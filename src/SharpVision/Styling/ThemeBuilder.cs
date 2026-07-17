// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Builds a complete immutable theme.</summary>
internal static class ThemeBuilder
{
    /// <summary>Builds a frozen theme from resolved colors, glyphs, and metadata.</summary>
    internal static Theme Build(
        IReadOnlyDictionary<ColorRole, Color> roles,
        ThemeGlyphs glyphs,
        ThemeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(glyphs);
        ArgumentNullException.ThrowIfNull(definition);
        var theme = new Theme(
            glyphs,
            definition.Version,
            definition.Name ?? "Custom",
            definition.Slug ?? "custom",
            definition.ColorScheme == "light" ? ColorScheme.Light : ColorScheme.Dark,
            definition.Author ?? "SharpVision contributors",
            definition.License ?? "MIT",
            definition.Source ?? "https://github.com/sharpvision/sharpvision");
        foreach (var role in Enum.GetValues<ColorRole>())
        {
            theme.SetColor(role, roles[role]);
        }

        theme.Freeze();
        return theme;
    }
}
