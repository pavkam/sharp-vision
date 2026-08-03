// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Abstractions;

using Xterm;

/// <summary>
/// Optionally receives one validated palette or dynamic-color response instead of the
/// numeric-adapted fallback on <see cref="IProtocolSink"/>.
/// </summary>
[PublicAPI]
public interface IPaletteResponseSink
{
    /// <summary>Receives one validated palette or dynamic-color response. Color components
    /// retain their normalized 16-bit values.</summary>
    /// <param name="value">The immutable color response.</param>
    public void Response(in PaletteResponse value);
}
