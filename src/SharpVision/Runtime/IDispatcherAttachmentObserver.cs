// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

/// <summary>Observes committed dispatcher attachment changes on a control.</summary>
/// <remarks>
/// Controls that must defer work until a dispatcher is available, or cancel that work on detach,
/// implement this contract so <see cref="ControlBase"/> can notify them without
/// expanding the public attachment API.
/// </remarks>
[PublicAPI]
public interface IDispatcherAttachmentObserver
{
    /// <summary>Responds after attachment commits and the current dispatcher becomes available.</summary>
    public void OnDispatcherAttached();

    /// <summary>Responds after detachment commits and the prior dispatcher becomes unavailable.</summary>
    public void OnDispatcherDetached();
}
