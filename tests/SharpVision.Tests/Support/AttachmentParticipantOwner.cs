// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Exposes owner-bound attachment seams - participant registration and the deferred-
/// completion helpers - for lifecycle tests.</summary>
internal sealed class AttachmentParticipantOwner: ControlBase
{
    /// <summary>Registers one participant through the protected owner seam.</summary>
    /// <param name="participant">The participant to register.</param>
    internal void Register(IControlAttachmentParticipant participant) =>
        RegisterAttachmentParticipant(participant);

    /// <summary>Exposes the protected background-completion seam for direct invocation.</summary>
    /// <param name="token">The exact captured attachment.</param>
    /// <param name="action">The callback to post.</param>
    /// <param name="isOperationCurrent">An optional additional domain-current predicate.</param>
    /// <param name="onDiscarded">Optional cleanup when queued work is superseded.</param>
    /// <param name="onAbandoned">Optional cleanup when the queue cannot accept the callback.</param>
    internal void PostBackgroundCompletion(
        ControlAttachmentToken token,
        Action action,
        Func<bool>? isOperationCurrent = null,
        Action? onDiscarded = null,
        Action? onAbandoned = null) =>
        PostBackgroundCompletionForCurrentAttachment(token, action, isOperationCurrent, onDiscarded, onAbandoned);

    /// <summary>Exposes the protected recapture-and-dispatch seam for direct invocation.</summary>
    /// <param name="captured">The attachment captured when the operation began, or null.</param>
    /// <param name="action">The completion to run.</param>
    /// <param name="isOperationCurrent">An optional additional domain-current predicate.</param>
    internal void DispatchCompletion(
        ControlAttachmentToken? captured,
        Action action,
        Func<bool>? isOperationCurrent = null) =>
        DispatchToCurrentAttachment(captured, action, isOperationCurrent);
}
