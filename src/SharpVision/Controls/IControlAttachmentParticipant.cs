// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Owns dispatcher attachment and final disposal for one resource composed by a control.</summary>
internal interface IControlAttachmentParticipant: IDisposable
{
    /// <summary>Responds after the owner's dispatcher attachment commits.</summary>
    /// <param name="dispatcher">The exact committed owner dispatcher.</param>
    internal void OnOwnerAttached(Dispatcher dispatcher);

    /// <summary>Responds after the owner's attachment has been invalidated.</summary>
    internal void OnOwnerDetached();
}
