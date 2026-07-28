// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Input;

/// <summary>Maps the non-contiguous public function-key enum values by logical number.</summary>
internal static class FunctionKeys
{
    private static readonly Code[] _codes =
    [
        Code.F1, Code.F2, Code.F3, Code.F4, Code.F5, Code.F6, Code.F7,
        Code.F8, Code.F9, Code.F10, Code.F11, Code.F12, Code.F13, Code.F14,
        Code.F15, Code.F16, Code.F17, Code.F18, Code.F19, Code.F20,
        Code.F21, Code.F22, Code.F23, Code.F24, Code.F25, Code.F26,
        Code.F27, Code.F28, Code.F29, Code.F30, Code.F31, Code.F32,
        Code.F33, Code.F34, Code.F35, Code.F36, Code.F37, Code.F38,
        Code.F39, Code.F40, Code.F41, Code.F42, Code.F43, Code.F44,
        Code.F45, Code.F46, Code.F47, Code.F48, Code.F49, Code.F50,
        Code.F51, Code.F52, Code.F53, Code.F54, Code.F55, Code.F56,
        Code.F57, Code.F58, Code.F59, Code.F60, Code.F61, Code.F62,
        Code.F63
    ];

    /// <summary>Maps an inclusive function-key number to its stable public code.</summary>
    /// <param name="number">The one-based function-key number.</param>
    /// <param name="code">The mapped code when the number is supported.</param>
    /// <returns>Whether <paramref name="number"/> is between 1 and 63.</returns>
    public static bool TryGet(int number, out Code code)
    {
        if (number is < 1 or > 63)
        {
            code = Code.Unknown;
            return false;
        }

        code = _codes[number - 1];
        return true;
    }
}
