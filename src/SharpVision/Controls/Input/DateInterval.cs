// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Represents one ordered inclusive Gregorian date interval.</summary>
[PublicAPI]
public readonly record struct DateInterval
{
    /// <summary>Initializes an inclusive interval after validating endpoint order.</summary>
    /// <param name="start">The inclusive first date.</param>
    /// <param name="end">The inclusive last date.</param>
    /// <exception cref="ArgumentException"><paramref name="end"/> precedes <paramref name="start"/>.</exception>
    public DateInterval(DateOnly start, DateOnly end)
    {
        ArgumentException.ThrowIfBelowMinimum(end, start, nameof(end), "The interval end cannot precede its start.");

        Start = start;
        End = end;
    }

    /// <summary>Gets the inclusive first date.</summary>
    public DateOnly Start { get; }

    /// <summary>Gets the inclusive last date.</summary>
    public DateOnly End { get; }

    /// <summary>Gets the inclusive number of represented dates.</summary>
    [NonNegativeValue]
    public int Days => End.DayNumber - Start.DayNumber + 1;

    /// <summary>Determines whether one date belongs to this inclusive interval.</summary>
    /// <param name="date">The date to test.</param>
    /// <returns>True when <paramref name="date"/> is at or between the endpoints.</returns>
    [Pure]
    public bool Contains(DateOnly date) => date >= Start && date <= End;

    /// <summary>Determines whether this interval intersects another inclusive interval.</summary>
    /// <param name="other">The interval to test.</param>
    /// <returns>True when the intervals share at least one date.</returns>
    [Pure]
    public bool Intersects(DateInterval other) => Start <= other.End && End >= other.Start;
}
