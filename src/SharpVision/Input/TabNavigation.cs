// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Controls how Tab traversal behaves within a control's subtree.</summary>
public enum TabNavigation
{
    /// <summary>No scope boundary. Tab passes through this control's children as part of the parent scope.</summary>
    Continue,

    /// <summary>Visits this scope's descendants once, then continues in the parent scope.</summary>
    Once,

    /// <summary>Tab wraps within this control's children. After the last child, focus returns to the first.</summary>
    Cycle,

    /// <summary>Excludes this control's descendants from Tab traversal.</summary>
    None,
}
