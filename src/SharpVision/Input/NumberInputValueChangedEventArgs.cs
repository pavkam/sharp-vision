// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Reports one committed NumberInput value transition.</summary>
[PublicAPI]
public sealed class NumberInputValueChangedEventArgs: EventArgs
{
    /// <summary>Initializes one immutable numeric transition.</summary>
    /// <param name="previousValue">The value before the transition, or null.</param>
    /// <param name="value">The committed value, or null.</param>
    public NumberInputValueChangedEventArgs(decimal? previousValue, decimal? value)
    {
        PreviousValue = previousValue;
        Value = value;
    }

    /// <summary>Gets the value before the transition, or null.</summary>
    public decimal? PreviousValue { get; }

    /// <summary>Gets the committed value, or null.</summary>
    public decimal? Value { get; }
}
