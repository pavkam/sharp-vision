// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Multiplexing;

/// <summary>Identifies finite typed operation families eligible for passthrough.</summary>
[Flags]
[PublicAPI]
public enum Operation
{
    /// <summary>No operation may bypass the multiplexer.</summary>
    None = 0,

    /// <summary>Bounded typed terminal capability queries may bypass the multiplexer.</summary>
    CapabilityQueries = 1,

    /// <summary>Typed clipboard transactions may bypass the multiplexer.</summary>
    Clipboard = 2,

    /// <summary>Typed graphics commands may bypass the multiplexer.</summary>
    Graphics = 4
}
