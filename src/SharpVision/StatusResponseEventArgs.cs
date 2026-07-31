// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision;

/// <summary>Provides one dispatcher-affine terminal status-string response.</summary>
[PublicAPI]
public sealed class StatusResponseEventArgs: EventArgs
{
    /// <summary>Initializes an event payload for one immutable status response.</summary>
    /// <param name="response">The typed response received from the terminal.</param>
    /// <exception cref="ArgumentException"><paramref name="response"/> is empty.</exception>
    public StatusResponseEventArgs(StatusResponse response)
    {
        if (response.IsEmpty)
        {
            throw new ArgumentException("The response cannot be empty.", nameof(response));
        }

        Response = response;
    }

    /// <summary>Gets the immutable decoded status response.</summary>
    public StatusResponse Response { get; }
}
