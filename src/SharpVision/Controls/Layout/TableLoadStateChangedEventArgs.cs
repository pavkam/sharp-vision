// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Describes one committed <see cref="Table.LoadState"/> transition.</summary>
public sealed class TableLoadStateChangedEventArgs: EventArgs
{
    /// <summary>Initializes a validated load-state transition snapshot.</summary>
    /// <param name="previousState">The state before the transition.</param>
    /// <param name="state">The committed state.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="previousState"/> or <paramref name="state"/> is undefined.
    /// </exception>
    public TableLoadStateChangedEventArgs(TableLoadState previousState, TableLoadState state)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(previousState, nameof(previousState), "The enum value is unknown.");
        ArgumentOutOfRangeException.ThrowIfNotDefined(state, nameof(state), "The enum value is unknown.");
        PreviousState = previousState;
        State = state;
    }

    /// <summary>Gets the state before the transition.</summary>
    public TableLoadState PreviousState { get; }

    /// <summary>Gets the committed state.</summary>
    public TableLoadState State { get; }
}
