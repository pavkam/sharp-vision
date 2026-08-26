// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

/// <summary>
/// Captures whether a <see cref="NavigationView"/>'s current-navigation or
/// selected item is the root being removed, or a descendant of it, before
/// detachment makes the ancestor walk impossible.
/// </summary>
internal readonly struct NavigationViewRemovalRepair
{
    /// <summary>Initializes an immutable pre-detachment removal snapshot.</summary>
    /// <param name="currentRemoved">Whether the current-navigation item was removed.</param>
    /// <param name="selectedRemoved">Whether the selected item was removed.</param>
    /// <param name="selectedIndex">The selected item's index in semantic item order before removal.</param>
    public NavigationViewRemovalRepair(bool currentRemoved, bool selectedRemoved, int selectedIndex)
    {
        IsCurrentRemoved = currentRemoved;
        IsSelectedRemoved = selectedRemoved;
        SelectedIndex = selectedIndex;
    }

    /// <summary>Gets whether the current-navigation item was removed.</summary>
    public bool IsCurrentRemoved { get; }

    /// <summary>Gets whether the selected item was removed.</summary>
    public bool IsSelectedRemoved { get; }

    /// <summary>Gets the selected item's index in semantic item order before removal.</summary>
    public int SelectedIndex { get; }
}
