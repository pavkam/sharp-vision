// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Reports an atomic calendar selection transition.</summary>
[PublicAPI]
public sealed class CalendarSelectionChangedEventArgs: EventArgs
{
    /// <summary>Initializes one committed selection transition.</summary>
    /// <param name="previousSelection">The previous selection, or null.</param>
    /// <param name="selection">The current selection, or null.</param>
    public CalendarSelectionChangedEventArgs(
        DateInterval? previousSelection,
        DateInterval? selection)
    {
        PreviousSelection = previousSelection;
        Selection = selection;
    }

    /// <summary>Gets the selection before the transition, or null.</summary>
    public DateInterval? PreviousSelection { get; }

    /// <summary>Gets the committed selection after the transition, or null.</summary>
    public DateInterval? Selection { get; }
}
