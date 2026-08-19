// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>Reports one committed TabControl selection transition.</summary>
[PublicAPI]
public sealed class TabSelectionChangedEventArgs: EventArgs
{
    /// <summary>Initializes one immutable tab selection transition.</summary>
    /// <param name="previousIndex">The index before the transition, or -1.</param>
    /// <param name="currentIndex">The committed index, or -1.</param>
    /// <param name="previousItem">The page selected before the transition, or null.</param>
    /// <param name="currentItem">The page selected after the transition, or null.</param>
    /// <exception cref="ArgumentException">
    /// An item is null for a selected index or present for the -1 sentinel.
    /// </exception>
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
        ValidateItem(previousIndex, previousItem, nameof(previousItem));
        ValidateItem(currentIndex, currentItem, nameof(currentItem));

        PreviousIndex = previousIndex;
        CurrentIndex = currentIndex;
        PreviousItem = previousItem;
        CurrentItem = currentItem;
    }

    /// <summary>Gets the index before the transition, or -1.</summary>
    [ValueRange(-1, int.MaxValue)]
    public int PreviousIndex { get; }

    /// <summary>Gets the committed index, or -1.</summary>
    [ValueRange(-1, int.MaxValue)]
    public int CurrentIndex { get; }

    /// <summary>Gets the page selected before the transition, or null.</summary>
    public TabItem? PreviousItem { get; }

    /// <summary>Gets the page selected after the transition, or null.</summary>
    public TabItem? CurrentItem { get; }

    private static void ValidateItem(int index, TabItem? item, string parameterName)
    {
        if (index == -1 != (item is null))
        {
            throw new ArgumentException("The tab item presence must agree with its selection index.", parameterName);
        }
    }
}
