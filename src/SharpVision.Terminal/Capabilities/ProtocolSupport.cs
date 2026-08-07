// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Pairs one terminal protocol with its detected support evidence.</summary>
[PublicAPI]
public readonly record struct ProtocolSupport
{
    /// <summary>Initializes a validated protocol/feature pair.</summary>
    /// <param name="protocol">The protocol.</param>
    /// <param name="feature">The support evidence.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="protocol"/> is unknown.</exception>
    public ProtocolSupport(TerminalProtocol protocol, Feature feature)
    {
        if (!Enum.IsDefined(protocol))
        {
            throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "The terminal protocol is unknown.");
        }

        Protocol = protocol;
        Feature = feature;
    }

    /// <summary>Gets the protocol.</summary>
    public TerminalProtocol Protocol { get; }

    /// <summary>Gets the support evidence.</summary>
    public Feature Feature { get; }
}
