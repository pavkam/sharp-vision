// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Dispatches typed events over stable ancestry and handler snapshots.</summary>
[PublicAPI]
public static class Router
{
    /// <summary>Routes one typed event from a target according to its strategy.</summary>
    /// <typeparam name="TArgs">The exact event-argument type.</typeparam>
    /// <param name="target">The non-null original target.</param>
    /// <param name="routedEvent">The non-null typed identifier.</param>
    /// <param name="eventArgs">The non-null event payload and route state.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The attached target is accessed off-dispatcher, arguments are already routing,
    /// or an active modal plane excludes the target.
    /// </exception>
    /// <exception cref="ObjectDisposedException"><paramref name="target"/> is disposed.</exception>
    public static RouteResult Route<TArgs>(
        ControlBase target,
        Event<TArgs> routedEvent,
        TArgs eventArgs) where TArgs : RoutedEventArgs =>
        RouteCore(target, routedEvent, eventArgs, initialize: null);

    /// <summary>Routes one typed event after initializing it against a captured route immediately before preview.</summary>
    /// <typeparam name="TArgs">The exact event-argument type.</typeparam>
    /// <param name="target">The non-null original target.</param>
    /// <param name="routedEvent">The non-null typed identifier.</param>
    /// <param name="eventArgs">The non-null event payload and route state.</param>
    /// <param name="initialize">
    /// The non-null initializer invoked after route capture and argument activation but before preview handlers.
    /// </param>
    /// <returns>The handled state, post-route command, and captured source.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The attached target is accessed off-dispatcher, arguments are already routing,
    /// or an active modal plane excludes the target.
    /// </exception>
    /// <exception cref="ObjectDisposedException"><paramref name="target"/> is disposed.</exception>
    internal static RouteResult RouteInitialized<TArgs>(
        ControlBase target,
        Event<TArgs> routedEvent,
        TArgs eventArgs,
        Action<TArgs> initialize) where TArgs : RoutedEventArgs
    {
        ArgumentNullException.ThrowIfNull(initialize);
        return RouteCore(target, routedEvent, eventArgs, initialize);
    }

    private static RouteResult RouteCore<TArgs>(
        ControlBase target,
        Event<TArgs> routedEvent,
        TArgs eventArgs,
        Action<TArgs>? initialize) where TArgs : RoutedEventArgs
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(routedEvent);
        ArgumentNullException.ThrowIfNull(eventArgs);
        target.VerifyMutable();

        var modality = target.ModalityOwner;
        var boundary = modality?.BoundaryFor(target);

        if (modality?.Active is not null && boundary is null)
        {
            throw new InvalidOperationException("The routed target is outside the active modal plane.");
        }

        var depth = 0;

        for (var current = target; current is not null; current = current.Parent)
        {
            depth++;

            if (ReferenceEquals(current, boundary))
            {
                break;
            }
        }

        var route = ArrayPool<ControlBase>.Shared.Rent(depth);
        var index = 0;

        for (var current = target; current is not null; current = current.Parent)
        {
            route[index++] = current;

            if (ReferenceEquals(current, boundary))
            {
                break;
            }
        }

        Debug.Assert(index == depth, "The captured modal boundary remains in the route snapshot.");

        var sequence = Sequence.Current;
        var began = false;

        var result = default(RouteResult);

        try
        {
            eventArgs.Begin(target);
            began = true;
            initialize?.Invoke(eventArgs);

            if (routedEvent.Strategy == RoutingStrategy.TunnelBubble)
            {
                eventArgs.Phase = RoutingPhase.Preview;

                for (index = depth - 1; index >= 0; index--)
                {
                    if (eventArgs is PointerEventArgs previewPointer)
                    {
                        previewPointer.SetLocal(route[index]);
                    }

                    route[index].InvokeHandlers(routedEvent, eventArgs, sequence);
                }
            }

            eventArgs.Phase = RoutingPhase.Bubble;

            var bubbleCount = routedEvent.Strategy == RoutingStrategy.Direct ? 1 : depth;

            // Ancestry traversal is never cut short. Handled suppresses ordinary handlers and
            // default behavior, but handlers explicitly registered for handled events must still
            // run at every remaining node -- the per-registration filter decides that, not the
            // walk. The preview loop above already walks the full route, and stopping here was
            // the asymmetry. Nodes without handlers bail out inside InvokeHandlers, so walking
            // the rest of the ancestry stays allocation-free.
            for (index = 0; index < bubbleCount; index++)
            {
                var current = route[index];

                // Rebased for every visited node, not only ones with registered handlers —
                // InvokeDefault (and therefore OnEvent) below runs unconditionally regardless of
                // Handlers.Count, so a handler-less override reading LocalCells must still see
                // its own local coordinates rather than whatever the last handler-bearing
                // ancestor left behind.
                if (eventArgs is PointerEventArgs bubblePointer)
                {
                    bubblePointer.SetLocal(current);
                }

                current.InvokeHandlers(routedEvent, eventArgs, sequence);

                if (!eventArgs.Handled)
                {
                    // A node's own default runs immediately after its bubble
                    // handlers. This prevents an ancestor widget default from
                    // stealing navigation keys from a focused nested editor.
                    current.InvokeDefault(eventArgs);
                }
            }

            result = new RouteResult(eventArgs.Handled, eventArgs.PostRouteCommand, eventArgs.OriginalSource);
        }
        finally
        {
            if (began)
            {
                eventArgs.End();
            }

            Array.Clear(route, 0, depth);
            ArrayPool<ControlBase>.Shared.Return(route);
        }

        return result;
    }
}
