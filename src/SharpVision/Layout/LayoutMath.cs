// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;

internal static class LayoutMath
{
    extension(int left)
    {
        public int Add(int right)
        {
            var value = (long) left + right;
            return value >= int.MaxValue ? int.MaxValue : (int) value;
        }

        public int SaturatingAdd(int right) =>
            (int) Math.Clamp((long) left + right, int.MinValue, int.MaxValue);

        public int Negate() => left == int.MinValue ? int.MaxValue : -left;

        public int Multiply(int right) =>
            (int) Math.Clamp((long) left * right, int.MinValue, int.MaxValue);
    }

    extension(int? value)
    {
        public int? Subtract(int extent) =>
            value.HasValue ? Math.Max(0, value.Value - extent) : null;
    }
}
