// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Abstractions;

using Xterm;

/// <summary>
/// Optionally receives one validated pixel or cell metrics response instead of the
/// numeric-adapted fallback on <see cref="IProtocolSink"/>.
/// </summary>
[PublicAPI]
public interface IMetricsResponseSink
{
    /// <summary>Receives one validated pixel or cell metrics response.</summary>
    /// <param name="value">The immutable metrics response.</param>
    public void Response(in MetricsResponse value);
}
