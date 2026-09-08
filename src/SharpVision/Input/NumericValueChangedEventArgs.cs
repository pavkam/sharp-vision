// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Reports one committed numeric field value transition.</summary>
[PublicAPI]
public sealed class NumericValueChangedEventArgs: EventArgs
{
    /// <summary>Initializes one immutable numeric transition.</summary>
    /// <param name="previous">The value before the transition, or null.</param>
    /// <param name="current">The committed value, or null.</param>
    public NumericValueChangedEventArgs(decimal? previous, decimal? current)
    {
        Previous = previous;
        Current = current;
    }

    /// <summary>Gets the value before the transition, or null.</summary>
    public decimal? Previous { get; }

    /// <summary>Gets the committed value, or null.</summary>
    public decimal? Current { get; }
}
