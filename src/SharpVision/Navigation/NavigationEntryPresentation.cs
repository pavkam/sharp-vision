// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

/// <summary>
/// Captures the authored IsFocusable and IsTabStop a caller set on a
/// <see cref="NavigationView"/> section entry before the view overwrote them
/// with private presentation policy, so they can be restored unchanged when
/// the entry leaves that section.
/// </summary>
internal readonly record struct NavigationEntryPresentation
{
    /// <summary>Initializes an immutable snapshot of one entry's authored presentation.</summary>
    /// <param name="focusable">The caller-authored focusability at attach time.</param>
    /// <param name="tabStop">The caller-authored tab-stop participation at attach time.</param>
    /// <param name="version">The unique ownership transaction that captured the snapshot.</param>
    public NavigationEntryPresentation(bool focusable, bool tabStop, long version)
    {
        IsFocusable = focusable;
        IsTabStop = tabStop;
        Version = version;
    }

    /// <summary>Gets the authored focusability.</summary>
    public bool IsFocusable { get; }

    /// <summary>Gets the authored tab-stop participation.</summary>
    public bool IsTabStop { get; }

    /// <summary>Gets the unique ownership transaction that captured this snapshot.</summary>
    public long Version { get; }

    /// <summary>Creates a snapshot with the latest caller-authored focusability.</summary>
    /// <param name="value">The requested focusability.</param>
    /// <returns>The updated immutable snapshot.</returns>
    public NavigationEntryPresentation WithFocusable(bool value) => new(value, IsTabStop, Version);

    /// <summary>Creates a snapshot with the latest caller-authored tab-stop participation.</summary>
    /// <param name="value">The requested tab-stop participation.</param>
    /// <returns>The updated immutable snapshot.</returns>
    public NavigationEntryPresentation WithTabStop(bool value) => new(IsFocusable, value, Version);
}
