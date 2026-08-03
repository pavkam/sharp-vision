// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Abstractions;

using Xterm;

/// <summary>
/// Optionally receives one validated iTerm2 OSC 1337 Capabilities feature-reporting reply
/// instead of the synthetic unsupported diagnostic fallback on <see cref="IProtocolSink"/>.
/// </summary>
[PublicAPI]
public interface IItermCapabilitiesResponseSink
{
    /// <summary>Receives one validated iTerm2 OSC 1337 Capabilities feature-reporting reply.</summary>
    /// <param name="value">The non-null immutable feature-reporting reply.</param>
    public void Response(ItermCapabilitiesResponse value);
}
