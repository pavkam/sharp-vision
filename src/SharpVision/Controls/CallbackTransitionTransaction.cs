// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.ComponentModel;
using System.Runtime.ExceptionServices;

/// <summary>Publishes one committed callback transition and preserves its earliest failure.</summary>
/// <remarks>
/// A derived control receives one instance from <c>SetTransitionProperty</c> or
/// <c>BeginPropertyTransition</c> on <see cref="ControlBase"/> for the property that began the
/// transition, then calls <see cref="PublishCurrent{TEventArgs}(EventHandler{TEventArgs}?, object, TEventArgs)"/>
/// (or one of its overloads) to deliver any further typed events the same transition raises, and
/// finally calls <see cref="ThrowIfFailed"/> once every event for the transition has been offered a
/// chance to run. Mutation is intrinsic to this type: it accumulates the earliest observer failure
/// across every publication call made against it, so it is passed by <c>ref</c> or returned by value
/// and reused for the lifetime of one transition rather than copied.
/// </remarks>
public struct CallbackTransitionTransaction
{
    private readonly CallbackTransitionToken _token;
    private ExceptionDispatchInfo? _failure;

    /// <summary>Initializes a transaction for one committed token.</summary>
    /// <param name="token">The immutable current identity.</param>
    internal CallbackTransitionTransaction(CallbackTransitionToken token) => _token = token;

    /// <summary>Gets whether this transition still owns callback continuation.</summary>
    public readonly bool IsCurrent => _token.IsCurrent;

    /// <summary>Runs mandatory work even after supersession and captures its failure.</summary>
    /// <param name="action">The non-null invariant work.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    public void CaptureRequired(Action action) =>
        ExceptionAggregation.Capture(action, ref _failure);

    /// <summary>Runs dependent work only while this transition remains current.</summary>
    /// <param name="action">The non-null current-state work.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    public void CaptureIfCurrent(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsCurrent)
        {
            ExceptionAggregation.Capture(action, ref _failure);
        }
    }

    /// <summary>Publishes a property event to captured subscribers until superseded.</summary>
    /// <param name="handlers">The captured multicast delegate, or null.</param>
    /// <param name="sender">The non-null event sender.</param>
    /// <param name="eventArgs">The committed property payload.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sender"/> is null.</exception>
    public void PublishCurrent(
        PropertyChangedEventHandler? handlers,
        object sender,
        PropertyChangedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(sender);

        if (handlers is null || !IsCurrent)
        {
            return;
        }

        if (handlers.HasSingleTarget)
        {
            try
            {
                handlers(sender, eventArgs);
            }
            catch (Exception exception)
            {
                _failure ??= ExceptionDispatchInfo.Capture(exception);
            }
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            if (!IsCurrent)
            {
                break;
            }

            try
            {
                ((PropertyChangedEventHandler) handler)(sender, eventArgs);
            }
            catch (Exception exception)
            {
                _failure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }
    }

    /// <summary>Publishes a non-generic event to captured subscribers until superseded.</summary>
    /// <param name="handlers">The captured multicast delegate, or null.</param>
    /// <param name="sender">The non-null event sender.</param>
    /// <param name="eventArgs">The event payload.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sender"/> is null.</exception>
    public void PublishCurrent(EventHandler? handlers, object sender, EventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(sender);

        if (handlers is null || !IsCurrent)
        {
            return;
        }

        if (handlers.HasSingleTarget)
        {
            try
            {
                handlers(sender, eventArgs);
            }
            catch (Exception exception)
            {
                _failure ??= ExceptionDispatchInfo.Capture(exception);
            }
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            if (!IsCurrent)
            {
                break;
            }

            try
            {
                ((EventHandler) handler)(sender, eventArgs);
            }
            catch (Exception exception)
            {
                _failure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }
    }

    /// <summary>Publishes a typed event to captured subscribers until superseded.</summary>
    /// <typeparam name="TEventArgs">The event payload type.</typeparam>
    /// <param name="handlers">The captured multicast delegate, or null.</param>
    /// <param name="sender">The non-null event sender.</param>
    /// <param name="eventArgs">The event payload.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sender"/> is null.</exception>
    public void PublishCurrent<TEventArgs>(
        EventHandler<TEventArgs>? handlers,
        object sender,
        TEventArgs eventArgs)
        where TEventArgs : EventArgs
    {
        ArgumentNullException.ThrowIfNull(sender);

        if (handlers is null || !IsCurrent)
        {
            return;
        }

        if (handlers.HasSingleTarget)
        {
            try
            {
                handlers(sender, eventArgs);
            }
            catch (Exception exception)
            {
                _failure ??= ExceptionDispatchInfo.Capture(exception);
            }
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            if (!IsCurrent)
            {
                break;
            }

            try
            {
                ((EventHandler<TEventArgs>) handler)(sender, eventArgs);
            }
            catch (Exception exception)
            {
                _failure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }
    }

    /// <summary>Rethrows the earliest captured callback failure, if any.</summary>
    public readonly void ThrowIfFailed() => _failure?.Throw();

}
