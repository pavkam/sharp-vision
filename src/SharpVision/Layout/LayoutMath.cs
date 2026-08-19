// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;

internal static class LayoutMath
{
    extension(int left)
    {
        [Pure]
        public int Add(int right) => (int) Math.Clamp((long) left + right, int.MinValue, int.MaxValue);

        [Pure]
        public int SaturatingAdd(int right) =>
            (int) Math.Clamp((long) left + right, int.MinValue, int.MaxValue);

        [Pure]
        public int Negate() => left == int.MinValue ? int.MaxValue : -left;

        [Pure]
        public int Multiply(int right) =>
            (int) Math.Clamp((long) left * right, int.MinValue, int.MaxValue);
    }

    extension(int? value)
    {
        [Pure]
        public int? Subtract(int extent) =>
            value.HasValue ? Math.Max(0, value.Value - extent) : null;
    }
}
