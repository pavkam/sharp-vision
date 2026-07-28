// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics;

using Capabilities;

using CellMetrics = Metrics;

/// <summary>Supplies one render transaction's terminal profile and measured cell-pixel geometry.</summary>
internal readonly struct GraphicsContext
{
    /// <summary>Initializes one renderer-owned graphics context.</summary>
    /// <param name="profile">The non-null active terminal profile.</param>
    /// <param name="metrics">Exact measured geometry, or null when unavailable.</param>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
    public GraphicsContext(TerminalProfile profile, CellMetrics? metrics)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Profile = profile;
        Metrics = metrics;
    }

    /// <summary>Gets the active profile, or null for a direct backend test using the default context.</summary>
    public TerminalProfile? Profile { get; }

    /// <summary>Gets measured cell-pixel geometry, or null when no exact measurement exists.</summary>
    public CellMetrics? Metrics { get; }
}
