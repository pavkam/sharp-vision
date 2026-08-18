// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Reports a tree view item expansion-state transition.</summary>
[PublicAPI]
public sealed class ItemExpandedChangedEventArgs: EventArgs
{
    /// <summary>Initializes one committed expansion transition.</summary>
    /// <param name="isExpanded">Whether the item is now expanded.</param>
    public ItemExpandedChangedEventArgs(bool isExpanded) => IsExpanded = isExpanded;

    /// <summary>Gets whether the item is expanded after the transition.</summary>
    public bool IsExpanded { get; }
}
