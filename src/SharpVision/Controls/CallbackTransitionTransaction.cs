// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.ComponentModel;
using System.Runtime.ExceptionServices;

/// <summary>Publishes one committed callback transition and preserves its earliest failure.</summary>
internal struct CallbackTransitionTransaction
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
    public void CaptureRequired(Action action) =>
        ExceptionAggregation.Capture(action, ref _failure);

    /// <summary>Runs dependent work only while this transition remains current.</summary>
    /// <param name="action">The non-null current-state work.</param>
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
    /// <param name="sender">The event sender.</param>
    /// <param name="eventArgs">The committed property payload.</param>
    public void PublishCurrent(
        PropertyChangedEventHandler? handlers,
        object sender,
        PropertyChangedEventArgs eventArgs)
    {
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
    /// <param name="sender">The event sender.</param>
    /// <param name="eventArgs">The event payload.</param>
    public void PublishCurrent(EventHandler? handlers, object sender, EventArgs eventArgs)
    {
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
    /// <param name="sender">The event sender.</param>
    /// <param name="eventArgs">The event payload.</param>
    public void PublishCurrent<TEventArgs>(
        EventHandler<TEventArgs>? handlers,
        object sender,
        TEventArgs eventArgs)
        where TEventArgs : EventArgs
    {
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
