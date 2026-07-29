// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

/// <summary>
/// Captures the authored Padding, Focusable, and TabStop a caller set on a
/// <see cref="NavigationViewItem"/> before its owning group or view overwrote
/// them with private presentation policy, so they can be restored unchanged
/// when the item leaves that owner.
/// </summary>
internal readonly record struct NavigationItemPresentation
{
    /// <summary>Initializes an immutable snapshot of one item's authored presentation.</summary>
    /// <param name="padding">The caller-authored padding at attach time.</param>
    /// <param name="focusable">The caller-authored focusability at attach time.</param>
    /// <param name="tabStop">The caller-authored tab-stop participation at attach time.</param>
    public NavigationItemPresentation(Thickness padding, bool focusable, bool tabStop)
    {
        Padding = padding;
        Focusable = focusable;
        TabStop = tabStop;
    }

    /// <summary>Gets the authored padding.</summary>
    public Thickness Padding { get; }

    /// <summary>Gets the authored focusability.</summary>
    public bool Focusable { get; }

    /// <summary>Gets the authored tab-stop participation.</summary>
    public bool TabStop { get; }
}
