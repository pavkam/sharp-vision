// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Validates enum values accepted by control contracts.</summary>
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
    }
}
