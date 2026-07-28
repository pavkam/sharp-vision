// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Reports one committed TimeInput value transition.</summary>
[PublicAPI]
public sealed class TimeInputValueChangedEventArgs: EventArgs
{
    /// <summary>Initializes one immutable time transition.</summary>
    /// <param name="previousValue">The time before the transition, or null.</param>
    /// <param name="value">The committed time, or null.</param>
    public TimeInputValueChangedEventArgs(TimeOnly? previousValue, TimeOnly? value)
    {
        PreviousValue = previousValue;
        Value = value;
    }

    /// <summary>Gets the time before the transition, or null.</summary>
    public TimeOnly? PreviousValue { get; }

    /// <summary>Gets the committed time, or null.</summary>
    public TimeOnly? Value { get; }
}
