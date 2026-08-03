// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Abstractions;

/// <summary>
/// Optionally receives one decoded Kitty OSC 5522 clipboard packet instead of the synthetic
/// unsupported diagnostic fallback on <see cref="IProtocolSink"/>.
/// </summary>
[PublicAPI]
public interface IKittyClipboardPacketSink
{
    /// <summary>Receives one decoded Kitty OSC 5522 clipboard packet.</summary>
    /// <param name="value">The non-null owned clipboard packet.</param>
    public void Response(Kitty.Clipboard.Packet value);
}
