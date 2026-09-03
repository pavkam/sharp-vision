// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

/// <summary>Describes a committed change to the semantic location represented by a breadcrumb.</summary>
[PublicAPI]
public sealed class BreadcrumbCurrentChangedEventArgs: EventArgs
{
    /// <summary>Initializes one immutable current-location transition.</summary>
    /// <param name="previousItem">The previously represented item, or null.</param>
    /// <param name="currentItem">The newly represented item, or null.</param>
    public BreadcrumbCurrentChangedEventArgs(BreadcrumbItem? previousItem, BreadcrumbItem? currentItem)
    {
        PreviousItem = previousItem;
        CurrentItem = currentItem;
    }

    /// <summary>Gets the previously represented item, or null.</summary>
    public BreadcrumbItem? PreviousItem { get; }

    /// <summary>Gets the newly represented item, or null.</summary>
    public BreadcrumbItem? CurrentItem { get; }
}
