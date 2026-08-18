// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Abstractions;

/// <summary>
/// Optionally receives one bounded Kitty graphics APC response instead of the synthetic
/// unsupported diagnostic fallback on <see cref="IProtocolSink"/>.
/// </summary>
[PublicAPI]
public interface IKittyGraphicsResponseSink
{
    /// <summary>Receives one bounded Kitty graphics APC response.</summary>
    /// <param name="value">The non-null owned graphics response.</param>
    public void Response(Kitty.Graphics.KittyGraphicsResponse value);
}
