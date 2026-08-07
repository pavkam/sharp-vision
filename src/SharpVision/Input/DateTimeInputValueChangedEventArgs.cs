// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Reports one committed DateTimeInput value transition.</summary>
[PublicAPI]
public sealed class DateTimeInputValueChangedEventArgs: EventArgs
{
    /// <summary>Initializes one immutable date-time transition.</summary>
    /// <param name="previousValue">The date-time before the transition, or null.</param>
    /// <param name="value">The committed date-time, or null.</param>
    public DateTimeInputValueChangedEventArgs(DateTime? previousValue, DateTime? value)
    {
        PreviousValue = previousValue;
        Value = value;
    }

    /// <summary>Gets the date-time before the transition, or null.</summary>
    public DateTime? PreviousValue { get; }

    /// <summary>Gets the committed date-time, or null.</summary>
    public DateTime? Value { get; }
}
