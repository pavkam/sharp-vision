// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using SharpVision.Terminal.Protocols;

/// <summary>Collapses a deferred role color to a concrete color against a theme palette.</summary>
internal static class SemanticColor
{
    /// <summary>Resolves a role color to its concrete palette value; passes other colors through.</summary>
    /// <param name="color">The candidate color.</param>
    /// <param name="lookup">The palette lookup for a role, returning null when undefined.</param>
    /// <returns>
    /// The concrete color; <see cref="Color.Default"/> when the color carries a defined role the theme
    /// happens not to set.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="color"/> is a role color carrying an id that is not a defined <see cref="ColorRole"/>.
    /// </exception>
    public static Color Resolve(Color color, Func<ColorRole, Color?> lookup)
    {
        if (color.Kind != ColorKind.Role)
        {
            return color;
        }

        ColorRole role = (ColorRole) color.RoleId;

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentException(
                $"The color carries an unknown role id {color.RoleId}; use a ThemeColors.* value.",
                nameof(color));
        }

        // A defined role the theme happens not to set falls back to Default (loader normally fills all roles).
        return lookup(role) ?? Color.Default;
    }
}
