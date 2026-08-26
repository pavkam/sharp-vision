// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

/// <summary>Observes committed dispatcher attachment changes without expanding public control API.</summary>
internal interface IDispatcherAttachmentObserver
{
    /// <summary>Responds after attachment commits and the current dispatcher becomes available.</summary>
    internal void OnDispatcherAttached();

    /// <summary>Responds after detachment commits and the prior dispatcher becomes unavailable.</summary>
    internal void OnDispatcherDetached();
}
