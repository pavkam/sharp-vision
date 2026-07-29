// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics.Backends;

using Capabilities;

using MultiplexerRoute = Multiplexing.Route;

/// <summary>Selects one renderer backend from authoritative evidence and route policy.</summary>
internal static class GraphicsBackendSelector
{
    /// <summary>Creates Kitty or a frame-ordered mixed non-retained backend, or null for fallback.</summary>
    /// <param name="capabilities">The non-null final negotiated capability evidence.</param>
    /// <param name="route">An optional explicit graphics route.</param>
    /// <param name="maxPreparedBytes">The positive complete prepared transaction bound.</param>
    /// <returns>An owned backend, or null when no safe protocol is proven.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="capabilities"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxPreparedBytes"/> is not positive.</exception>
    public static IGraphicsBackend? Create(
        TerminalCapabilities capabilities,
        MultiplexerRoute? route = null,
        int maxPreparedBytes = 16 * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPreparedBytes);

        if (route is not null && !route.CanRouteGraphics)
        {
            return null;
        }

        if (IsAuthoritative(capabilities.KittyGraphics, allowQuery: true))
        {
            return new KittyGraphicsBackend(maxPreparedBytes: maxPreparedBytes, route: route);
        }

        var sixel = IsAuthoritative(capabilities.Sixel, allowQuery: true);
        var iterm = IsAuthoritative(capabilities.ItermImages, allowQuery: false);

        return sixel || iterm
            ? new NonRetainedGraphicsBackend(sixel, iterm, maxPreparedBytes, route)
            : null;
    }

    /// <summary>Tests whether current immutable evidence authorizes protocol output.</summary>
    /// <param name="feature">The current immutable feature evidence.</param>
    /// <param name="allowQuery">Whether correlated query evidence may authorize this protocol.</param>
    /// <returns>True only when support and origin authorize output.</returns>
    public static bool IsAuthoritative(Feature feature, bool allowQuery) =>
        feature.State == Support.Supported &&
        (feature.Origin == Origin.Override || (allowQuery && feature.Origin == Origin.Query));
}
