// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

/// <summary>
/// Captures the authored Focusable and TabStop a caller set on a
/// <see cref="NavigationView"/> section entry before the view overwrote them
/// with private presentation policy, so they can be restored unchanged when
/// the entry leaves that section.
/// </summary>
internal readonly record struct NavigationEntryPresentation
{
    /// <summary>Initializes an immutable snapshot of one entry's authored presentation.</summary>
    /// <param name="focusable">The caller-authored focusability at attach time.</param>
    /// <param name="tabStop">The caller-authored tab-stop participation at attach time.</param>
    public NavigationEntryPresentation(bool focusable, bool tabStop)
    {
        Focusable = focusable;
        TabStop = tabStop;
    }

    /// <summary>Gets the authored focusability.</summary>
    public bool Focusable { get; }

    /// <summary>Gets the authored tab-stop participation.</summary>
    public bool TabStop { get; }
}
