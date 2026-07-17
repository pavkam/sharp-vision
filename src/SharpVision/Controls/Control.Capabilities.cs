// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;

/// <summary>Provides inherited immutable terminal capability context.</summary>
public abstract partial class Control
{
    /// <summary>Gets the immutable terminal capability profile inherited from the application.</summary>
    protected TerminalCapabilities Capabilities { get; private set; } = TerminalCapabilities.Conservative;

    /// <summary>Gets the inherited capability profile for framework-owned descendants.</summary>
    internal TerminalCapabilities CapabilityContext => Capabilities;

    /// <summary>Publishes one immutable capability profile across this complete subtree.</summary>
    /// <param name="value">The non-null profile to inherit.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The attached tree is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">This control or an owned descendant is disposed.</exception>
    internal void SetCapabilities(TerminalCapabilities value)
    {
        ArgumentNullException.ThrowIfNull(value);
        VerifyMutable();
        var changed = new List<(Control Control, TerminalCapabilities Previous)>();
        CommitCapabilities(value, changed);
        ExceptionDispatchInfo? failure = null;

        foreach (var entry in changed)
        {
            CaptureFailure(
                () => entry.Control.OnCapabilitiesChanged(entry.Previous, value),
                ref failure);
        }

        failure?.Throw();
    }

    /// <summary>Responds after one inherited terminal capability profile commits.</summary>
    /// <param name="previous">The non-null previous immutable profile.</param>
    /// <param name="current">The non-null committed immutable profile.</param>
    protected virtual void OnCapabilitiesChanged(
        TerminalCapabilities previous,
        TerminalCapabilities current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
    }

    private void CommitCapabilities(
        TerminalCapabilities value,
        List<(Control Control, TerminalCapabilities Previous)> changed)
    {
        ThrowIfDisposed();
        var previous = Capabilities;

        if (!ReferenceEquals(previous, value))
        {
            Capabilities = value;
            changed.Add((this, previous));
        }

        VisitChildren(child => child.CommitCapabilities(value, changed));
    }
}
