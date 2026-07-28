// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Validates enum values accepted by control contracts.</summary>
internal static class EnumValidation
{
    /// <summary>Rejects a value that is not defined by its enum type.</summary>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <param name="value">The proposed value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is undefined.</exception>
    public static void ValidateDefined<T>(T value) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The enum value is unknown.");
        }
    }
}
