// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Exposes owner-bound attachment participant registration for lifecycle tests.</summary>
internal sealed class AttachmentParticipantOwner: ControlBase
{
    /// <summary>Registers one participant through the protected owner seam.</summary>
    /// <param name="participant">The participant to register.</param>
    internal void Register(IControlAttachmentParticipant participant) =>
        RegisterAttachmentParticipant(participant);
}
