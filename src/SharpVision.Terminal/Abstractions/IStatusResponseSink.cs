// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Abstractions;

using Xterm;

/// <summary>
/// Optionally receives one validated bounded DECRQSS response instead of the synthetic
/// unsupported diagnostic fallback on <see cref="IProtocolSink"/>.
/// </summary>
[PublicAPI]
public interface IStatusResponseSink
{
    /// <summary>Receives one validated bounded DECRQSS response.</summary>
    /// <param name="value">The immutable owned status response.</param>
    public void Response(in StatusResponse value);
}
