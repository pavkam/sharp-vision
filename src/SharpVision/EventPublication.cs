// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision;

using System.Runtime.ExceptionServices;

/// <summary>Publishes a multicast delegate to every subscriber with per-handler exception isolation.</summary>
/// <remarks>
/// A non-control collaborator - such as <see cref="Application"/>, a dispatcher, or
/// <see cref="FocusManager"/> - uses this static helper directly for the same
/// "publish until superseded, aggregate failures" contract that
/// <see cref="CallbackTransitionTransaction"/> gives a <see cref="ControlBase"/>
/// subclass. A type that already derives from <see cref="ControlBase"/> uses its own
/// property-commit and transition seams instead of calling this helper directly, so publication and
/// invalidation stay coordinated through one owner.
/// </remarks>
public static class EventPublication
{
    /// <summary>Invokes every subscriber of <paramref name="handlers"/>, capturing only the first thrown
    /// exception and rethrowing it after every eligible subscriber has run.</summary>
    /// <param name="handlers">The multicast delegate to publish, or null (a no-op).</param>
    /// <param name="isStillValid">Checked before each subscriber; publication stops (without error) the
    /// first time this returns false.</param>
    /// <param name="invoke">Invokes one subscriber, already cast to its concrete delegate type.</param>
    /// <exception cref="ArgumentNullException"><paramref name="isStillValid"/> or
    /// <paramref name="invoke"/> is null.</exception>
    public static void Publish<TSubscriber>(Delegate? handlers, Func<bool> isStillValid, Action<TSubscriber> invoke)
        where TSubscriber : Delegate
    {
        ArgumentNullException.ThrowIfNull(isStillValid);
        ArgumentNullException.ThrowIfNull(invoke);

        if (handlers is null)
        {
            return;
        }

        ExceptionDispatchInfo? failure = null;

        foreach (var subscriber in handlers.GetInvocationList())
        {
            if (!isStillValid())
            {
                break;
            }

            try
            {
                invoke((TSubscriber) subscriber);
            }
            catch (Exception exception)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        failure?.Throw();
    }
}
