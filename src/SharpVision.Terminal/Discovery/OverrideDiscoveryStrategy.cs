// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Discovery;

using Adapters;

/// <summary>Applies explicit caller policy after all inferred evidence phases.</summary>
internal sealed class OverrideDiscoveryStrategy: IDiscoveryStrategy
{
    /// <summary>Initializes the explicit override evidence strategy.</summary>
    public OverrideDiscoveryStrategy()
    {
    }

    /// <summary>Gets the explicit override phase.</summary>
    public DiscoveryPhase Phase => DiscoveryPhase.Override;

    /// <summary>Applies explicit caller settings.</summary>
    /// <param name="current">The non-null current capability snapshot.</param>
    /// <param name="context">The non-null immutable discovery context.</param>
    /// <returns>The original or refined capability snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="current"/> or <paramref name="context"/> is null.</exception>
    public TerminalCapabilities Apply(TerminalCapabilities current, DiscoveryContext context)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(context);

        return current.Apply(context.Overrides);
    }
}
