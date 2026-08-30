// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision;

/// <summary>Provides one dispatcher-published terminal diagnostics transition.</summary>
[PublicAPI]
public sealed class TerminalDiagnosticsChangedEventArgs: EventArgs
{
    /// <summary>Initializes one immutable diagnostics transition.</summary>
    /// <param name="previous">The non-null previous snapshot.</param>
    /// <param name="current">The non-null current snapshot.</param>
    /// <exception cref="ArgumentNullException">A snapshot is null.</exception>
    public TerminalDiagnosticsChangedEventArgs(
        TerminalDiagnostics previous,
        TerminalDiagnostics current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        Previous = previous;
        Current = current;
    }

    /// <summary>Gets the immutable snapshot active before the transition.</summary>
    public TerminalDiagnostics Previous { get; }

    /// <summary>Gets the immutable snapshot active after the transition.</summary>
    public TerminalDiagnostics Current { get; }
}
