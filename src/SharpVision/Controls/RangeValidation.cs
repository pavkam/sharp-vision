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
        /// <exception cref="ArgumentException"><paramref name="minimum"/> is at or above <paramref name="maximum"/>.</exception>
        public void ThrowIfAtOrAboveMaximum(T maximum, string paramName, string message)
        {
            if (minimum.CompareTo(maximum) >= 0)
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
        /// <exception cref="ArgumentException"><paramref name="maximum"/> is at or below <paramref name="minimum"/>.</exception>
        public void ThrowIfAtOrBelowMinimum(T minimum, string paramName, string message)
        {
            if (maximum.CompareTo(minimum) <= 0)
            {
                throw new ArgumentException(message, paramName);
            }
        }
    }
}
