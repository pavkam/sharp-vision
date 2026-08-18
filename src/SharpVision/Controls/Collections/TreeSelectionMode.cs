// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Specifies whether a <see cref="TreeView"/> permits no, one, or many selected items.</summary>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "Single is the conventional selection-mode name.")]
public enum TreeSelectionMode
{
    /// <summary>Disables selection while retaining navigation and invocation.</summary>
    None,

    /// <summary>Permits one selected item.</summary>
    Single,

    /// <summary>Permits multiple selected items with Ctrl and Shift modifiers.</summary>
    Multiple
}
