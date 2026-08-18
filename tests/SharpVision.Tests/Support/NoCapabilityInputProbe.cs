// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>An <see cref="InputBase"/> derivative that enables no capability at all, proving the
/// unconditional base contract (focusable, Tab-stopping) is the only thing every input pays for.</summary>
internal sealed class NoCapabilityInputProbe: InputBase
{
    /// <summary>Calls the protected off-dispatcher/disposed guard through a public seam.</summary>
    internal void ProbeVerifyMutable() => VerifyMutable();

    /// <summary>Reads IsOpen through the protected seam without ever enabling the popup capability.</summary>
    internal bool ProbeGetOpened() => IsOpen;

    /// <summary>Writes IsOpen through the protected seam without ever enabling the popup capability.</summary>
    internal void ProbeSetOpened(bool value) => IsOpen = value;
}
