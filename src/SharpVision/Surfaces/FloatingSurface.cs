// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Surfaces;

using System.Runtime.ExceptionServices;

/// <summary>
/// Provides retained content, committed bounds, lifecycle, and modality support for an elevated surface.
/// </summary>
/// <remarks>
/// Concrete surface families retain ownership of their public open-state contract. This base coordinates
/// the common presentation transaction without exposing multi-child container semantics.
/// </remarks>
[PublicAPI]
public abstract class FloatingSurface: ContentControl
{
    private ModalScope? _modalScope;
    private bool _isClosing;
    private bool _isEnteringModal;
    private bool _isOpening;
    private bool _openingInvalidated;
    private long _presentationVersion;

    #region Surface lifecycle

    /// <summary>Raised only after the surface becomes presented and its bounds commit.</summary>
    public event EventHandler? Opened;

    /// <summary>
    /// Raised before anything commits, letting a handler veto the request by setting
    /// <see cref="SurfaceCloseRequestedEventArgs.Cancel"/>.
    /// </summary>
    /// <remarks>
    /// Nothing has changed yet when this runs: the surface is still fully presented, its modal
    /// scope (if any) is still active, and setting <c>Cancel</c> leaves every bit of that state
    /// untouched - no <see cref="Closing"/> or <see cref="Closed"/> notification follows a
    /// cancelled request.
    /// </remarks>
    public event EventHandler<SurfaceCloseRequestedEventArgs>? CloseRequested;

    /// <summary>Raised when closure is requested or after family-specific closing state commits.</summary>
    /// <remarks>A request handler may retain the surface by leaving its presentation available.</remarks>
    public event EventHandler? Closing;

    /// <summary>Raised only after the presented surface becomes unavailable and its bounds clear.</summary>
    public event EventHandler? Closed;

    /// <summary>Gets the committed visible surface rectangle, or an empty rectangle when unavailable.</summary>
    public Rect SurfaceBounds { get; protected set; }

    /// <summary>Gets whether the common lifecycle currently represents a presented surface.</summary>
    protected bool IsSurfacePresented { get; private set; }

    /// <summary>Gets whether this surface currently owns one active application modality scope.</summary>
    protected bool HasActiveSurfaceModal => _modalScope is { IsActive: true };

    /// <summary>Raises the inherited opened notification after the surface becomes presented.</summary>
    protected void RaiseSurfaceOpened() => Opened?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises the inherited closing notification for a family-specific close request.</summary>
    protected void RaiseSurfaceClosing() => Closing?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises the inherited closed notification after a family confirms presentation unavailability.</summary>
    protected void RaiseSurfaceClosed() => Closed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Captures the current <see cref="Closed"/> invocation list so it can still be raised after a
    /// synchronous <see cref="Closing"/> handler disposes the surface, which otherwise nulls the field
    /// before the caller gets a chance to raise it.
    /// </summary>
    protected EventHandler? CaptureClosedHandlers() => Closed;

    /// <summary>Atomically commits family-specific open state and marks the surface as presented.</summary>
    /// <param name="commitOpenState">The non-null family-specific state commit.</param>
    /// <remarks>
    /// The callback is the family's atomic commit boundary. Before propagating a callback failure,
    /// it must restore any family-specific state it changed. The base always clears provisional
    /// common bounds and remains unpresented after a failure. Opening and closing cannot reenter
    /// while the callback runs.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="commitOpenState"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The attached surface is mutated off-dispatcher, is already presented, is closing, opening
    /// is reentered, or the surface becomes unavailable during the family commit.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The surface is disposed.</exception>
    protected void OpenSurface(Action commitOpenState)
    {
        ArgumentNullException.ThrowIfNull(commitOpenState);
        VerifyMutable();

        if (Dispatcher is null)
        {
            throw new InvalidOperationException("A floating surface must be attached before it can open.");
        }

        if (_isOpening)
        {
            throw new InvalidOperationException("Floating surface opening cannot be reentered.");
        }

        if (_isClosing)
        {
            throw new InvalidOperationException("A floating surface cannot open while it is closing.");
        }

        if (IsSurfacePresented)
        {
            throw new InvalidOperationException("The floating surface is already open.");
        }

        _isOpening = true;
        _openingInvalidated = false;

        try
        {
            commitOpenState();

            if (_openingInvalidated || Dispatcher is null)
            {
                throw new InvalidOperationException(
                    "A floating surface cannot finish opening after it becomes unavailable.");
            }

            IsSurfacePresented = true;
            IncrementPresentationVersion();
        }
        catch
        {
            SurfaceBounds = default;
            IsSurfacePresented = false;
            throw;
        }
        finally
        {
            _openingInvalidated = false;
            _isOpening = false;
        }

        RaiseSurfaceOpened();
    }

    /// <summary>Closes one presented surface through the common ordered cleanup transaction.</summary>
    /// <param name="commitClosingState">Commits family state that makes the surface ineligible.</param>
    /// <param name="commitUnavailableState">Makes the family-specific content unavailable.</param>
    /// <returns><see langword="true"/> when a presented surface was closed; otherwise false.</returns>
    /// <remarks>
    /// Cleanup continues after callback failures. After all stages complete, the earliest failure is rethrown.
    /// Repeated closure after committed cleanup is harmless.
    /// </remarks>
    /// <exception cref="ArgumentNullException">A callback is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The attached surface is mutated off-dispatcher, is opening, or closure is reentered.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The surface is disposed.</exception>
    /// <exception cref="Exception">A state callback, lifecycle subscriber, or modal cleanup callback fails.</exception>
    protected bool CloseSurface(
        Action commitClosingState,
        Action commitUnavailableState) =>
        CloseSurfaceCore(commitClosingState, commitUnavailableState, publishClosing: true);

    /// <summary>Completes closure after the concrete surface has already published its closing request.</summary>
    /// <param name="commitClosingState">Commits family state that makes the surface ineligible.</param>
    /// <param name="commitUnavailableState">Makes the family-specific content unavailable.</param>
    /// <returns><see langword="true"/> when a presented surface was closed; otherwise false.</returns>
    /// <remarks>
    /// This seam avoids a duplicate <see cref="Closing"/> notification while preserving modal exit,
    /// unavailable-state commit, bounds clearing, and one final <see cref="Closed"/> notification.
    /// Cleanup continues after callback failures and rethrows the earliest failure.
    /// </remarks>
    /// <exception cref="ArgumentNullException">A callback is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The attached surface is mutated off-dispatcher, is opening, or closure is reentered.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The surface is disposed.</exception>
    /// <exception cref="Exception">A state callback, lifecycle subscriber, or modal cleanup callback fails.</exception>
    private protected bool CloseSurfaceAfterClosingRequest(
        Action commitClosingState,
        Action commitUnavailableState) =>
        CloseSurfaceCore(commitClosingState, commitUnavailableState, publishClosing: false);

    private bool CloseSurfaceCore(
        Action commitClosingState,
        Action commitUnavailableState,
        bool publishClosing)
    {
        ArgumentNullException.ThrowIfNull(commitClosingState);
        ArgumentNullException.ThrowIfNull(commitUnavailableState);
        VerifyMutable();

        if (_isOpening)
        {
            throw new InvalidOperationException("A floating surface cannot close while it is opening.");
        }

        if (_isClosing)
        {
            throw new InvalidOperationException("Floating surface closure cannot be reentered.");
        }

        if (!IsSurfacePresented)
        {
            return false;
        }

        if (CloseRequested is { } closeRequested)
        {
            var requestArgs = new SurfaceCloseRequestedEventArgs();
            closeRequested.Invoke(this, requestArgs);

            if (requestArgs.Cancel)
            {
                return false;
            }
        }

        _isClosing = true;
        ExceptionDispatchInfo? failure = null;

        try
        {
            ExceptionAggregation.Capture(commitClosingState, ref failure);

            if (publishClosing)
            {
                ExceptionAggregation.Capture(RaiseSurfaceClosing, ref failure);
            }

            ExceptionAggregation.Capture(ExitSurfaceModal, ref failure);
            ExceptionAggregation.Capture(commitUnavailableState, ref failure);
            SurfaceBounds = default;
            IsSurfacePresented = false;
            IncrementPresentationVersion();
            ExceptionAggregation.Capture(RaiseSurfaceClosed, ref failure);
        }
        finally
        {
            _isClosing = false;
        }

        failure?.Throw();
        return true;
    }

    #endregion

    #region Modality

    /// <summary>Enters one application-owned modal presentation rooted at this surface.</summary>
    /// <param name="outsideInteraction">The policy for input outside the surface plane.</param>
    /// <param name="initialFocus">An optional eligible focus target owned by this surface.</param>
    /// <returns>The disposable lifetime representing this surface's modal presentation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="outsideInteraction"/> is undefined.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// This surface is not an eligible modal root, or <paramref name="initialFocus"/> is invalid.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The surface is detached, not presented, mutated off-dispatcher, closing, reentering modal
    /// entry, or already modal.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The surface, modality manager, or supplied focus target is disposed.
    /// </exception>
    protected ModalScope EnterSurfaceModal(
        OutsideInteraction outsideInteraction,
        Control? initialFocus)
    {
        VerifyMutable();

        if (!Enum.IsDefined(outsideInteraction))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outsideInteraction),
                outsideInteraction,
                "The outside-interaction policy is unknown.");
        }

        if (_isClosing)
        {
            throw new InvalidOperationException("A floating surface cannot enter modality while it is closing.");
        }

        if (!IsSurfacePresented)
        {
            throw new InvalidOperationException("A floating surface must be presented before it can enter modality.");
        }

        if (_isEnteringModal)
        {
            throw new InvalidOperationException("Floating surface modal entry cannot be reentered.");
        }

        if (_modalScope is { IsActive: true })
        {
            throw new InvalidOperationException("The floating surface is already modal.");
        }

        ClearInactiveModalScope();
        var modality = ModalityOwner ?? throw new InvalidOperationException(
            "A modal floating surface must belong to an attached application tree.");
        var presentationVersion = _presentationVersion;
        _isEnteringModal = true;

        try
        {
            var scope = modality.Enter(this, outsideInteraction, initialFocus);

            if (!scope.IsActive)
            {
                return scope;
            }

            if (!IsSurfacePresented ||
                presentationVersion != _presentationVersion ||
                Dispatcher is null ||
                !ReferenceEquals(ModalityOwner, modality))
            {
                scope.Dispose();
                return scope;
            }

            _modalScope = scope;
            scope.Exited += OnModalExited;
            return scope;
        }
        finally
        {
            _isEnteringModal = false;
        }
    }

    /// <summary>Ends this surface's active modal presentation, if any.</summary>
    /// <remarks>The tracked identity clears before callbacks run so replacement lifetimes remain distinct.</remarks>
    /// <exception cref="Exception">Modal focus restoration or an exit callback fails after cleanup.</exception>
    protected void ExitSurfaceModal()
    {
        if (_modalScope is not { } scope)
        {
            return;
        }

        ClearModalScope(scope);

        if (scope.IsActive)
        {
            scope.Dispose();
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(ExitSurfaceModal, ref failure);
        ExceptionAggregation.Capture(() => base.OnUnavailable(reason), ref failure);

        if (RemovesPresentation(reason))
        {
            ReleasePresentation();
        }

        if (reason == ReleaseReason.Disposed)
        {
            CloseRequested = null;
            Closing = null;
            Closed = null;
        }

        failure?.Throw();
    }

    private void OnModalExited(object? sender, EventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is ModalScope scope)
        {
            ClearModalScope(scope);
        }
    }

    private void ClearInactiveModalScope()
    {
        if (_modalScope is { IsActive: false } scope)
        {
            ClearModalScope(scope);
        }
    }

    private void ClearModalScope(ModalScope scope)
    {
        Debug.Assert(scope is not null, "Modal tracking cleanup requires one concrete lifetime.");

        if (!ReferenceEquals(_modalScope, scope))
        {
            return;
        }

        _modalScope = null;
        scope.Exited -= OnModalExited;
    }

    /// <summary>
    /// Clears this surface's presented bounds and flag outside the normal close path, so a caller that
    /// bypasses <see cref="OnUnavailable"/>'s own <see cref="ReleaseReason.Detached"/> handling — such as
    /// a descendant of a removed subtree root, which never receives its own <c>OnUnavailable</c> call —
    /// can still leave <see cref="OpenSurface"/> reopenable afterward.
    /// </summary>
    private protected void ReleasePresentation()
    {
        SurfaceBounds = default;
        IsSurfacePresented = false;
        _openingInvalidated = _isOpening;
        IncrementPresentationVersion();
    }

    private void IncrementPresentationVersion() => _presentationVersion = unchecked(_presentationVersion + 1);

    private static bool RemovesPresentation(ReleaseReason reason) => reason switch
    {
        ReleaseReason.Detached => true,
        ReleaseReason.Hidden => true,
        ReleaseReason.Disposed => true,
        ReleaseReason.Disabled => false,
        ReleaseReason.TerminalFocusLost => false,
        ReleaseReason.Transferred => false,
        ReleaseReason.ModalScopeChanged => false,
        _ => throw new InvalidOperationException("The release reason is unknown.")
    };

    #endregion
}
