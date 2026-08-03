// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Clipboard;

/// <summary>Shares canonical Base64 validation common to the clipboard and Kitty graphics encoders.</summary>
/// <remarks>
/// The BCL span decoder accepts some non-canonical final quanta. Correlation-sensitive protocols
/// such as Kitty OSC 5522 require complete quartets and terminal-only padding before allocating
/// an owned decoded copy, so this check is stricter than "the BCL decoder would accept it".
/// </remarks>
internal static class Base64Validation
{
    extension(ReadOnlySpan<byte> value)
    {
        /// <summary>
        /// Reports whether the span is strict canonical RFC 4648 §4 Base64: a multiple of four
        /// bytes, at most two trailing <c>=</c> padding bytes, standard alphabet only, and no
        /// embedded whitespace.
        /// </summary>
        /// <returns>Whether every byte belongs to a well-formed canonical Base64 encoding.</returns>
        public bool IsCanonicalBase64()
        {
            if (value.Length % 4 != 0)
            {
                return false;
            }

            var padding = 0;

            for (var index = value.Length - 1; index >= 0 && value[index] == (byte) '='; index--)
            {
                padding++;
            }

            if (padding > 2)
            {
                return false;
            }

            for (var index = 0; index < value.Length - padding; index++)
            {
                var item = value[index];
                var valid = item is (>= (byte) 'A' and <= (byte) 'Z') or
                    (>= (byte) 'a' and <= (byte) 'z') or
                    (>= (byte) '0' and <= (byte) '9') or
                    (byte) '+' or (byte) '/';

                if (!valid)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
