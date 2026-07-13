// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Controls;

/// <summary>Provides controlled source, phase, and handled state for one route.</summary>
public abstract class RoutedEventArgs: EventArgs
{
    /// <summary>Gets the control that started the current or most recent route.</summary>
    public Control? OriginalSource { get; private set; }

    /// <summary>Gets the current logical source, which may be explicitly retargeted.</summary>
    public Control? Source { get; private set; }

    /// <summary>Gets the active or most recent route phase.</summary>
    public Phase Phase { get; internal set; }

    /// <summary>Gets or sets whether ordinary handling and default behavior should stop.</summary>
    public bool Handled { get; set; }

    private bool IsRouting { get; set; }

    /// <summary>Retargets the logical source without changing the original source.</summary>
    /// <param name="source">The non-null replacement logical source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="InvalidOperationException">No route is currently active.</exception>
    public void Retarget(Control source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!IsRouting)
        {
            throw new InvalidOperationException("Source can be retargeted only during routing.");
        }

        Source = source;
    }

    /// <summary>Begins one route and resets per-route mutable state.</summary>
    internal void Begin(Control source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (IsRouting)
        {
            throw new InvalidOperationException("Event arguments cannot be routed recursively.");
        }

        IsRouting = true;
        OriginalSource = source;
        Source = source;
        Phase = Phase.Preview;
        Handled = false;
    }

    /// <summary>Ends the active route while preserving observable final values.</summary>
    internal void End() => IsRouting = false;
}
