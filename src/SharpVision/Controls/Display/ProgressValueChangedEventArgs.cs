// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Reports one committed ProgressBar value transition.</summary>
[PublicAPI]
public sealed class ProgressValueChangedEventArgs: EventArgs
{
    /// <summary>Initializes one immutable progress value transition.</summary>
    /// <param name="previousValue">The value before the transition.</param>
    /// <param name="newValue">The committed value.</param>
    public ProgressValueChangedEventArgs(double previousValue, double newValue)
    {
        PreviousValue = previousValue;
        NewValue = newValue;
    }

    /// <summary>Gets the value before the transition.</summary>
    public double PreviousValue { get; }

    /// <summary>Gets the new progress value after the transition, clamped to [Minimum, Maximum].</summary>
    public double NewValue { get; }
}
