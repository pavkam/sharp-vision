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
    /// <param name="cause">The defined transition cause.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is unknown.</exception>
    public NavigationViewSelectionChangedEventArgs(
        NavigationViewItem? previousItem,
        NavigationViewItem? currentItem,
        ActivationCause cause)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(cause, nameof(cause), "The activation cause is unknown.");

        PreviousItem = previousItem;
        CurrentItem = currentItem;
        Cause = cause;
    }

    /// <summary>Gets the item before the transition, or null.</summary>
    public NavigationViewItem? PreviousItem { get; }

    /// <summary>Gets the committed item, or null.</summary>
    public NavigationViewItem? CurrentItem { get; }

    /// <summary>Gets the transition input path.</summary>
    public ActivationCause Cause { get; }
}
