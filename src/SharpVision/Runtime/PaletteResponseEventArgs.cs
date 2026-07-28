// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

/// <summary>Provides one dispatcher-affine terminal palette response.</summary>
[PublicAPI]
public sealed class PaletteResponseEventArgs: EventArgs
{
    /// <summary>Initializes an event payload for one immutable color response.</summary>
    /// <param name="response">The typed response received from the terminal.</param>
    /// <exception cref="ArgumentException"><paramref name="response"/> is empty.</exception>
    public PaletteResponseEventArgs(PaletteResponse response)
    {
        if (response.IsEmpty)
        {
            throw new ArgumentException("The response cannot be empty.", nameof(response));
        }

        Response = response;
    }

    /// <summary>Gets the immutable decoded color response.</summary>
    public PaletteResponse Response { get; }
}
