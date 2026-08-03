// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Reports one committed TabControl selection transition.</summary>
[PublicAPI]
public sealed class TabSelectionChangedEventArgs: EventArgs
{
    /// <summary>Initializes one immutable tab selection transition.</summary>
    /// <param name="previousIndex">The index before the transition, or -1.</param>
    /// <param name="currentIndex">The committed index, or -1.</param>
    /// <param name="previousItem">The page selected before the transition, or null.</param>
    /// <param name="currentItem">The page selected after the transition, or null.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="previousIndex"/> or <paramref name="currentIndex"/> is less than -1.
    /// </exception>
    public TabSelectionChangedEventArgs(
        int previousIndex,
        int currentIndex,
        TabItem? previousItem,
        TabItem? currentItem)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(previousIndex, -1);
        ArgumentOutOfRangeException.ThrowIfLessThan(currentIndex, -1);

        PreviousIndex = previousIndex;
        CurrentIndex = currentIndex;
        PreviousItem = previousItem;
        CurrentItem = currentItem;
    }

    /// <summary>Gets the index before the transition, or -1.</summary>
    public int PreviousIndex { get; }

    /// <summary>Gets the committed index, or -1.</summary>
    public int CurrentIndex { get; }

    /// <summary>Gets the page selected before the transition, or null.</summary>
    public TabItem? PreviousItem { get; }

    /// <summary>Gets the page selected after the transition, or null.</summary>
    public TabItem? CurrentItem { get; }
}
