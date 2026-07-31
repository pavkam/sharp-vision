// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision;

/// <summary>Provides one dispatcher-affine terminal capability-string response.</summary>
[PublicAPI]
public sealed class CapabilityResponseEventArgs: EventArgs
{
    /// <summary>Initializes an event payload for one immutable capability response.</summary>
    /// <param name="response">The non-null typed response received from the terminal.</param>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    public CapabilityResponseEventArgs(CapabilityResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        Response = response;
    }

    /// <summary>Gets the immutable decoded capability response.</summary>
    public CapabilityResponse Response { get; }
}
