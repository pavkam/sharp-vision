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
    /// <returns>The concrete color; <see cref="Color.Default"/> when a role is undefined by the theme.</returns>
    public static Color Resolve(Color color, Func<ColorRole, Color?> lookup)
    {
        if (color.Kind != ColorKind.Role)
        {
            return color;
        }

        // The loader guarantees every role is defined; Default is a safe last resort if not.
        return lookup((ColorRole) color.RoleId) ?? Color.Default;
    }
}
