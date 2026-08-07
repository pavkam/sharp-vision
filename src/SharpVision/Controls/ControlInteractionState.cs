// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Owns one control's committed local interaction facts.</summary>
/// <remarks>Focus, physical pointer-over, direct pointer-over, and press are distinct facts.
/// The focus and pointer managers decide when they change; this value merely commits the local
/// state atomically so <see cref="ControlBase"/> does not become a second interaction authority.</remarks>
internal sealed class ControlInteractionState
{
    /// <summary>Gets whether the control owns direct keyboard focus.</summary>
    public bool Focused { get; private set; }

    /// <summary>Gets whether the physical pointer is over the control or one of its descendants.</summary>
    public bool PointerOver { get; private set; }

    /// <summary>Gets whether the physical pointer directly targets the control.</summary>
    public bool PointerDirectlyOver { get; private set; }

    /// <summary>Gets whether an active press began on the control.</summary>
    public bool Pressed { get; private set; }

    /// <summary>Gets whether an owning collection selected this control.</summary>
    public bool Selected { get; private set; }

    /// <summary>Gets whether an owning navigator marks this control current.</summary>
    public bool Current { get; private set; }

    /// <summary>Commits direct focus when it differs from the current fact.</summary>
    public bool SetFocused(bool value)
    {
        if (Focused == value)
        {
            return false;
        }

        Focused = value;
        return true;
    }

    /// <summary>Commits physical pointer facts and returns the prior pointer-over value.</summary>
    public bool SetPointerOver(bool value, bool directlyOver, out bool wasOver)
    {
        wasOver = PointerOver;

        if (wasOver == value && PointerDirectlyOver == directlyOver)
        {
            return false;
        }

        PointerOver = value;
        PointerDirectlyOver = directlyOver;
        return true;
    }

    /// <summary>Commits press state when it differs from the current fact.</summary>
    public bool SetPressed(bool value)
    {
        if (Pressed == value)
        {
            return false;
        }

        Pressed = value;
        return true;
    }

    /// <summary>Commits collection-selected state when it differs from the current fact.</summary>
    public bool SetSelected(bool value)
    {
        if (Selected == value)
        {
            return false;
        }

        Selected = value;
        return true;
    }

    /// <summary>Commits collection-current state when it differs from the current fact.</summary>
    public bool SetCurrent(bool value)
    {
        if (Current == value)
        {
            return false;
        }

        Current = value;
        return true;
    }
}
