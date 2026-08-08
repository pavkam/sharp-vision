// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Scrolling;

/// <summary>Defines one immutable validated integer scroll range.</summary>
[PublicAPI]
public readonly record struct ScrollRange
{
    /// <summary>Initializes a non-negative ordered range, current value, and viewport.</summary>
    /// <param name="minimum">The non-negative inclusive lower endpoint.</param>
    /// <param name="maximum">The inclusive endpoint not below minimum.</param>
    /// <param name="value">The current value inside both endpoints.</param>
    /// <param name="viewport">The non-negative visible extent.</param>
    /// <exception cref="ArgumentOutOfRangeException">A value is negative or outside the range.</exception>
    /// <exception cref="ArgumentException"><paramref name="maximum"/> is below minimum.</exception>
    public ScrollRange(int minimum, int maximum, int value, int viewport)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimum);
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        ArgumentOutOfRangeException.ThrowIfNegative(viewport);

        maximum.ThrowIfBelowMinimum(minimum, nameof(maximum), "Maximum cannot be below minimum.");

        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "The value must be inside the inclusive range.");
        }

        Minimum = minimum;
        Maximum = maximum;
        Value = value;
        Viewport = viewport;
    }

    /// <summary>Gets the inclusive lower endpoint.</summary>
    public int Minimum { get; }

    /// <summary>Gets the inclusive upper endpoint.</summary>
    public int Maximum { get; }

    /// <summary>Gets the current value.</summary>
    public int Value { get; }

    /// <summary>Gets the visible extent.</summary>
    public int Viewport { get; }

    /// <summary>Gets the non-negative endpoint distance.</summary>
    public int Span => Maximum - Minimum;

    /// <summary>Clamps any integer value to the inclusive endpoints.</summary>
    /// <param name="value">The candidate value.</param>
    /// <returns>The contained value.</returns>
    public int Clamp(int value) => Math.Clamp(value, Minimum, Maximum);

    /// <summary>Adds a signed delta with saturation and endpoint clamping.</summary>
    /// <param name="delta">The signed requested change.</param>
    /// <returns>The contained resulting value.</returns>
    public int Move(int delta)
    {
        var value = (long) Value + delta;
        return (int) Math.Clamp(value, Minimum, Maximum);
    }
}
