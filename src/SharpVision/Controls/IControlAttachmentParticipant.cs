// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Owns dispatcher attachment and final disposal for one resource composed by a control.</summary>
/// <remarks>
/// This is the seam behind any helper whose lifetime should follow its owning
/// <see cref="ControlBase"/>'s dispatcher attachment rather than the owner's own construction or
/// disposal - for example <see cref="ControlTimer"/>. A control registers one through
/// <see cref="ControlBase.RegisterAttachmentParticipant"/> from its constructor, before the control
/// is ever attached; the framework then calls <see cref="OnOwnerAttached"/> and
/// <see cref="OnOwnerDetached"/> once per committed attachment and detachment, in registration
/// order, for as long as the owner lives, and disposes every registered participant when the owner
/// itself is disposed.
/// </remarks>
public interface IControlAttachmentParticipant: IDisposable
{
    /// <summary>Responds after the owner's dispatcher attachment commits.</summary>
    /// <param name="dispatcher">The exact committed owner dispatcher.</param>
    public void OnOwnerAttached(Dispatcher dispatcher);

    /// <summary>Responds after the owner's attachment has been invalidated.</summary>
    public void OnOwnerDetached();
}
