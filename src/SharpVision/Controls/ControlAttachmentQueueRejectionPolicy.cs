// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Defines how attachment-affine fire-and-forget work handles synchronous queue rejection.</summary>
/// <remarks>
/// Passed to <see cref="ControlBase.PostForCurrentAttachment"/>, which posts a callback that only
/// runs while a captured <see cref="ControlAttachmentToken"/> stays current. Synchronous rejection
/// - the target dispatcher's queue is full or already disposed - is a distinct failure from that
/// staleness check, and this policy governs only the former.
/// </remarks>
public enum ControlAttachmentQueueRejectionPolicy
{
    /// <summary>Propagates the queue rejection to the caller.</summary>
    Throw,

    /// <summary>Drops rejected work without another observable effect.</summary>
    Drop,

    /// <summary>Attempts to report rejection through the dispatcher's callback-failure path.</summary>
    Report,

    /// <summary>Runs the caller's discard cleanup synchronously.</summary>
    RunCleanup
}
