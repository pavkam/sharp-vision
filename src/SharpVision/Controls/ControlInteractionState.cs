// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Owns one control's committed local interaction facts.</summary>
/// <remarks>Focus, physical pointer-over, direct pointer-over, and press are distinct facts.
/// The focus and pointer managers decide when they change; this value merely commits the local
/// state atomically so <see cref="Control"/> does not become a second interaction authority.</remarks>
internal sealed class ControlInteractionState
{
    /// <summary>Gets whether the control owns direct keyboard focus.</summary>
    internal bool IsFocused { get; private set; }

    /// <summary>Gets whether the physical pointer is over the control or one of its descendants.</summary>
    internal bool IsPointerOver { get; private set; }

    /// <summary>Gets whether the physical pointer directly targets the control.</summary>
    internal bool IsPointerDirectlyOver { get; private set; }

    /// <summary>Gets whether an active press began on the control.</summary>
    internal bool IsPressed { get; private set; }

    /// <summary>Gets whether an owning collection selected this control.</summary>
    internal bool IsSelected { get; private set; }

    /// <summary>Gets whether an owning navigator marks this control current.</summary>
    internal bool IsCurrent { get; private set; }

    /// <summary>Commits direct focus when it differs from the current fact.</summary>
    internal bool SetFocused(bool value)
    {
        if (IsFocused == value)
        {
            return false;
        }

        IsFocused = value;
        return true;
    }

    /// <summary>Commits physical pointer facts and returns the prior pointer-over value.</summary>
    internal bool SetPointerOver(bool value, bool directlyOver, out bool wasOver)
    {
        wasOver = IsPointerOver;

        if (wasOver == value && IsPointerDirectlyOver == directlyOver)
        {
            return false;
        }

        IsPointerOver = value;
        IsPointerDirectlyOver = directlyOver;
        return true;
    }

    /// <summary>Commits press state when it differs from the current fact.</summary>
    internal bool SetPressed(bool value)
    {
        if (IsPressed == value)
        {
            return false;
        }

        IsPressed = value;
        return true;
    }

    /// <summary>Commits collection-selected state when it differs from the current fact.</summary>
    internal bool SetSelected(bool value)
    {
        if (IsSelected == value)
        {
            return false;
        }

        IsSelected = value;
        return true;
    }

    /// <summary>Commits collection-current state when it differs from the current fact.</summary>
    internal bool SetCurrent(bool value)
    {
        if (IsCurrent == value)
        {
            return false;
        }

        IsCurrent = value;
        return true;
    }
}
