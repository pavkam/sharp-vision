// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Reports one committed value transition raised by
/// <see cref="Controls.Input.TemporalInputBase{TValue}.ValueChanged"/>.</summary>
/// <typeparam name="TValue">The immutable temporal value type.</typeparam>
[PublicAPI]
public sealed class TemporalValueChangedEventArgs<TValue>: EventArgs
    where TValue : struct
{
    /// <summary>Initializes one immutable temporal transition.</summary>
    /// <param name="previous">The value before the transition, or null.</param>
    /// <param name="current">The committed value, or null.</param>
    public TemporalValueChangedEventArgs(TValue? previous, TValue? current)
    {
        Previous = previous;
        Current = current;
    }

    /// <summary>Gets the value before the transition, or null.</summary>
    public TValue? Previous { get; }

    /// <summary>Gets the committed value, or null.</summary>
    public TValue? Current { get; }
}
