// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Describes where one well-known <c>styles.*</c> role section inherits from before its
/// own JSON overlays: which parent role supplies its Normal chrome delta, which parent role
/// supplies its per-state deltas, and which row-specific adjustments apply to those states.</summary>
/// <remarks>
/// The cascade carries deltas, never whole values (see <see cref="Theme"/>'s root resolution): a
/// parent contributes only what the theme changed about it, applied onto the child's own
/// code-owned default with the child's own JSON winning on top. A role with no parents is a
/// terminal root (<c>control</c>). A role with a Normal parent but no state parent is passive
/// chrome that answers no interaction (<c>container</c>, <c>window</c>, <c>popup</c>,
/// <c>tooltip</c>, <c>panel</c>). A role with both follows its parent's interaction cues
/// (<c>input</c> from <c>control</c>; <c>button</c> and <c>toggle</c> from <c>input</c>;
/// <c>item</c> takes Normal from <c>control</c> and states from <c>input</c> with the row rule).
/// </remarks>
internal readonly record struct StyleRoleCascade
{
    /// <summary>Initializes one role's cascade description.</summary>
    /// <param name="normalParent">The role whose authored Normal delta this role's Normal starts from, or null for a terminal root.</param>
    /// <param name="stateParent">The role whose authored per-state deltas this role inherits, or null for passive chrome.</param>
    /// <param name="preservePointerBackground">Whether pointer hover keeps this role's own Normal background (the selectable-row rule).</param>
    /// <param name="applyBorderlessFocusFallback">Whether Focused/FocusWithin receive the reverse-video safety net when they would otherwise match Normal on borderless geometry.</param>
    /// <exception cref="ArgumentException"><paramref name="stateParent"/> is set while <paramref name="normalParent"/> is null.</exception>
    internal StyleRoleCascade(
        string? normalParent,
        string? stateParent,
        bool preservePointerBackground = false,
        bool applyBorderlessFocusFallback = false)
    {
        if (normalParent is null && stateParent is not null)
        {
            throw new ArgumentException("A role that inherits states must also inherit its Normal from a parent.", nameof(stateParent));
        }

        NormalParent = normalParent;
        StateParent = stateParent;
        PreservePointerBackground = preservePointerBackground;
        ApplyBorderlessFocusFallback = applyBorderlessFocusFallback;
    }

    /// <summary>Gets the role whose authored Normal delta this role's Normal starts from, or null for a terminal root.</summary>
    internal string? NormalParent { get; }

    /// <summary>Gets the role whose authored per-state deltas this role inherits, or null for passive chrome.</summary>
    internal string? StateParent { get; }

    /// <summary>Gets whether pointer hover keeps this role's own Normal background.</summary>
    internal bool PreservePointerBackground { get; }

    /// <summary>Gets whether Focused/FocusWithin receive the reverse-video safety net on borderless geometry.</summary>
    internal bool ApplyBorderlessFocusFallback { get; }

    /// <summary>Gets whether this role is a terminal root that inherits nothing.</summary>
    internal bool IsRoot => NormalParent is null;
}
