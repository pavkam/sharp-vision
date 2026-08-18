// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Reports an expander expansion-state transition.</summary>
[PublicAPI]
public sealed class ExpandedChangedEventArgs: EventArgs
{
    /// <summary>Initializes one committed expansion transition.</summary>
    /// <param name="isExpanded">Whether the expander is now expanded.</param>
    public ExpandedChangedEventArgs(bool isExpanded) => IsExpanded = isExpanded;

    /// <summary>Gets whether the expander is expanded after the transition.</summary>
    public bool IsExpanded { get; }
}
