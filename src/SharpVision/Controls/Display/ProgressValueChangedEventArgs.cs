// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Reports one committed ProgressBar value transition.</summary>
[PublicAPI]
public sealed class ProgressValueChangedEventArgs: EventArgs
{
    /// <summary>Initializes one immutable progress value transition.</summary>
    /// <param name="previousValue">The value before the transition.</param>
    /// <param name="value">The committed value.</param>
    /// <exception cref="ArgumentOutOfRangeException">A value is not finite.</exception>
    public ProgressValueChangedEventArgs(double previousValue, double value)
    {
        ValidateFinite(previousValue, nameof(previousValue));
        ValidateFinite(value, nameof(value));
        PreviousValue = previousValue;
        Value = value;
    }

    /// <summary>Gets the value before the transition.</summary>
    public double PreviousValue { get; }

    /// <summary>Gets the committed value, clamped to [Minimum, Maximum].</summary>
    public double Value { get; }

    private static void ValidateFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Progress values must be finite.");
        }
    }
}
