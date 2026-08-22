// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Validates enum values accepted by control contracts.</summary>
/// <remarks>
/// SharpVision.Terminal keeps a mirrored copy of this class at
/// <c>src/SharpVision.Terminal/EnumValidation.cs</c>, because that assembly cannot reference
/// this internal type: the project dependency points the other way, from SharpVision to
/// SharpVision.Terminal, not back. Apply any behavioral change to both copies.
/// </remarks>
[PublicAPI]
public static class EnumValidation
{
    extension(ArgumentOutOfRangeException)
    {
        /// <summary>Rejects a value that is not defined by its enum type.</summary>
        /// <param name="value">The candidate enum value.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is undefined.</exception>
        public static void ThrowIfNotDefined<T>(T value) where T : struct, Enum
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The enum value is unknown.");
            }
        }

        /// <summary>Rejects a value that is not defined by its enum type, reporting the caller's parameter name and message.</summary>
        /// <param name="value">The candidate enum value.</param>
        /// <param name="paramName">The name of the parameter that received the value.</param>
        /// <param name="message">The exception message describing the violation.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is undefined.</exception>
        public static void ThrowIfNotDefined<T>(T value, string paramName, string message) where T : struct, Enum
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, message);
            }
        }

        /// <summary>Rejects a value containing bits outside the given allowed mask.</summary>
        /// <param name="value">The candidate enum value.</param>
        /// <param name="allowedMask">The union of bits every valid value may set.</param>
        /// <param name="paramName">The name of the parameter that received the value.</param>
        /// <param name="message">The exception message describing the violation.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> sets a bit outside <paramref name="allowedMask"/>.</exception>
        public static void ThrowIfUndefinedFlags<T>(T value, T allowedMask, string paramName, string message) where T : struct, Enum
        {
            if ((Convert.ToUInt64(value, CultureInfo.InvariantCulture) &
                 ~Convert.ToUInt64(allowedMask, CultureInfo.InvariantCulture)) != 0)
            {
                throw new ArgumentOutOfRangeException(paramName, value, message);
            }
        }
    }
}
