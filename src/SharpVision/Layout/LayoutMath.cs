// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;

/// <summary>Provides saturating integer arithmetic for terminal cell layout.</summary>
[PublicAPI]
[SuppressMessage(
    "Naming",
    "CA1708:IdentifiersShouldDifferByMoreThanCase",
    Justification = "C# extension-block metadata names are compiler-generated.")]
public static class LayoutMath
{
    extension(int left)
    {
        /// <summary>Adds signed cell values and saturates at the integer boundaries.</summary>
        [Pure]
        public int Add(int right) => (int) Math.Clamp((long) left + right, int.MinValue, int.MaxValue);

        /// <summary>Adds signed cell values and saturates at the integer boundaries.</summary>
        [Pure]
        public int SaturatingAdd(int right) =>
            (int) Math.Clamp((long) left + right, int.MinValue, int.MaxValue);

        /// <summary>Subtracts signed cell values and saturates at the integer boundaries.</summary>
        [Pure]
        public int SaturatingSubtract(int right) =>
            (int) Math.Clamp((long) left - right, int.MinValue, int.MaxValue);

        /// <summary>Negates a signed cell value, saturating the unrepresentable minimum.</summary>
        [Pure]
        public int Negate() => left == int.MinValue ? int.MaxValue : -left;

        /// <summary>Multiplies signed cell values and saturates at the integer boundaries.</summary>
        [Pure]
        public int Multiply(int right) =>
            (int) Math.Clamp((long) left * right, int.MinValue, int.MaxValue);
    }

    extension(int? value)
    {
        /// <summary>Subtracts a non-negative extent and clamps a present result to zero.</summary>
        [Pure]
        public int? Subtract(int extent) =>
            value.HasValue ? Math.Max(0, value.Value - extent) : null;
    }

    extension(ReadOnlySpan<int> values)
    {
        /// <summary>Sums signed cell values from left to right with saturation after each term.</summary>
        [Pure]
        public int SaturatingSum()
        {
            var result = 0;

            foreach (var value in values)
            {
                result = result.Add(value);
            }

            return result;
        }
    }

    extension(IEnumerable<int> values)
    {
        /// <summary>Enumerates and sums signed cell values with saturation after each term.</summary>
        [Pure]
        public int SaturatingSum()
        {
            ArgumentNullException.ThrowIfNull(values);
            var result = 0;

            foreach (var value in values)
            {
                result = result.Add(value);
            }

            return result;
        }
    }

    /// <summary>Returns the saturated cells occupied by gaps between <paramref name="count"/> items.</summary>
    /// <param name="spacing">The non-negative cells in each gap.</param>
    /// <param name="count">The non-negative item count.</param>
    /// <param name="limit">An optional non-negative maximum extent in cells.</param>
    /// <returns>Zero for fewer than two items; otherwise the saturated and optionally bounded gap extent.</returns>
    [Pure]
    public static int GapExtent(int spacing, int count, int? limit)
    {
        Debug.Assert(spacing >= 0, "Gap spacing is non-negative.");
        Debug.Assert(count >= 0, "Gap item count is non-negative.");
        Debug.Assert(limit is null or >= 0, "A gap extent limit is non-negative when present.");

        if (count <= 1)
        {
            return 0;
        }

        var requested = spacing.Multiply(count - 1);
        return limit.HasValue ? Math.Min(limit.Value, requested) : requested;
    }
}
