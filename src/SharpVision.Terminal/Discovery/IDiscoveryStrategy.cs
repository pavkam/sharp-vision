// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Discovery;

/// <summary>Applies one fixed-precedence source of immutable capability evidence.</summary>
internal interface IDiscoveryStrategy
{
    /// <summary>Gets the unique pipeline phase owned by this strategy.</summary>
    public DiscoveryPhase Phase { get; }

    /// <summary>Applies this phase to an immutable capability snapshot.</summary>
    /// <param name="current">The non-null current capability snapshot.</param>
    /// <param name="context">The non-null owned discovery evidence.</param>
    /// <returns>The original or a refined immutable capability snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="current"/> or <paramref name="context"/> is null.</exception>
    public TerminalCapabilities Apply(TerminalCapabilities current, DiscoveryContext context);
}
