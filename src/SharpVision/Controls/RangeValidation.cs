// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Validates mutually consistent Minimum/Maximum endpoints, and related numeric step
/// contracts, accepted by control range contracts.</summary>
internal static class RangeValidation
{
    extension(ArgumentOutOfRangeException)
    {
        /// <summary>Rejects a step that is not a positive whole number of minutes.</summary>
        /// <param name="value">The candidate step.</param>
        /// <param name="paramName">The validated parameter name.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="value"/> is zero, negative, or not a whole minute.
        /// </exception>
        public static void ThrowIfNotAPositiveWholeMinuteStep(TimeSpan value, string paramName)
        {
            if (value <= TimeSpan.Zero || value.TotalMinutes != Math.Truncate(value.TotalMinutes))
            {
                throw new ArgumentOutOfRangeException(paramName, value, "TimeStep must be a positive whole number of minutes.");
            }
        }

        /// <summary>Rejects a step that is not positive.</summary>
        /// <param name="value">The candidate step.</param>
        /// <param name="paramName">The validated parameter name.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is zero or negative.</exception>
        public static void ThrowIfNotAPositiveStep(decimal value, string paramName)
        {
            if (value <= 0m)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Step must be positive.");
            }
        }

        /// <summary>Rejects a value that falls outside the given inclusive endpoints.</summary>
        /// <param name="value">The candidate value.</param>
        /// <param name="minimum">The inclusive lower endpoint.</param>
        /// <param name="maximum">The inclusive upper endpoint.</param>
        /// <param name="paramName">The validated parameter name.</param>
        /// <param name="message">The exception message naming the specific endpoints.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="value"/> is below <paramref name="minimum"/> or above <paramref name="maximum"/>.
        /// </exception>
        public static void ThrowIfOutsideInclusiveRange<T>(T value, T minimum, T maximum, string paramName, string message)
            where T : IComparable<T>
        {
            if (value.CompareTo(minimum) < 0 || value.CompareTo(maximum) > 0)
            {
                throw new ArgumentOutOfRangeException(paramName, value, message);
            }
        }

        /// <summary>Rejects a value that is not finite.</summary>
        /// <param name="value">The candidate value.</param>
        /// <param name="paramName">The validated parameter name.</param>
        /// <param name="message">The exception message describing the violation.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is <c>NaN</c> or infinite.</exception>
        public static void ThrowIfNotFinite(double value, string paramName, string message)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, message);
            }
        }

        /// <summary>Rejects a non-null value that is not finite; a null value is accepted.</summary>
        /// <param name="value">The candidate value.</param>
        /// <param name="paramName">The validated parameter name.</param>
        /// <param name="message">The exception message describing the violation.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> has a value that is <c>NaN</c> or infinite.</exception>
        public static void ThrowIfNotFinite(double? value, string paramName, string message)
        {
            if (value.HasValue && !double.IsFinite(value.Value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, message);
            }
        }
    }

    extension(ArgumentException)
    {
        /// <summary>Rejects a minimum that exceeds the given maximum.</summary>
        /// <param name="minimum">The candidate lower endpoint.</param>
        /// <param name="maximum">The upper endpoint the minimum may not exceed.</param>
        /// <param name="paramName">The validated parameter name.</param>
        /// <param name="message">The exception message naming the specific endpoints.</param>
        /// <exception cref="ArgumentException"><paramref name="minimum"/> exceeds <paramref name="maximum"/>.</exception>
        public static void ThrowIfAboveMaximum<T>(T minimum, T maximum, string paramName, string message)
            where T : IComparable<T>
        {
            if (minimum.CompareTo(maximum) > 0)
            {
                throw new ArgumentException(message, paramName);
            }
        }

        /// <summary>Rejects a minimum that is at or above the given maximum.</summary>
        /// <param name="minimum">The candidate lower endpoint.</param>
        /// <param name="maximum">The upper endpoint the minimum must remain strictly below.</param>
        /// <param name="paramName">The validated parameter name.</param>
        /// <param name="message">The exception message naming the specific endpoints.</param>
        /// <remarks>
        /// The inclusive <c>&gt;= 0</c> comparison alone already rejects equal endpoints,
        /// including numerically equal pairs such as -0.0 and +0.0: <c>CompareTo</c> reports
        /// those as equal, so no separate equality check is needed.
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="minimum"/> is at or above <paramref name="maximum"/>.</exception>
        public static void ThrowIfAtOrAboveMaximum<T>(T minimum, T maximum, string paramName, string message)
            where T : IComparable<T>
        {
            if (minimum.CompareTo(maximum) >= 0)
            {
                throw new ArgumentException(message, paramName);
            }
        }

        /// <summary>Rejects a maximum that is below the given minimum.</summary>
        /// <param name="maximum">The candidate upper endpoint.</param>
        /// <param name="minimum">The lower endpoint the maximum may not fall below.</param>
        /// <param name="paramName">The validated parameter name.</param>
        /// <param name="message">The exception message naming the specific endpoints.</param>
        /// <exception cref="ArgumentException"><paramref name="maximum"/> is below <paramref name="minimum"/>.</exception>
        public static void ThrowIfBelowMinimum<T>(T maximum, T minimum, string paramName, string message)
            where T : IComparable<T>
        {
            if (maximum.CompareTo(minimum) < 0)
            {
                throw new ArgumentException(message, paramName);
            }
        }

        /// <summary>Rejects a maximum that is at or below the given minimum.</summary>
        /// <param name="maximum">The candidate upper endpoint.</param>
        /// <param name="minimum">The lower endpoint the maximum must remain strictly above.</param>
        /// <param name="paramName">The validated parameter name.</param>
        /// <param name="message">The exception message naming the specific endpoints.</param>
        /// <remarks>
        /// The inclusive <c>&lt;= 0</c> comparison alone already rejects equal endpoints,
        /// including numerically equal pairs such as -0.0 and +0.0: <c>CompareTo</c> reports
        /// those as equal, so no separate equality check is needed.
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="maximum"/> is at or below <paramref name="minimum"/>.</exception>
        public static void ThrowIfAtOrBelowMinimum<T>(T maximum, T minimum, string paramName, string message)
            where T : IComparable<T>
        {
            if (maximum.CompareTo(minimum) <= 0)
            {
                throw new ArgumentException(message, paramName);
            }
        }
    }

    extension<T>(T value) where T : IComparable<T>
    {
        /// <summary>Constrains a value to the given inclusive endpoints without throwing.</summary>
        /// <param name="minimum">The inclusive lower endpoint.</param>
        /// <param name="maximum">The inclusive upper endpoint.</param>
        /// <returns><paramref name="minimum"/> or <paramref name="maximum"/> if <paramref name="value"/> falls outside them; otherwise <paramref name="value"/>.</returns>
        public T Clamp(T minimum, T maximum) =>
            value.CompareTo(minimum) < 0 ? minimum
            : value.CompareTo(maximum) > 0 ? maximum
            : value;
    }

    /// <summary>Divides two non-negative operands, rounding the result to the nearest whole number
    /// and rounding a midpoint remainder up.</summary>
    /// <param name="numerator">The non-negative dividend.</param>
    /// <param name="denominator">The positive divisor.</param>
    /// <returns>The quotient, rounded half up.</returns>
    internal static int RoundHalfUp(long numerator, long denominator) =>
        (int) ((numerator + (denominator / 2)) / denominator);
}
