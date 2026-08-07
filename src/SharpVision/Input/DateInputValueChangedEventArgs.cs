// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Reports one committed DateInput value transition.</summary>
[PublicAPI]
public sealed class DateInputValueChangedEventArgs: EventArgs
{
    /// <summary>Initializes one immutable date transition.</summary>
    /// <param name="previousValue">The date before the transition, or null.</param>
    /// <param name="value">The committed date, or null.</param>
    public DateInputValueChangedEventArgs(DateOnly? previousValue, DateOnly? value)
    {
        PreviousValue = previousValue;
        Value = value;
    }

    /// <summary>Gets the date before the transition, or null.</summary>
    public DateOnly? PreviousValue { get; }

    /// <summary>Gets the committed date, or null.</summary>
    public DateOnly? Value { get; }
}
