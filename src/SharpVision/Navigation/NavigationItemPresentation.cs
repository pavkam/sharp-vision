// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

/// <summary>
/// Captures the authored IsFocusable and IsTabStop a caller set on a
/// <see cref="NavigationViewItem"/> before its owning group overwrote them
/// with private presentation policy, so they can be restored unchanged when
/// the item leaves that group.
/// </summary>
internal readonly record struct NavigationItemPresentation
{
    /// <summary>Initializes an immutable snapshot of one item's authored presentation.</summary>
    /// <param name="focusable">The caller-authored focusability at attach time.</param>
    /// <param name="tabStop">The caller-authored tab-stop participation at attach time.</param>
    public NavigationItemPresentation(bool focusable, bool tabStop)
    {
        IsFocusable = focusable;
        IsTabStop = tabStop;
    }

    /// <summary>Gets the authored focusability.</summary>
    public bool IsFocusable { get; }

    /// <summary>Gets the authored tab-stop participation.</summary>
    public bool IsTabStop { get; }
}
