// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using System.Runtime.ExceptionServices;

/// <summary>Owns one current modal-scope identity and its policy callbacks.</summary>
/// <remarks>
/// The session clears identity before external callbacks, so a callback may install a replacement
/// without stale cleanup erasing it. Callers retain presentation policy and supply only currentness,
/// dismissal, external-exit, and failed-entry rollback behavior.
/// </remarks>
internal sealed class ModalSession
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
    /// <exception cref="InvalidOperationException">Entry is reentered or a current scope is active.</exception>
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

            if (!scope.IsActive || !isCurrent())
            {
                if (scope.IsActive)
                {
                    scope.Dispose();
                }

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

            failure.Throw();
            throw;
        }
        finally
        {
            IsEntering = false;
        }
    }

    /// <summary>Clears and ends the exact current scope, if any.</summary>
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
