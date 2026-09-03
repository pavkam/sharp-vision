// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Reports one committed chart-point selection transition.</summary>
[PublicAPI]
public sealed class ChartSelectionChangedEventArgs: EventArgs
{
    /// <summary>Initializes one immutable chart selection transition.</summary>
    /// <param name="previousSelection">The selection before the transition, or null.</param>
    /// <param name="selection">The committed selection after the transition, or null.</param>
    public ChartSelectionChangedEventArgs(
        ChartSelection? previousSelection,
        ChartSelection? selection)
    {
        PreviousSelection = previousSelection;
        Selection = selection;
    }

    /// <summary>Gets the selection before the transition, or null.</summary>
    public ChartSelection? PreviousSelection { get; }

    /// <summary>Gets the committed selection after the transition, or null.</summary>
    public ChartSelection? Selection { get; }
}
