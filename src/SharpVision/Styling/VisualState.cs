// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Describes orthogonal interaction and semantic visual state.</summary>
/// <remarks>The flags are local to a control. Parent focus and pointer membership are represented
/// by <see cref="FocusWithin"/> and <see cref="PointerOver"/>, while direct ownership uses
/// <see cref="Focused"/>. Resolution applies overlays in <see cref="VisualStateOrder.OrderedOverlays"/> order.</remarks>
[Flags]
[PublicAPI]
public enum VisualState
{
    /// <summary>The base appearance with no overlay.</summary>
    Normal = 0,

    /// <summary>The pointer is over this control or a descendant.</summary>
    PointerOver = 1 << 0,

    /// <summary>Keyboard focus is within this control's subtree.</summary>
    FocusWithin = 1 << 1,

    /// <summary>This control owns keyboard focus.</summary>
    Focused = 1 << 2,

    /// <summary>This control is the current item of a navigator.</summary>
    Current = 1 << 3,

    /// <summary>This control is selected by its owner.</summary>
    Selected = 1 << 4,

    /// <summary>This control is checked.</summary>
    Checked = 1 << 5,

    /// <summary>This control is indeterminate.</summary>
    Indeterminate = 1 << 6,

    /// <summary>A semantic press behavior currently presses this control.</summary>
    Pressed = 1 << 7,

    /// <summary>This control is disabled through itself or an ancestor.</summary>
    Disabled = 1 << 8
}
