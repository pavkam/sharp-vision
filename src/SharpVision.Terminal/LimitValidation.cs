// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal;

/// <summary>Validates bounded positive integer limits accepted by terminal limit records.</summary>
internal static class LimitValidation
{
    extension(ArgumentOutOfRangeException)
    {
        /// <summary>Rejects a value that is not positive or exceeds the given inclusive maximum.</summary>
        /// <param name="value">The candidate limit value.</param>
        /// <param name="maximum">The inclusive upper bound the value must not exceed.</param>
        /// <param name="paramName">The name of the parameter that received the value.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive,
        /// or exceeds <paramref name="maximum"/>.</exception>
        public static void ThrowIfNotPositiveOrGreaterThan(int value, int maximum, string paramName)
        {
            if (value <= 0 || value > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    string.Create(CultureInfo.InvariantCulture, $"The limit must be positive and no greater than {maximum}."));
            }
        }
    }
}
