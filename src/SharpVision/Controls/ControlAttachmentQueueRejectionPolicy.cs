// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Defines how attachment-affine fire-and-forget work handles synchronous queue rejection.</summary>
internal enum ControlAttachmentQueueRejectionPolicy
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
