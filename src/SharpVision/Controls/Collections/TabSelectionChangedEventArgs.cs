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
    public TabSelectionChangedEventArgs(int previousIndex, int currentIndex)
    {
        PreviousIndex = previousIndex;
        CurrentIndex = currentIndex;
    }

    /// <summary>Gets the index before the transition, or -1.</summary>
    public int PreviousIndex { get; }

    /// <summary>Gets the committed index, or -1.</summary>
    public int CurrentIndex { get; }
}
