// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Discovery;

using Adapters;

/// <summary>Applies caller-supplied environment hints and conservative narrowing.</summary>
internal sealed class EnvironmentDiscoveryStrategy: IDiscoveryStrategy
{
    /// <summary>Initializes the environment evidence strategy.</summary>
    public EnvironmentDiscoveryStrategy()
    {
    }

    /// <summary>Gets the environment phase.</summary>
    public DiscoveryPhase Phase => DiscoveryPhase.Environment;

    /// <summary>Applies environment evidence without accessing process-global state.</summary>
    /// <param name="current">The non-null current capability snapshot.</param>
    /// <param name="context">The non-null immutable discovery context.</param>
    /// <returns>The original or refined capability snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="current"/> or <paramref name="context"/> is null.</exception>
    public TerminalCapabilities Apply(TerminalCapabilities current, DiscoveryContext context)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(context);

        return EnvironmentEvidenceAdapter.Apply(current, context.Environment);
    }
}
