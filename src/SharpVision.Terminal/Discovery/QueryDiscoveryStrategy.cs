// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Discovery;

using Adapters;

/// <summary>Applies bounded capability-query results after environment evidence.</summary>
internal sealed class QueryDiscoveryStrategy: IDiscoveryStrategy
{
    /// <summary>Initializes the bounded query evidence strategy.</summary>
    public QueryDiscoveryStrategy()
    {
    }

    /// <summary>Gets the query phase.</summary>
    public DiscoveryPhase Phase => DiscoveryPhase.Query;

    /// <summary>Applies bounded query evidence.</summary>
    /// <param name="current">The non-null current capability snapshot.</param>
    /// <param name="context">The non-null immutable discovery context.</param>
    /// <returns>The original or refined capability snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="current"/> or <paramref name="context"/> is null.</exception>
    public TerminalCapabilities Apply(TerminalCapabilities current, DiscoveryContext context)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(context);

        return current.Apply(context.Queries, context.Environment);
    }
}
