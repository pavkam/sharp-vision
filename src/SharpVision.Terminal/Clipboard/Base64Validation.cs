// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Clipboard;

using MustUseReturnValue = JetBrains.Annotations.MustUseReturnValueAttribute;

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
        [Pure]
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

        /// <summary>
        /// Base64-encodes the complete span into <paramref name="destination"/>, throwing
        /// <paramref name="failureMessage"/> as an <see cref="InvalidOperationException"/> if the
        /// destination is too small to hold the full encoding.
        /// </summary>
        /// <param name="destination">The buffer sized to hold the complete encoding.</param>
        /// <param name="failureMessage">The exception message used if encoding does not complete.</param>
        /// <returns>The number of bytes written to <paramref name="destination"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// The destination was too small to hold the complete encoding of a bounded, pre-validated
        /// source span.
        /// </exception>
        /// <remarks>
        /// Every call site bounds and validates its source before calling this helper, so encoding
        /// is expected to always complete. The uniform <c>consumed == value.Length</c> check applies
        /// even at call sites that previously checked only <see cref="OperationStatus.Done"/>: this is
        /// a provable no-change, because <see cref="Base64.EncodeToUtf8"/> reports
        /// <see cref="OperationStatus.Done"/> only once the entire source has been consumed.
        /// </remarks>
        [MustUseReturnValue]
        public int EncodeBase64OrThrow(Span<byte> destination, string failureMessage)
        {
            var status = Base64.EncodeToUtf8(value, destination, out var consumed, out var written);

            return status == OperationStatus.Done && consumed == value.Length
                ? written
                : throw new InvalidOperationException(failureMessage);
        }
    }
}
