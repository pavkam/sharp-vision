// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using System.Runtime.ExceptionServices;

/// <summary>Owns one current modal-scope identity and its policy callbacks.</summary>
/// <remarks>
/// <para>
/// The session clears identity before external callbacks, so a callback may install a replacement
/// without stale cleanup erasing it. Callers retain presentation policy and supply only currentness,
/// dismissal, external-exit, and failed-entry rollback behavior.
/// </para>
/// <para>
/// A session tracks at most one active scope at a time. <see cref="IsEntering"/> guards the window
/// between calling the caller's <c>enterScope</c> delegate and committing (or rejecting) the
/// resulting <see cref="ModalScope"/>: a nested call to <see cref="Enter"/> from inside that window -
/// for example a focus-change callback the entry delegate itself triggers - would observe a session
/// that is neither active nor idle, so it throws instead of racing the outer call to a conclusion. A
/// sequential call while a scope is already active is rejected too: an owner asks
/// <see cref="IsActive"/> before entering again, because two active scopes for one owner would
/// leave the manager and the session disagreeing about which one dismissal addresses.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class ModalSession
{
    /// <summary>Gets the optional current dismissal policy.</summary>
    private Action<ModalScope>? DismissPolicy { get; }

    /// <summary>Gets the optional external-exit policy.</summary>
    private Action<ModalScope>? ExitPolicy { get; }

    /// <summary>Initializes an empty session with optional family policy callbacks.</summary>
    /// <param name="dismissRequested">Invoked for a current active dismissal request.</param>
    /// <param name="exited">Invoked after a current scope exits and its identity clears.</param>
    public ModalSession(
        Action<ModalScope>? dismissRequested = null,
        Action<ModalScope>? exited = null)
    {
        DismissPolicy = dismissRequested;
        ExitPolicy = exited;
    }

    /// <summary>Gets the exact tracked scope, including an inactive scope awaiting exit callback.</summary>
    public ModalScope? Current { get; private set; }

    /// <summary>Gets whether the tracked scope remains active.</summary>
    public bool IsActive => Current is { IsActive: true };

    /// <summary>Gets whether one entry delegate is currently executing.</summary>
    public bool IsEntering { get; private set; }

    /// <summary>Enters, validates, and tracks one modal scope transaction.</summary>
    /// <param name="enterScope">Creates the candidate manager-owned scope.</param>
    /// <param name="isCurrent">Validates caller presentation identity after entry callbacks.</param>
    /// <param name="rollback">Optionally restores caller presentation after failed entry.</param>
    /// <returns>The candidate scope, active or inactive according to manager and callback outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="enterScope"/> or
    /// <paramref name="isCurrent"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Entry is reentered from inside an in-progress <paramref name="enterScope"/> or
    /// <paramref name="isCurrent"/> call, or this session already owns an active scope.
    /// </exception>
    /// <exception cref="Exception">Entry or cleanup fails; an initiating entry failure remains authoritative.</exception>
    public ModalScope Enter(
        Func<ModalScope> enterScope,
        Func<bool> isCurrent,
        Action? rollback = null)
    {
        ArgumentNullException.ThrowIfNull(enterScope);
        ArgumentNullException.ThrowIfNull(isCurrent);

        if (IsEntering)
        {
            throw new InvalidOperationException("Modal session entry cannot be reentered.");
        }

        if (IsActive)
        {
            throw new InvalidOperationException("The modal session already owns an active scope.");
        }

        ClearInactive();
        IsEntering = true;
        ModalScope? scope = null;

        try
        {
            scope = enterScope();

            if (!scope.IsActive)
            {
                return scope;
            }

            if (!isCurrent())
            {
                scope.Dispose();
                InvokeRollback();
                return scope;
            }

            Current = scope;
            scope.DismissRequested += OnDismissRequested;
            scope.Exited += OnExited;
            return scope;
        }
        catch (Exception exception)
        {
            var failure = ExceptionDispatchInfo.Capture(exception);

            if (scope is not null)
            {
                Clear(scope);

                if (scope.IsActive)
                {
                    try
                    {
                        scope.Dispose();
                    }
                    catch
                    {
                        // Entry remains the authoritative failure.
                    }
                }
            }

            InvokeRollback();

            failure.Throw();
            throw;
        }
        finally
        {
            IsEntering = false;
        }

        void InvokeRollback()
        {
            if (rollback is not null)
            {
                try
                {
                    rollback();
                }
                catch
                {
                    // Entry remains the authoritative failure.
                }
            }
        }
    }

    /// <summary>Clears and ends the exact current scope, if any.</summary>
    /// <remarks>A no-op when no scope is tracked, including while <see cref="IsEntering"/> is true -
    /// entry has not yet committed a scope for this method to end.</remarks>
    /// <exception cref="Exception">Modal focus restoration or an exit callback fails after cleanup.</exception>
    public void Exit()
    {
        if (Current is not { } scope)
        {
            return;
        }

        Clear(scope);

        if (scope.IsActive)
        {
            scope.Dispose();
        }
    }

    private void OnDismissRequested(object? sender, EventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is ModalScope scope &&
            ReferenceEquals(Current, scope) &&
            scope.IsActive)
        {
            DismissPolicy?.Invoke(scope);
        }
    }

    private void OnExited(object? sender, EventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is not ModalScope scope || !ReferenceEquals(Current, scope))
        {
            return;
        }

        Clear(scope);
        ExitPolicy?.Invoke(scope);
    }

    private void ClearInactive()
    {
        if (Current is { IsActive: false } scope)
        {
            Clear(scope);
        }
    }

    private void Clear(ModalScope scope)
    {
        if (!ReferenceEquals(Current, scope))
        {
            return;
        }

        Current = null;
        scope.DismissRequested -= OnDismissRequested;
        scope.Exited -= OnExited;
    }
}
