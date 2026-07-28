// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

/// <summary>Provides one dispatcher-affine terminal metrics response.</summary>
[PublicAPI]
public sealed class MetricsResponseEventArgs: EventArgs
{
    /// <summary>Initializes an event payload for one immutable geometry response.</summary>
    /// <param name="response">The typed response received from the terminal.</param>
    /// <exception cref="ArgumentException"><paramref name="response"/> is empty.</exception>
    public MetricsResponseEventArgs(MetricsResponse response)
    {
        if (response.IsEmpty)
        {
            throw new ArgumentException("The response cannot be empty.", nameof(response));
        }

        Response = response;
    }

    /// <summary>Gets the immutable decoded geometry response.</summary>
    public MetricsResponse Response { get; }
}
