// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using System.Runtime.ExceptionServices;

using MustDisposeResource = JetBrains.Annotations.MustDisposeResourceAttribute;

/// <summary>Owns the dispatcher-affine modal-scope stack for one attached application tree.</summary>
[PublicAPI]
public sealed class ModalityManager: IDisposable
{
    #region Construction and state

    // Retain committed exits in lifetime order until focus restoration and Exited publication finish.
    // Active interaction ignores those tombstones while unwind ordering still observes them.
    private readonly List<ModalScope> _stack = [];
    private readonly Queue<(ModalScope Scope, bool RestoreFocus, ControlBase? ExcludedSubtree)> _pendingUnwinds = new();
    private readonly List<ControlBase> _unwindExclusions = [];
    private readonly List<ControlBase> _unavailableSubtrees = [];
    private readonly List<LightDismiss> _lightDismissHandlers = [];
    private readonly FocusManager _focus;
    private readonly PointerManager _pointer;
    private readonly Action<ControlBase?>? _activate;
    private ExceptionDispatchInfo? _unwindFailure;
    private ModalScope? _unwindRequested;
    private ModalScope? _pendingExit;
    private ControlBase? _pendingExitFocus;
    private bool _releaseOwnershipWhenUnwound;
    private bool _isCommittingInactive;
    private bool _isDisposed;
    private bool _isPumpingUnwinds;
    private bool _unwindRestoresFocus = true;
    private bool _isUnwinding;

    /// <summary>Initializes modal ownership for the same tree owned by focus and pointer services.
    /// The caller owns and must dispose the manager.</summary>
    /// <param name="root">The non-null attached application root.</param>
    /// <param name="focus">The non-null live focus manager for <paramref name="root"/>.</param>
    /// <param name="pointer">The non-null live pointer manager for <paramref name="root"/>.</param>
    /// <param name="activate">Optional integration callback that activates the Window containing
    /// a modal root when entry has no eligible focus target.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">A dependency does not own the same attached tree.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The root is disposed.</exception>
    [MustDisposeResource]
    internal ModalityManager(
        ControlBase root,
        FocusManager focus,
        PointerManager pointer,
        Action<ControlBase?>? activate = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(focus);
        ArgumentNullException.ThrowIfNull(pointer);
        root.VerifyMutable();

        if (root.Dispatcher is null)
        {
            throw new ArgumentException("The modality root must be attached.", nameof(root));
        }

        if (!ReferenceEquals(focus.Root, root) || !ReferenceEquals(root.FocusOwner, focus))
        {
            throw new ArgumentException("The focus manager must own the modality root.", nameof(focus));
        }

        if (!ReferenceEquals(pointer.Root, root) || !ReferenceEquals(root.CaptureOwner, pointer))
        {
            throw new ArgumentException("The pointer manager must own the modality root.", nameof(pointer));
        }

        if (root.ModalityOwner is not null)
        {
            throw new ArgumentException("The root already belongs to a modality manager.", nameof(root));
        }

        Root = root;
        _focus = focus;
        _pointer = pointer;
        _activate = activate;
        root.SetModalityOwner(this);
    }

    /// <summary>Gets the youngest interaction-active modal scope, or null when interaction is unrestricted.</summary>
    public ModalScope? Active
    {
        get
        {
            for (var index = _stack.Count - 1; index >= 0; index--)
            {
                if (_stack[index].IsActive)
                {
                    return _stack[index];
                }
            }

            return null;
        }
    }

    /// <summary>Gets the attached application root owned by this manager.</summary>
    internal ControlBase Root { get; }

    /// <summary>Gets the active plane's root count, or zero when no scope is active.</summary>
    internal int ActiveRootCount => Active?.RootCount ?? 0;

    #endregion

    #region Scope lifecycle

    /// <summary>Enters one modal plane after validating all tree, availability, and overlap constraints.</summary>
    /// <param name="root">The non-null attached and available primary plane root.</param>
    /// <param name="outsideInteraction">The defined outside-interaction policy.</param>
    /// <param name="initialFocus">An optional eligible focus target inside the primary root.</param>
    /// <returns>The active disposable modal lifetime.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outsideInteraction"/> is undefined.</exception>
    /// <exception cref="ArgumentException">
    /// A root or initial-focus target is foreign, unavailable, ineligible, disposed, or an exact active duplicate.
    /// </exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The manager or supplied control is disposed.</exception>
    /// <exception cref="Exception">
    /// Pointer cleanup, focus publication, or transactional rollback notification fails after committed cleanup.
    /// </exception>
    [MustDisposeResource]
    public ModalScope Enter(
        ControlBase root,
        OutsideInteraction outsideInteraction = OutsideInteraction.Ignore,
        ControlBase? initialFocus = null)
    {
        ArgumentNullException.ThrowIfNull(root);

        ArgumentOutOfRangeException.ThrowIfNotDefined(outsideInteraction, nameof(outsideInteraction), "The outside-interaction policy is unknown.");

        VerifyAccess();
        ValidatePlaneRoot(root, nameof(root));
        ValidateEntryRoot(root, nameof(root));

        if (initialFocus is not null)
        {
            ValidateInitialFocus(initialFocus, root, nameof(initialFocus));
        }

        var previousFocus = _focus.Focused;
        var previousCapture = _pointer.Captured;
        var scope = new ModalScope(
            this,
            root,
            outsideInteraction,
            previousFocus,
            previousCapture);
        _stack.Add(scope);
        scope.IsActive = true;

        try
        {
            _pointer.ModalScopeChanged(scope);
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            var target = initialFocus ?? FindInitialFocus(root);
            CommitFocus(
                target,
                FocusReason.Programmatic,
                excludedSubtrees: null,
                scope,
                previousFocus);

            if (target is null)
            {
                _activate?.Invoke(root);
            }

            ObjectDisposedException.ThrowIf(_isDisposed, this);
            return scope;
        }
        catch (Exception exception)
        {
            if (scope.IsActive)
            {
                try
                {
                    RollbackEntry(
                        scope,
                        previousFocus,
                        ExceptionDispatchInfo.Capture(exception));
                }
                catch
                {
                    // Entry rollback is best effort; the initiating failure remains authoritative.
                }
            }

            ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }
    }

    /// <summary>Unwinds every scope and releases inherited modality ownership.</summary>
    /// <remarks>
    /// Every affected scope commits inactive before a reentrant disposal call returns. Focus restoration
    /// and <see cref="ModalScope.Exited"/> publication may wait for an enclosing focus transaction.
    /// Shutdown or unavailability may strengthen the remaining unwind by suppressing restoration.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="Exception">A pointer-reconciliation, focus, or exit callback fails after complete unwind.</exception>
    public void Dispose()
    {
        DisposeCore(restoreFocus: true, verifyAccess: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Gets one active plane root by its valid zero-based insertion position.</summary>
    /// <param name="index">The valid zero-based position.</param>
    /// <returns>The active plane root.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The position is outside the active plane roots.</exception>
    [Pure]
    internal ControlBase ActiveRootAt(int index)
    {
        var active = Active ?? throw new ArgumentOutOfRangeException(
            nameof(index),
            index,
            "There is no active modal plane.");
        return active.RootAt(index);
    }

    /// <summary>Returns whether one target may interact under the current active plane.</summary>
    /// <param name="control">The target, or null.</param>
    /// <returns>True when unrestricted or contained by an active plane root.</returns>
    [Pure]
    internal bool Allows(ControlBase? control)
    {
        var active = Active;
        return active is null
            ? control is null || (IsMember(control) && !IsUnavailable(control))
            : control is not null && !IsUnavailable(control) && active.BoundaryFor(control) is not null;
    }

    /// <summary>Resolves a key or terminal-focus target within the current modal plane.</summary>
    /// <param name="focused">The currently focused control, or null.</param>
    /// <returns>
    /// The eligible focused control, the active primary plane root, or the application root
    /// when no scope and no focus are active.
    /// </returns>
    [Pure]
    internal ControlBase KeyTarget(ControlBase? focused)
    {
        var active = Active;
        return active is null
            ? focused ?? Root
            : focused is not null && active.BoundaryFor(focused) is not null
                ? focused
                : active.Root;
    }

    /// <summary>Resolves a text or paste target without falling back through an active plane.</summary>
    /// <param name="focused">The currently focused control, or null.</param>
    /// <returns>The eligible focused control, or null when an active plane excludes it.</returns>
    [Pure]
    internal ControlBase? FocusedTarget(ControlBase? focused)
    {
        var active = Active;
        return active is null
            ? focused
            : focused is not null && active.BoundaryFor(focused) is not null
                ? focused
                : null;
    }

    /// <summary>Returns the active plane root containing one target.</summary>
    /// <param name="control">The non-null target.</param>
    /// <returns>The matching route boundary, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    [Pure]
    internal ControlBase? BoundaryFor(ControlBase control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return Active?.BoundaryFor(control);
    }

    /// <summary>Adds one disjoint root to an active scope after complete validation.</summary>
    /// <param name="scope">The non-null active owned scope.</param>
    /// <param name="root">The non-null proposed plane root.</param>
    internal void Include(ModalScope scope, ControlBase root)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(root);
        VerifyAccess();

        if (!scope.IsActive || !_stack.Contains(scope))
        {
            throw new InvalidOperationException("Roots cannot be included in an inactive modal scope.");
        }

        ValidatePlaneRoot(root, nameof(root));
        ValidateDisjoint(root, nameof(root));
        scope.AddRoot(root);
        _pointer.ReconcileHover();
    }

    /// <summary>Ends one active scope and every younger scope in reverse order.</summary>
    /// <param name="scope">The scope requested by its disposable handle.</param>
    internal void Exit(ModalScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        if (!scope.IsActive)
        {
            return;
        }

        VerifyAccess();
        var index = _stack.IndexOf(scope);
        Debug.Assert(index >= 0, "Every active scope remains in its owning manager stack.");
        UnwindFrom(index, restoreFocus: true, excludedSubtree: null);
    }

    /// <summary>Removes unavailable included roots or unwinds from the oldest affected primary root.</summary>
    /// <param name="subtree">The non-null unavailable subtree in the owned tree.</param>
    internal void Unavailable(ControlBase subtree)
        => Unavailable(subtree, restoreFocus: true);

    /// <summary>Removes or unwinds roots within one unavailable subtree using the requested focus policy.</summary>
    /// <param name="subtree">The non-null unavailable subtree in the owned tree.</param>
    /// <param name="restoreFocus">Whether each exited scope may restore focus.</param>
    internal void Unavailable(ControlBase subtree, bool restoreFocus)
    {
        ArgumentNullException.ThrowIfNull(subtree);

        if (_isDisposed && !_isUnwinding)
        {
            return;
        }

        if (_isUnwinding)
        {
            StrengthenUnwindPolicy(restoreFocus, subtree);
        }

        Debug.Assert(IsMember(subtree), "Unavailable subtrees belong to the modality root.");
        var primaryIndex = -1;

        for (var index = 0; index < _stack.Count; index++)
        {
            if (_stack[index].IsActive && IsWithin(_stack[index].Root, subtree))
            {
                primaryIndex = index;
                break;
            }
        }

        var survivingCount = primaryIndex < 0 ? _stack.Count : primaryIndex;
        for (var index = 0; index < survivingCount; index++)
        {
            if (_stack[index].IsActive)
            {
                _stack[index].RemoveSecondaryRootsWithin(subtree);
            }
        }

        if (primaryIndex >= 0)
        {
            UnwindFrom(primaryIndex, restoreFocus, subtree);
        }
    }

    /// <summary>Guards one retained subtree for the complete unavailable callback transaction.</summary>
    /// <param name="subtree">The non-null subtree whose current ownership must not be reused.</param>
    /// <exception cref="ArgumentNullException"><paramref name="subtree"/> is null.</exception>
    internal void BeginUnavailable(ControlBase subtree)
    {
        ArgumentNullException.ThrowIfNull(subtree);
        Debug.Assert(IsMember(subtree), "Unavailable guards begin while old ownership remains coherent.");
        _unavailableSubtrees.Add(subtree);
    }

    /// <summary>Ends the most recent matching unavailable-subtree guard.</summary>
    /// <param name="subtree">The non-null subtree whose callback transaction completed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="subtree"/> is null.</exception>
    internal void EndUnavailable(ControlBase subtree)
    {
        ArgumentNullException.ThrowIfNull(subtree);
        var index = _unavailableSubtrees.FindLastIndex(item => ReferenceEquals(item, subtree));
        Debug.Assert(index >= 0, "Every unavailable guard ends exactly once.");

        if (index >= 0)
        {
            _unavailableSubtrees.RemoveAt(index);
        }
    }

    /// <summary>Handles a disposed subtree and severs ownership when it is the manager root.</summary>
    /// <param name="root">The disposed subtree root.</param>
    internal void RootDisposed(ControlBase root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (ReferenceEquals(root, Root))
        {
            RootDisposed();
            return;
        }

        Unavailable(root);
    }

    /// <summary>Severs manager ownership while the application root is being disposed.</summary>
    internal void RootDisposed() => DisposeCore(restoreFocus: false, verifyAccess: false);

    /// <summary>Publishes one outside-dismissal request to the scope captured for an input record.</summary>
    /// <param name="scope">The non-null scope that governed targeting for the consumed record.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="Exception">A dismissal callback fails.</exception>
    internal void RequestDismiss(ModalScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        if (_isDisposed || !scope.IsActive)
        {
            return;
        }

        VerifyAccess();
        Debug.Assert(_stack.Contains(scope), "An active dismissal scope belongs to this manager.");
        scope.PublishDismissRequested();
    }

    /// <summary>Registers a younger non-modal surface that may consume presses outside the active plane.</summary>
    /// <param name="handler">The non-null light-dismiss handler.</param>
    internal void RegisterLightDismiss(LightDismiss handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        VerifyAccess();
        _lightDismissHandlers.Add(handler);
    }

    /// <summary>Removes a previously registered non-modal surface.</summary>
    /// <param name="handler">The non-null light-dismiss handler.</param>
    internal void UnregisterLightDismiss(LightDismiss handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (!_isDisposed)
        {
            VerifyAccess();
        }

        _ = _lightDismissHandlers.Remove(handler);
    }

    /// <summary>Lets the youngest eligible non-modal surface consume one outside-plane press.</summary>
    /// <param name="pointer">The decoded pointer input.</param>
    /// <returns>True when a light-dismiss surface consumed the press.</returns>
    internal bool TryLightDismiss(Terminal.Input.Pointer pointer)
    {
        VerifyAccess();
        var snapshot = _lightDismissHandlers.ToArray();

        for (var index = snapshot.Length - 1; index >= 0; index--)
        {
            if (snapshot[index].TryDismiss(pointer))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Unwinds all scopes for application shutdown without restoring focus.</summary>
    internal void Shutdown() => DisposeCore(restoreFocus: false, verifyAccess: true);

    #endregion

    #region Lifetime implementation and validation

    /// <summary>Tests whether one control belongs to a retained subtree through owned ancestry.</summary>
    /// <param name="control">The target, or null.</param>
    /// <param name="subtree">The non-null subtree root.</param>
    /// <returns>True when ancestry reaches the subtree root.</returns>
    [Pure]
    internal static bool IsWithin(ControlBase? control, ControlBase subtree)
    {
        Debug.Assert(subtree is not null, "Subtree membership requires a concrete root.");

        for (var current = control; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, subtree))
            {
                return true;
            }
        }

        return false;
    }

    private void DisposeCore(bool restoreFocus, bool verifyAccess)
    {
        if (_isDisposed)
        {
            if (_isUnwinding && !restoreFocus)
            {
                if (verifyAccess)
                {
                    Root.VerifyMutable();
                }

                _unwindRestoresFocus = false;
            }

            return;
        }

        if (verifyAccess)
        {
            Root.VerifyMutable();
        }

        _isDisposed = true;
        _lightDismissHandlers.Clear();
        _releaseOwnershipWhenUnwound = true;
        var activeIndex = FindFirstActiveIndex();

        if (_isUnwinding)
        {
            _unwindRestoresFocus &= restoreFocus;

            if (activeIndex >= 0)
            {
                UnwindFrom(activeIndex, restoreFocus, excludedSubtree: null);
            }

            return;
        }

        if (activeIndex >= 0)
        {
            UnwindFrom(activeIndex, restoreFocus, excludedSubtree: null);
            return;
        }

        Debug.Assert(_stack.Count == 0, "Retained exit tombstones exist only during an unwind transaction.");
        Root.SetModalityOwner(null);
    }

    private void UnwindFrom(
        int index,
        bool restoreFocus,
        ControlBase? excludedSubtree,
        ExceptionDispatchInfo? priorFailure = null)
    {
        Debug.Assert(index >= 0 && index < _stack.Count, "Modal unwind begins at one active scope.");
        Debug.Assert(_stack[index].IsActive, "Modal unwind begins at one interaction-active scope.");
        var requested = _stack[index];
        _pendingUnwinds.Enqueue((requested, restoreFocus, excludedSubtree));

        if (!_isUnwinding)
        {
            _isUnwinding = true;
            _unwindFailure = priorFailure;
            _unwindRestoresFocus = true;
            _unwindExclusions.Clear();
        }
        else
        {
            _unwindFailure ??= priorFailure;
        }

        StrengthenUnwindPolicy(restoreFocus, excludedSubtree);
        CommitInactiveFrom(index);
        PumpUnwinds();
    }

    private void PumpUnwinds()
    {
        if (!_isUnwinding || _isCommittingInactive || _isPumpingUnwinds || _pendingExit is not null)
        {
            return;
        }

        _isPumpingUnwinds = true;
        var completed = false;

        try
        {
            while (_pendingExit is null)
            {
                if (_unwindRequested is null || !_stack.Contains(_unwindRequested))
                {
                    _unwindRequested = null;

                    while (_pendingUnwinds.Count > 0)
                    {
                        var request = _pendingUnwinds.Dequeue();
                        StrengthenUnwindPolicy(
                            request.RestoreFocus,
                            request.ExcludedSubtree);

                        if (_stack.Contains(request.Scope))
                        {
                            _unwindRequested = request.Scope;
                            break;
                        }
                    }

                    if (_unwindRequested is null)
                    {
                        completed = true;
                        break;
                    }
                }

                // A callback may enter another child while an older requested
                // tombstone survives. Commit every such child before publication.
                var requestedIndex = _stack.IndexOf(_unwindRequested);
                Debug.Assert(requestedIndex >= 0, "The selected unwind request remains retained.");
                var activeIndex = FindFirstActiveIndex(requestedIndex);
                while (activeIndex >= 0)
                {
                    CommitInactiveFrom(activeIndex);
                    activeIndex = FindFirstActiveIndex(requestedIndex);
                }

                // Always publish the retained youngest scope first.
                var scope = _stack[^1];
                Debug.Assert(!scope.IsActive, "The youngest unwind lifetime committed inactive before publication.");

                if (!_unwindRestoresFocus)
                {
                    CaptureFailure(scope.PublishExited, ref _unwindFailure);
                    FinalizeExit(scope);
                    continue;
                }

                BeginRestore(scope);
            }
        }
        finally
        {
            _isPumpingUnwinds = false;
        }

        if (completed)
        {
            CompleteUnwindTransaction();
        }
    }

    private void CommitInactiveFrom(int index)
    {
        Debug.Assert(index >= 0 && index < _stack.Count, "A committed batch begins in the retained stack.");
        var ownsCommitGuard = !_isCommittingInactive;
        if (ownsCommitGuard)
        {
            _isCommittingInactive = true;
        }

        var changed = false;

        try
        {
            for (var current = index; current < _stack.Count; current++)
            {
                if (_stack[current].IsActive)
                {
                    _stack[current].IsActive = false;
                    changed = true;
                }
            }

            if (changed)
            {
                ReconcilePointer(ref _unwindFailure);
            }
        }
        finally
        {
            if (ownsCommitGuard)
            {
                _isCommittingInactive = false;
            }
        }
    }

    private int FindFirstActiveIndex(int startIndex = 0)
    {
        for (var index = startIndex; index < _stack.Count; index++)
        {
            if (_stack[index].IsActive)
            {
                return index;
            }
        }

        return -1;
    }

    private void BeginRestore(ModalScope scope)
    {
        Debug.Assert(_pendingExit is null, "Only one retained scope awaits focus completion.");
        _pendingExit = scope;
        _pendingExitFocus = _focus.Focused;

        try
        {
            RestoreFocus(
                scope.PreviousFocus,
                failure => CompletePendingExit(scope, failure));
        }
        catch (Exception exception)
        {
            _unwindFailure ??= ExceptionDispatchInfo.Capture(exception);

            if (ReferenceEquals(_pendingExit, scope))
            {
                CompletePendingExit(scope, exception);
            }
        }
    }

    private void CompletePendingExit(ModalScope scope, Exception? focusFailure)
    {
        if (!ReferenceEquals(_pendingExit, scope))
        {
            return;
        }

        if (focusFailure is not null)
        {
            _unwindFailure ??= ExceptionDispatchInfo.Capture(focusFailure);
        }

        if (_unwindRestoresFocus && !_focus.IsDisposed)
        {
            CaptureFailure(
                () => EnsureRestoredFocus(scope.PreviousFocus, _pendingExitFocus),
                ref _unwindFailure);
        }

        // Restore callbacks may append active or already-exiting younger work.
        // Resume the pump and retry this scope's restoration only after every
        // retained lifetime above it has published.
        if (!ReferenceEquals(_stack[^1], scope))
        {
            _pendingExit = null;
            _pendingExitFocus = null;

            if (!_isPumpingUnwinds)
            {
                PumpUnwinds();
            }

            return;
        }

        CaptureFailure(scope.PublishExited, ref _unwindFailure);
        FinalizeExit(scope);
        _pendingExit = null;
        _pendingExitFocus = null;

        if (!_isPumpingUnwinds)
        {
            PumpUnwinds();
        }
    }

    private void FinalizeExit(ModalScope scope)
    {
        var index = _stack.IndexOf(scope);
        Debug.Assert(index >= 0, "A published exit remains retained until finalization.");

        if (index >= 0)
        {
            _stack.RemoveAt(index);
        }

        if (ReferenceEquals(_unwindRequested, scope))
        {
            _unwindRequested = null;
        }
    }

    private void CompleteUnwindTransaction()
    {
        Debug.Assert(_pendingExit is null, "Unwind completion follows every scope publication.");

        if (_releaseOwnershipWhenUnwound)
        {
            CaptureFailure(() => Root.SetModalityOwner(null), ref _unwindFailure);
        }

        var failure = _unwindFailure;
        _pendingUnwinds.Clear();
        _unwindExclusions.Clear();
        _unwindRequested = null;
        _pendingExitFocus = null;
        _unwindFailure = null;
        _releaseOwnershipWhenUnwound = false;
        _unwindRestoresFocus = true;
        _isUnwinding = false;
        failure?.Throw();
    }

    private void StrengthenUnwindPolicy(
        bool restoreFocus,
        ControlBase? excludedSubtree)
    {
        _unwindRestoresFocus &= restoreFocus;

        if (excludedSubtree is not null &&
            !_unwindExclusions.Exists(item => ReferenceEquals(item, excludedSubtree)))
        {
            _unwindExclusions.Add(excludedSubtree);
        }
    }

    private void RestoreFocus(ControlBase? previous, Action<Exception?> completion)
    {
        Debug.Assert(completion is not null, "Restore completion resumes one pending modal exit.");
        var attempted = new HashSet<ControlBase?>();
        RestoreFocusCore(
            ResolveRestoreTarget(previous, attempted),
            attempted,
            completion);
    }

    private void RestoreFocusCore(
        ControlBase? preferred,
        HashSet<ControlBase?> attempted,
        Action<Exception?> completion)
    {
        if (_focus.IsDisposed)
        {
            completion(null);
            return;
        }

        if (!attempted.Add(preferred))
        {
            completion(null);
            return;
        }

        _focus.Focus(
            preferred,
            FocusReason.Restore,
            cancellable: false,
            (committed, failure) =>
            {
                if (failure is not null || !_unwindRestoresFocus)
                {
                    completion(failure);
                    return;
                }

                if (_focus.IsDisposed)
                {
                    completion(null);
                    return;
                }

                if (committed && IsRestoreTargetValid(preferred))
                {
                    completion(null);
                    return;
                }

                var fallback = ResolveRestoreTarget(previous: null, attempted);
                if (fallback is null && attempted.Contains(null))
                {
                    completion(null);
                    return;
                }

                RestoreFocusCore(fallback, attempted, completion);
            },
            () => _unwindRestoresFocus && IsRestoreTargetValid(preferred),
            _unwindFailure);
    }

    private ControlBase? ResolveRestoreTarget(
        ControlBase? previous,
        HashSet<ControlBase?>? attempted = null)
        => previous is not null &&
            attempted?.Contains(previous) is not true &&
            IsRestoreTargetValid(previous)
                ? previous
                : Active is { } active
                    ? FindInitialFocus(active, _unwindExclusions, attempted)
                    : null;

    private bool IsRestoreTargetValid(ControlBase? target) =>
        target is null ||
        (IsEligibleFocusTarget(target, _unwindExclusions) && Allows(target));

    private void EnsureRestoredFocus(ControlBase? previous, ControlBase? displaced)
    {
        if (_focus.IsDisposed)
        {
            return;
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var target = ResolveRestoreTarget(previous);
            if (IsFocusCoherent(target, displaced))
            {
                return;
            }

            CaptureFailure(
                () => _focus.RestoreCoherently(target, displaced),
                ref _unwindFailure);
        }

        CaptureFailure(
            () => _focus.ForceFocusCoherence(ResolveRestoreTarget(previous), displaced),
            ref _unwindFailure);
    }

    private bool IsFocusCoherent(ControlBase? target, ControlBase? displaced) =>
        ReferenceEquals(_focus.Focused, target) &&
        (target is null || target.IsFocused) &&
        (displaced is null || ReferenceEquals(displaced, target) || !displaced.IsFocused);

    private void RollbackEntry(
        ModalScope scope,
        ControlBase? previousFocus,
        ExceptionDispatchInfo? priorFailure = null)
    {
        var index = _stack.IndexOf(scope);
        if (index < 0)
        {
            return;
        }

        Debug.Assert(
            ReferenceEquals(scope.PreviousFocus, previousFocus),
            "A failed entry retains the focus snapshot used by ordinary unwind.");
        UnwindFrom(
            index,
            restoreFocus: true,
            excludedSubtree: null,
            priorFailure: priorFailure);
    }

    private void ReconcilePointer(ref ExceptionDispatchInfo? failure)
    {
        // Inactive-state commit chooses which plane governs interaction before
        // any callback observes it. A live tree with no surviving scope becomes
        // unrestricted without replaying the last physical pointer record.
        if (Active is { } active)
        {
            CaptureFailure(
                () => _pointer.ModalScopeChanged(active),
                ref failure);
        }
        else if (_unwindRestoresFocus)
        {
            CaptureFailure(
                _pointer.ReconcileUnrestrictedModality,
                ref failure);
        }
    }

    private void CommitFocus(
        ControlBase? preferred,
        FocusReason reason,
        IReadOnlyList<ControlBase>? excludedSubtrees,
        ModalScope? entryScope = null,
        ControlBase? entryPreviousFocus = null)
    {
        var retainedExclusions = excludedSubtrees?.ToArray();
        var attempted = new HashSet<ControlBase?>();
        CommitFocusCore(
            preferred,
            reason,
            retainedExclusions,
            attempted,
            entryScope,
            entryPreviousFocus);
    }

    private void CommitFocusCore(
        ControlBase? preferred,
        FocusReason reason,
        IReadOnlyList<ControlBase>? excludedSubtrees,
        HashSet<ControlBase?> attempted,
        ModalScope? entryScope,
        ControlBase? entryPreviousFocus)
    {
        if (!attempted.Add(preferred))
        {
            return;
        }

        Func<bool>? isValid = entryScope is null
            ? null
            : () => entryScope.IsActive;
        _focus.Focus(
            preferred,
            reason,
            cancellable: false,
            (committed, failure) =>
            {
                if (entryScope is not null && !entryScope.IsActive)
                {
                    return;
                }

                if (failure is not null)
                {
                    if (entryScope is not null)
                    {
                        RollbackEntry(
                            entryScope,
                            entryPreviousFocus,
                            ExceptionDispatchInfo.Capture(failure));
                    }

                    return;
                }

                if (!committed)
                {
                    if (_focus.IsDisposed)
                    {
                        if (entryScope is not null && entryScope.IsActive)
                        {
                            RollbackEntry(
                                entryScope,
                                entryPreviousFocus);
                        }

                        return;
                    }

                    CommitFallback(
                        reason,
                        excludedSubtrees,
                        attempted,
                        entryScope,
                        entryPreviousFocus);
                }
            },
            isValid);
    }

    private void CommitFallback(
        FocusReason reason,
        IReadOnlyList<ControlBase>? excludedSubtrees,
        HashSet<ControlBase?> attempted,
        ModalScope? entryScope,
        ControlBase? entryPreviousFocus)
    {
        var fallback = Active is { } active
            ? FindInitialFocus(active, excludedSubtrees, attempted)
            : null;

        if (fallback is null && attempted.Contains(null))
        {
            return;
        }

        CommitFocusCore(
            fallback,
            reason,
            excludedSubtrees,
            attempted,
            entryScope,
            entryPreviousFocus);
    }

    private ControlBase? FindInitialFocus(
        ModalScope scope,
        IReadOnlyList<ControlBase>? excludedSubtrees = null,
        HashSet<ControlBase?>? attempted = null)
    {
        for (var index = 0; index < scope.RootCount; index++)
        {
            if (FindInitialFocus(scope.RootAt(index), excludedSubtrees, attempted) is { } target)
            {
                return target;
            }
        }

        return null;
    }

    private ControlBase? FindInitialFocus(
        ControlBase root,
        IReadOnlyList<ControlBase>? excludedSubtrees = null,
        HashSet<ControlBase?>? attempted = null) =>
        InitialFocusResolver.FindFirstEligibleFocusTarget(
            root,
            includeRoot: true,
            this,
            excludedSubtrees,
            attempted);

    private bool IsEligibleFocusTarget(
        ControlBase control,
        IReadOnlyList<ControlBase>? excludedSubtrees = null) =>
        InitialFocusResolver.IsEligibleFocusTarget(
            control,
            this,
            excludedSubtrees,
            respectActivePlane: false);

    private void ValidateInitialFocus(ControlBase control, ControlBase planeRoot, string parameterName)
    {
        ObjectDisposedException.ThrowIf(control.IsDisposed, control);

        if (!IsWithin(control, planeRoot) || !IsEligibleFocusTarget(control))
        {
            throw new ArgumentException(
                "Initial focus must be an eligible member of the proposed modal plane.",
                parameterName);
        }
    }

    private void ValidatePlaneRoot(ControlBase control, string parameterName)
    {
        ObjectDisposedException.ThrowIf(control.IsDisposed, control);

        if (!IsMember(control) || !ReferenceEquals(control.ModalityOwner, this))
        {
            throw new ArgumentException(
                "The modal root must belong to this application tree.",
                parameterName);
        }

        if (control.Dispatcher is null || !control.EffectiveIsVisible ||
            !control.EffectiveIsEnabled || IsUnavailable(control))
        {
            throw new ArgumentException(
                "The modal root must be attached, visible, and enabled.",
                parameterName);
        }
    }

    private void ValidateDisjoint(ControlBase candidate, string parameterName)
    {
        foreach (var scope in _stack)
        {
            if (!scope.IsActive)
            {
                continue;
            }

            for (var index = 0; index < scope.RootCount; index++)
            {
                var existing = scope.RootAt(index);
                if (IsWithin(candidate, existing) || IsWithin(existing, candidate))
                {
                    throw new ArgumentException(
                        "Modal plane roots must be pairwise disjoint.",
                        parameterName);
                }
            }
        }
    }

    private void ValidateEntryRoot(ControlBase candidate, string parameterName)
    {
        foreach (var scope in _stack)
        {
            if (!scope.IsActive)
            {
                continue;
            }

            for (var index = 0; index < scope.RootCount; index++)
            {
                if (ReferenceEquals(candidate, scope.RootAt(index)))
                {
                    throw new ArgumentException(
                        "The control already roots an active modal plane.",
                        parameterName);
                }
            }
        }
    }

    private bool IsMember(ControlBase control) => IsWithin(control, Root);

    /// <summary>Gets whether a control is inside a subtree currently publishing framework unavailability.</summary>
    /// <param name="control">The non-null control to inspect.</param>
    /// <returns>True while an enclosing unavailable transaction is active; otherwise false.</returns>
    [Pure]
    internal bool IsUnavailable(ControlBase control)
    {
        foreach (var subtree in _unavailableSubtrees)
        {
            if (IsWithin(control, subtree))
            {
                return true;
            }
        }

        return false;
    }

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        Root.VerifyMutable();
        Debug.Assert(ReferenceEquals(Root.ModalityOwner, this), "A live manager remains inherited from its root.");
    }

    private static void CaptureFailure(Action action, ref ExceptionDispatchInfo? failure)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }
    }

    #endregion
}
