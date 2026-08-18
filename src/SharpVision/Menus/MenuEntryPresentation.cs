// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Menus;

/// <summary>
/// Captures the authored IsFocusable, IsTabStop, Width, and Height a caller set on
/// a menu entry before <see cref="Menu"/> overwrote them with its own private
/// presentation policy, so they can be restored unchanged when the entry
/// leaves the menu.
/// </summary>
internal readonly record struct MenuEntryPresentation
{
    /// <summary>Initializes an immutable snapshot of one entry's authored presentation.</summary>
    /// <param name="focusable">The caller-authored focusability at attach time.</param>
    /// <param name="tabStop">The caller-authored tab-stop participation at attach time.</param>
    /// <param name="width">The caller-authored width at attach time.</param>
    /// <param name="height">The caller-authored height at attach time.</param>
    public MenuEntryPresentation(bool focusable, bool tabStop, Length width, Length height)
    {
        IsFocusable = focusable;
        IsTabStop = tabStop;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the authored focusability.</summary>
    public bool IsFocusable { get; }

    /// <summary>Gets the authored tab-stop participation.</summary>
    public bool IsTabStop { get; }

    /// <summary>Gets the authored width.</summary>
    public Length Width { get; }

    /// <summary>Gets the authored height.</summary>
    public Length Height { get; }
}
