// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Abstractions;

/// <summary>
/// Optionally receives one decoded OSC 52 clipboard reply instead of the synthetic
/// unsupported diagnostic fallback on <see cref="IProtocolSink"/>.
/// </summary>
[PublicAPI]
public interface IClipboardReplySink
{
    /// <summary>Receives one decoded OSC 52 clipboard reply.</summary>
    /// <param name="value">The immutable owned clipboard reply.</param>
    public void Response(in Clipboard.ClipboardReply value);
}
