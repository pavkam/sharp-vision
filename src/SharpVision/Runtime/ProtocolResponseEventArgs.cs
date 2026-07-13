// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using SharpVision.Terminal.Protocols;

/// <summary>Provides one dispatcher-affine typed terminal protocol response.</summary>
public sealed class ProtocolResponseEventArgs: EventArgs
{
    /// <summary>Initializes an event payload for one immutable decoded response.</summary>
    /// <param name="response">The typed response received from the terminal.</param>
    public ProtocolResponseEventArgs(Response response) => Response = response;

    /// <summary>Gets the immutable decoded terminal response.</summary>
    public Response Response { get; }
}
