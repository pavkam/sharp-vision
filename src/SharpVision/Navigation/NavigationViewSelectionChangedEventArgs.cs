// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

/// <summary>Reports one committed NavigationView selection transition.</summary>
[PublicAPI]
public sealed class NavigationViewSelectionChangedEventArgs: EventArgs
{
    /// <summary>Initializes one immutable selection transition.</summary>
    /// <param name="previousItem">The item before the transition, or null.</param>
    /// <param name="currentItem">The committed item, or null.</param>
    public NavigationViewSelectionChangedEventArgs(
        NavigationViewItem? previousItem,
        NavigationViewItem? currentItem)
    {
        PreviousItem = previousItem;
        CurrentItem = currentItem;
    }

    /// <summary>Gets the item before the transition, or null.</summary>
    public NavigationViewItem? PreviousItem { get; }

    /// <summary>Gets the committed item, or null.</summary>
    public NavigationViewItem? CurrentItem { get; }
}
