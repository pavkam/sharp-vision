// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Backends;

/// <summary>Describes one immutable protocol-extension family in backend composition order.</summary>
internal readonly record struct ProtocolExtension
{
    /// <summary>Initializes one known protocol-extension descriptor.</summary>
    /// <param name="kind">The protocol-extension family.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is unknown.</exception>
    public ProtocolExtension(ProtocolExtensionKind kind)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(kind, nameof(kind), "The protocol extension kind is unknown.");

        Kind = kind;
    }

    /// <summary>Gets the protocol-extension family.</summary>
    public ProtocolExtensionKind Kind { get; }
}
