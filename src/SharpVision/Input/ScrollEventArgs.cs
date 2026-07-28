// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using Scrolling;

/// <summary>Reports one committed ScrollBar value transition.</summary>
[PublicAPI]
public sealed class ScrollEventArgs: EventArgs
{
    /// <summary>Initializes immutable scroll values and their input cause.</summary>
    /// <param name="previousValue">The value before the transition.</param>
    /// <param name="value">The committed value.</param>
    /// <param name="cause">The defined input path.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A value is negative or <paramref name="cause"/> is unknown.
    /// </exception>
    public ScrollEventArgs(int previousValue, int value, Cause cause)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(previousValue);
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        if (!Enum.IsDefined(cause))
        {
            throw new ArgumentOutOfRangeException(nameof(cause), cause, "The scroll cause is unknown.");
        }

        PreviousValue = previousValue;
        Value = value;
        Cause = cause;
    }

    /// <summary>Gets the value before the transition.</summary>
    public int PreviousValue { get; }

    /// <summary>Gets the scroll position after the transition, within [Minimum, Maximum].</summary>
    public int Value { get; }

    /// <summary>Gets the input path that caused the transition.</summary>
    public Cause Cause { get; }
}
