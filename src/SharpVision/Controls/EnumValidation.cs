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
internal static class EnumValidation
{
    extension<T>(T value) where T : struct, Enum
    {
        /// <summary>Rejects a value that is not defined by its enum type.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is undefined.</exception>
        public void ValidateDefined()
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The enum value is unknown.");
            }
        }

        /// <summary>Rejects a value that is not defined by its enum type, reporting the caller's parameter name and message.</summary>
        /// <param name="paramName">The name of the parameter that received the value.</param>
        /// <param name="message">The exception message describing the violation.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is undefined.</exception>
        public void ValidateDefined(string paramName, string message)
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, message);
            }
        }
    }
}
