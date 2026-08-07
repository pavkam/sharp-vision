// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Single source of truth for direct appearance overlay resolution order.</summary>
internal static class VisualStateOrder
{
    /// <summary>Fixed overlay order for direct appearance composition.</summary>
    public static readonly VisualState[] OrderedOverlays =
    [
        VisualState.PointerOver,
        VisualState.FocusWithin,
        VisualState.Focused,
        VisualState.Current,
        VisualState.Selected,
        VisualState.Checked,
        VisualState.Indeterminate,
        VisualState.Pressed,
        VisualState.Disabled
    ];
}
