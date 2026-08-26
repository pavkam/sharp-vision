// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

using System.Buffers;

/// <summary>Implements the Unicode simple case folding used by Qt case-insensitive comparisons.</summary>
internal static class SyntaxCaseFolding
{
    /// <summary>Compares two UTF-16 spans after Unicode simple case folding.</summary>
    /// <param name="left">The first span.</param>
    /// <param name="right">The second span.</param>
    /// <returns>True when both spans contain the same folded scalar sequence.</returns>
    internal static bool Equals(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        while (!left.IsEmpty && !right.IsEmpty)
        {
            if (ReadFolded(ref left) != ReadFolded(ref right))
            {
                return false;
            }
        }

        return left.IsEmpty && right.IsEmpty;
    }

    /// <summary>Computes an ordinal hash over one Unicode-simple-folded UTF-16 span.</summary>
    /// <param name="value">The span to hash.</param>
    /// <returns>A hash consistent with <see cref="Equals"/>.</returns>
    internal static int GetHashCode(ReadOnlySpan<char> value)
    {
        var hash = new HashCode();

        while (!value.IsEmpty)
        {
            hash.Add(ReadFolded(ref value));
        }

        return hash.ToHashCode();
    }

    private static int ReadFolded(ref ReadOnlySpan<char> value)
    {
        var status = Rune.DecodeFromUtf16(value, out var rune, out var consumed);

        if (status != OperationStatus.Done)
        {
            var invalidCodeUnit = value[0];
            value = value[1..];
            return invalidCodeUnit;
        }

        value = value[consumed..];
        return Fold(rune.Value);
    }

    // These are the Unicode 17.0 simple-fold mappings that differ from invariant lowercase. The
    // two contiguous Cherokee ranges are expressed arithmetically; all other scalars use the
    // runtime's Unicode lowercase tables. This matches Qt's one-scalar CaseFold table without
    // allocations.
    private static int Fold(int value) => value switch
    {
        >= 0x13F8 and <= 0x13FD => value - 0x8,
        >= 0xAB70 and <= 0xABBF => value - 0x97D0,
        0x00B5 => 0x03BC,
        0x017F => 0x0073,
        0x0345 => 0x03B9,
        0x03C2 => 0x03C3,
        0x03D0 => 0x03B2,
        0x03D1 => 0x03B8,
        0x03D5 => 0x03C6,
        0x03D6 => 0x03C0,
        0x03F0 => 0x03BA,
        0x03F1 => 0x03C1,
        0x03F5 => 0x03B5,
        0x1C80 => 0x0432,
        0x1C81 => 0x0434,
        0x1C82 => 0x043E,
        0x1C83 => 0x0441,
        0x1C84 or 0x1C85 => 0x0442,
        0x1C86 => 0x044A,
        0x1C87 => 0x0463,
        0x1C88 => 0xA64B,
        0x1E9B => 0x1E61,
        0x1FBE => 0x03B9,
        0x1FD3 => 0x0390,
        0x1FE3 => 0x03B0,
        0xFB05 => 0xFB06,
        _ => Rune.ToLowerInvariant(new Rune(value)).Value,
    };
}
