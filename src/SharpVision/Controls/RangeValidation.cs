// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Validates mutually consistent Minimum/Maximum endpoints accepted by control range contracts.</summary>
internal static class RangeValidation
{
    extension<T>(T minimum) where T : IComparable<T>
    {
        /// <summary>Rejects a minimum that exceeds the given maximum.</summary>
        /// <param name="maximum">The upper endpoint the minimum may not exceed.</param>
        /// <param name="paramName">The validated parameter name.</param>
        /// <param name="message">The exception message naming the specific endpoints.</param>
        /// <exception cref="ArgumentException"><paramref name="minimum"/> exceeds <paramref name="maximum"/>.</exception>
        public void ThrowIfAboveMaximum(T maximum, string paramName, string message)
        {
            if (minimum.CompareTo(maximum) > 0)
            {
                throw new ArgumentException(message, paramName);
            }
        }

        /// <summary>Rejects a minimum that is at or above the given maximum.</summary>
        /// <param name="maximum">The upper endpoint the minimum must remain strictly below.</param>
        /// <param name="paramName">The validated parameter name.</param>
        /// <param name="message">The exception message naming the specific endpoints.</param>
        /// <remarks>
        /// Checks equality alongside the comparison because a type can hold values that
        /// compare equal to zero without being interchangeable: <c>double.CompareTo</c>
        /// orders -0.0 below +0.0, so the comparison alone would let a numerically
        /// zero-width endpoint pair through the strict guard it exists to reject.
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="minimum"/> is at or above <paramref name="maximum"/>.</exception>
        public void ThrowIfAtOrAboveMaximum(T maximum, string paramName, string message)
        {
            if (minimum.CompareTo(maximum) >= 0 || EqualityComparer<T>.Default.Equals(minimum, maximum))
            {
                throw new ArgumentException(message, paramName);
            }
        }
    }

    extension<T>(T maximum) where T : IComparable<T>
    {
        /// <summary>Rejects a maximum that is below the given minimum.</summary>
        /// <param name="minimum">The lower endpoint the maximum may not fall below.</param>
        /// <param name="paramName">The validated parameter name.</param>
        /// <param name="message">The exception message naming the specific endpoints.</param>
        /// <exception cref="ArgumentException"><paramref name="maximum"/> is below <paramref name="minimum"/>.</exception>
        public void ThrowIfBelowMinimum(T minimum, string paramName, string message)
        {
            if (maximum.CompareTo(minimum) < 0)
            {
                throw new ArgumentException(message, paramName);
            }
        }

        /// <summary>Rejects a maximum that is at or below the given minimum.</summary>
        /// <param name="minimum">The lower endpoint the maximum must remain strictly above.</param>
        /// <param name="paramName">The validated parameter name.</param>
        /// <param name="message">The exception message naming the specific endpoints.</param>
        /// <remarks>
        /// Checks equality alongside the comparison because a type can hold values that
        /// compare equal to zero without being interchangeable: <c>double.CompareTo</c>
        /// orders -0.0 below +0.0, so the comparison alone would let a numerically
        /// zero-width endpoint pair through the strict guard it exists to reject.
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="maximum"/> is at or below <paramref name="minimum"/>.</exception>
        public void ThrowIfAtOrBelowMinimum(T minimum, string paramName, string message)
        {
            if (maximum.CompareTo(minimum) <= 0 || EqualityComparer<T>.Default.Equals(maximum, minimum))
            {
                throw new ArgumentException(message, paramName);
            }
        }
    }

    extension<T>(T value) where T : IComparable<T>
    {
        /// <summary>Rejects a value that falls outside the given inclusive endpoints.</summary>
        /// <param name="minimum">The inclusive lower endpoint.</param>
        /// <param name="maximum">The inclusive upper endpoint.</param>
        /// <param name="paramName">The validated parameter name.</param>
        /// <param name="message">The exception message naming the specific endpoints.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="value"/> is below <paramref name="minimum"/> or above <paramref name="maximum"/>.
        /// </exception>
        public void ThrowIfOutsideInclusiveRange(T minimum, T maximum, string paramName, string message)
        {
            if (value.CompareTo(minimum) < 0 || value.CompareTo(maximum) > 0)
            {
                throw new ArgumentOutOfRangeException(paramName, value, message);
            }
        }
    }
}
