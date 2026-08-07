// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Clipboard;

/// <summary>Shares UTF-8 well-formedness validation common to the clipboard protocol encoders.</summary>
internal static class Utf8Validation
{
    extension(ReadOnlySpan<byte> value)
    {
        /// <summary>Reports whether every byte in the span decodes as well-formed UTF-8.</summary>
        /// <returns>Whether the span decodes completely with no invalid or incomplete sequence.</returns>
        public bool Valid()
        {
            while (!value.IsEmpty)
            {
                if (Rune.DecodeFromUtf8(value, out _, out var consumed) != OperationStatus.Done)
                {
                    return false;
                }

                Debug.Assert(consumed > 0, "Successful UTF-8 decoding always advances the input span.");
                value = value[consumed..];
            }

            return true;
        }
    }
}
