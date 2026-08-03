// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision;

/// <summary>Provides one dispatcher-affine typed terminal protocol response.</summary>
[PublicAPI]
public sealed class ProtocolResponseEventArgs: EventArgs
{
    /// <summary>Initializes an event payload for one immutable decoded response.</summary>
    /// <param name="response">The typed response received from the terminal.</param>
    public ProtocolResponseEventArgs(XtermCapabilitiesResponse response) => Response = response;

    /// <summary>Gets the immutable decoded terminal response.</summary>
    public XtermCapabilitiesResponse Response { get; }
}
