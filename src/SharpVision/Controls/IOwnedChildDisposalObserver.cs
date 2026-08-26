// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Allows an owning presentation control to repair semantic state before a caller
/// directly disposes one retained child.</summary>
internal interface IOwnedChildDisposalObserver
{
    /// <summary>Responds before the child's disposal publication begins.</summary>
    /// <param name="child">The directly owned child whose public disposal was requested.</param>
    internal void OnOwnedChildDisposalRequested(ControlBase child);
}
