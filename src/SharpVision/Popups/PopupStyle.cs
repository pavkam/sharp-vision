// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Popups;

/// <summary>
/// Independently overridable popup frame chrome, shared by every control that owns a retained
/// <see cref="Popup"/> (drop-downs, date-field calendars, submenus, and context menus).
/// </summary>
/// <remarks>
/// A null component keeps that part on the surface's own <see cref="ThemeRole.Popup"/> role
/// appearance; only the components actually set locally are ever authored (see #81).
/// </remarks>
[PublicAPI]
public readonly record struct PopupStyle
{
    /// <summary>Gets the complete locally authored border, or null to keep Theme ownership.</summary>
    public Border? Border { get; init; }

    /// <summary>Gets the complete locally authored shadow, or null to keep Theme ownership.</summary>
    public Shadow? Shadow { get; init; }
}
