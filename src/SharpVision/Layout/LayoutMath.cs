// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;

internal static class LayoutMath
{
    public static int Add(int left, int right)
    {
        var value = (long) left + right;
        return value >= int.MaxValue ? int.MaxValue : (int) value;
    }

    public static int? Subtract(int? value, int extent) =>
        value.HasValue ? Math.Max(0, value.Value - extent) : null;

    public static int SaturatingAdd(int left, int right) =>
        (int) Math.Clamp((long) left + right, int.MinValue, int.MaxValue);

    public static int Negate(int value) => value == int.MinValue ? int.MaxValue : -value;
}
