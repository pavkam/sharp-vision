// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Abstractions;

using Xterm;

/// <summary>
/// Optionally receives one validated bounded XTGETTCAP response instead of the synthetic
/// unsupported diagnostic fallback on <see cref="IProtocolSink"/>.
/// </summary>
[PublicAPI]
public interface ICapabilityResponseSink
{
    /// <summary>Receives one validated bounded XTGETTCAP response.</summary>
    /// <param name="value">The non-null immutable capability response.</param>
    public void Response(CapabilityResponse value);
}
