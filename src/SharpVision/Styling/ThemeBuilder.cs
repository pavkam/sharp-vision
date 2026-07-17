// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Builds a palette-only immutable theme.</summary>
internal static class ThemeBuilder
{
    /// <summary>Builds a frozen theme from a complete semantic palette.</summary>
    internal static Theme Build(IReadOnlyDictionary<ColorRole, Color> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var theme = new Theme();
        foreach (var role in Enum.GetValues<ColorRole>())
        {
            theme.SetColor(role, roles[role]);
        }

        theme.Freeze();
        return theme;
    }
}
