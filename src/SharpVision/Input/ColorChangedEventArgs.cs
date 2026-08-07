// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Reports one committed ColorPicker value transition.</summary>
[PublicAPI]
public sealed class ColorChangedEventArgs: EventArgs
{
    /// <summary>Initializes one immutable color transition.</summary>
    /// <param name="previousValue">The color before the transition.</param>
    /// <param name="value">The committed color.</param>
    public ColorChangedEventArgs(Color previousValue, Color value)
    {
        PreviousValue = previousValue;
        Value = value;
    }

    /// <summary>Gets the color before the transition.</summary>
    public Color PreviousValue { get; }

    /// <summary>Gets the committed color.</summary>
    public Color Value { get; }
}
