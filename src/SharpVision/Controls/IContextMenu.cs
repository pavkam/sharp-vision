// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Defines the control-owned contract for a secondary-action menu surface.</summary>
/// <remarks>
/// The contract keeps the control foundation independent from the concrete menu and popup
/// feature namespaces while allowing a menu implementation to participate in ownership,
/// layout, input routing, and disposal through its presentation control.
/// </remarks>
[PublicAPI]
public interface IContextMenu
{
    /// <summary>Gets the retained presentation control owned by the associated control.</summary>
    public Control Presentation { get; }

    /// <summary>Shows the menu at a root-relative cell position.</summary>
    /// <param name="row">The zero-based row in root coordinates.</param>
    /// <param name="col">The zero-based column in root coordinates.</param>
    public void Show(int row, int col);
}
