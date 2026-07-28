// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Discovery;

using SharpVision.Terminal.Discovery;

/// <summary>Supplies one invalid phase declaration for pipeline constructor validation tests.</summary>
internal sealed class UndefinedDiscoveryStrategy: IDiscoveryStrategy
{
    /// <summary>Initializes the invalid discovery strategy test collaborator.</summary>
    internal UndefinedDiscoveryStrategy()
    {
    }

    /// <summary>Gets an undefined phase value.</summary>
    public DiscoveryPhase Phase => (DiscoveryPhase) (-1);

    /// <summary>Validates the strategy contract without refining its input.</summary>
    /// <param name="current">The non-null current capability snapshot.</param>
    /// <param name="context">The non-null immutable discovery context.</param>
    /// <returns>The original immutable capability snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="current"/> or <paramref name="context"/> is null.</exception>
    public TerminalCapabilities Apply(TerminalCapabilities current, DiscoveryContext context)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(context);

        return current;
    }
}
