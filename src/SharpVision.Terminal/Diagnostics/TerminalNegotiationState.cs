// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Diagnostics;

/// <summary>Describes bounded startup capability-negotiation progress.</summary>
[PublicAPI]
public enum TerminalNegotiationState
{
    /// <summary>No active query batch was configured.</summary>
    Disabled,

    /// <summary>A bounded query batch is awaiting completion or expiry.</summary>
    Pending,

    /// <summary>The query batch published its final owned results.</summary>
    Completed
}
