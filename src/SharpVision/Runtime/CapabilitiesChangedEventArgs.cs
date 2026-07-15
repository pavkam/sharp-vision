// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;


/// <summary>Provides one dispatcher-published capability profile change.</summary>
public sealed class CapabilitiesChangedEventArgs: EventArgs
{
    /// <summary>Initializes one immutable capability transition.</summary>
    /// <param name="previous">The non-null previously active profile.</param>
    /// <param name="current">The non-null newly active profile.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="previous"/> or <paramref name="current"/> is null.
    /// </exception>
    public CapabilitiesChangedEventArgs(
        TerminalCapabilities previous,
        TerminalCapabilities current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        Previous = previous;
        Current = current;
    }

    /// <summary>Gets the immutable profile active before this transition.</summary>
    public TerminalCapabilities Previous { get; }

    /// <summary>Gets the immutable profile active after this transition.</summary>
    public TerminalCapabilities Current { get; }
}
