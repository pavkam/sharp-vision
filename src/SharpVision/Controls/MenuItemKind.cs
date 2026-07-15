// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Classifies the activation and rendering behavior of one menu item.</summary>
public enum MenuItemKind
{
    /// <summary>Invokes one ordinary command-like item.</summary>
    Command,

    /// <summary>Toggles one independently checked item before invocation.</summary>
    Check,

    /// <summary>Selects one checked item within its containing menu and group name.</summary>
    Radio,
}
