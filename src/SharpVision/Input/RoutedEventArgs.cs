// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Provides controlled source, phase, and handled state for one route.</summary>
[PublicAPI]
public abstract class RoutedEventArgs: EventArgs
{
    /// <summary>Gets the control that started the current or most recent route.</summary>
    public ControlBase? OriginalSource { get; private set; }

    /// <summary>Gets the current logical source, which may be explicitly retargeted.</summary>
    public ControlBase? Source { get; private set; }

    /// <summary>Gets the active or most recent route phase.</summary>
    public RoutingPhase Phase { get; internal set; }

    /// <summary>Gets or sets whether ordinary handling and default behavior should stop.</summary>
    public bool IsHandled { get; set; }

    /// <summary>Gets the command to apply after the route, if any.</summary>
    public PostRouteCommand PostRouteCommand { get; private set; }

    private bool IsRouting { get; set; }

    /// <summary>Retargets the logical source without changing the original source.</summary>
    /// <param name="source">The non-null replacement logical source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="InvalidOperationException">No route is currently active.</exception>
    public void Retarget(ControlBase source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!IsRouting)
        {
            throw new InvalidOperationException("Source can be retargeted only during routing.");
        }

        Source = source;
    }

    /// <summary>Requests one command to be applied after this route completes.</summary>
    /// <param name="command">The requested non-none command.</param>
    public void RequestPostRouteCommand(PostRouteCommand command)
    {
        if (!IsRouting)
        {
            throw new InvalidOperationException("A post-route command can be requested only during routing.");
        }

        if (command == PostRouteCommand.None || !Enum.IsDefined(command))
        {
            throw new ArgumentOutOfRangeException(nameof(command), command,
                "A defined non-none post-route command is required.");
        }

        if (PostRouteCommand != PostRouteCommand.None && PostRouteCommand != command)
        {
            throw new InvalidOperationException("A route can request only one post-route command.");
        }

        PostRouteCommand = command;
    }

    /// <summary>Begins one route and resets per-route mutable state.</summary>
    internal void Begin(ControlBase source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (IsRouting)
        {
            throw new InvalidOperationException("Event arguments cannot be routed recursively.");
        }

        IsRouting = true;
        OriginalSource = source;
        Source = source;
        Phase = RoutingPhase.Preview;
        IsHandled = false;
        PostRouteCommand = PostRouteCommand.None;
    }

    /// <summary>Ends the active route while preserving observable final values.</summary>
    internal void End() => IsRouting = false;
}
