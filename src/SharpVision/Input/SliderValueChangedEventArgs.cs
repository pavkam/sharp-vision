// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Reports one committed Slider value transition.</summary>
public sealed class SliderValueChangedEventArgs: EventArgs
{
    /// <summary>Initializes one immutable signed range transition.</summary>
    /// <param name="previousValue">The value before the transition.</param>
    /// <param name="value">The committed value.</param>
    public SliderValueChangedEventArgs(int previousValue, int value)
    {
        PreviousValue = previousValue;
        Value = value;
    }

    /// <summary>Gets the value before the transition.</summary>
    public int PreviousValue { get; }

    /// <summary>Gets the committed value.</summary>
    public int Value { get; }
}
