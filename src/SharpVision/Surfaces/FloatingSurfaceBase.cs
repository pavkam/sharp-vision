// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Surfaces;

using System.Runtime.ExceptionServices;

using InstantHandle = JetBrains.Annotations.InstantHandleAttribute;
using MustDisposeResource = JetBrains.Annotations.MustDisposeResourceAttribute;

/// <summary>
/// Provides retained content, committed bounds, lifecycle, and modality support for an elevated surface.
/// </summary>
/// <remarks>
/// Concrete surface families retain ownership of their public open-state contract. This base coordinates
/// the common presentation transaction without exposing multi-child container semantics.
/// </remarks>
[PublicAPI]
public abstract class FloatingSurfaceBase: ContentControl
{
    private static readonly TimeSpan _fadeRefreshInterval = TimeSpan.FromMilliseconds(16);

    private readonly ModalSession _modalSession;
    private Action? _deferredCloseCompletion;
    private Action? _deferredUnavailableCommit;
    private EventHandler? _deferredClosedHandlers;
    private DispatcherTimer? _fadeTimer;
    private FloatingSurfaceTransition? _fadeTransition;
    private long _fadePresentationVersion;
    private bool _deferredWasPresented;
    private bool _isClosing;
    private bool _isCompletingClose;
    private bool _isEnteringFade;
    private bool _isRequestingClose;
    private bool _isEnteringModal;
    private bool _isOpening;
    private bool _allowsOpeningDuringClosing;
    private bool _openingInvalidated;
    private bool _presentationReleasedForPendingDetach;

    /// <summary>Initializes one surface with shared modal-session policy routing.</summary>
    protected FloatingSurfaceBase() =>
        _modalSession = new ModalSession(
            OnSurfaceModalDismissRequested,
            OnSurfaceModalExited);

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

    /// <summary>Gets or sets the duration of the shared terminal-cell entrance fade.</summary>
    /// <remarks>
    /// The default zero completes synchronously. Positive transitions use the owning dispatcher's
    /// monotonic clock and dissolve the complete rendered surface over the current-frame underlay.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or exceeds timer limits.</exception>
    /// <exception cref="InvalidOperationException">The attached surface is presented, exiting, or mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The surface is disposed.</exception>
    public TimeSpan FadeInDuration
    {
        get;
        set => SetFadeDuration(ref field, value, nameof(FadeInDuration));
    }

    /// <summary>Gets or sets the duration of the shared terminal-cell dismissal fade.</summary>
    /// <remarks>
    /// The default zero preserves synchronous closure. A positive value keeps logical state,
    /// bounds, focus, and modality until visual progress reaches zero while consuming input.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or exceeds timer limits.</exception>
    /// <exception cref="InvalidOperationException">The attached surface is presented, exiting, or mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The surface is disposed.</exception>
    public TimeSpan FadeOutDuration
    {
        get;
        set => SetFadeDuration(ref field, value, nameof(FadeOutDuration));
    }

    /// <summary>Gets current terminal-cell visibility from zero through one.</summary>
    /// <remarks>
    /// Zero also represents an unavailable surface. Entrance advances toward one, stable
    /// presentation remains one, and an accepted positive-duration exit advances toward zero.
    /// </remarks>
    public double FadeProgress { get; private set; }

    /// <summary>Gets whether the common lifecycle currently represents a presented surface.</summary>
    protected bool IsSurfacePresented { get; private set; }

    /// <summary>Gets whether the common lifecycle still represents a logically open surface.</summary>
    private protected bool IsSurfaceOpen { get; private set; }

    /// <summary>Gets whether an accepted close is visually disappearing before structural cleanup.</summary>
    private protected bool IsSurfaceExiting { get; private set; }

    /// <summary>Gets whether this surface currently owns one active application modality scope.</summary>
    protected bool HasActiveSurfaceModal => _modalSession.IsActive;

    /// <summary>Gets the identity of the current common presentation transaction.</summary>
    internal long SurfacePresentationVersion { get; private set; }

    /// <summary>Raises the inherited opened notification after the surface becomes presented.</summary>
    protected void RaiseSurfaceOpened() => Opened?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises the inherited closing notification for a family-specific close request.</summary>
    protected void RaiseSurfaceClosing() => Closing?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises the inherited closed notification after a family confirms presentation unavailability.</summary>
    protected void RaiseSurfaceClosed() => Closed?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises <see cref="CloseRequested"/> and reports whether a handler vetoed the request.</summary>
    /// <returns>False when a handler set <see cref="SurfaceCloseRequestedEventArgs.Cancel"/>; otherwise true.</returns>
    protected bool RaiseCloseRequested()
    {
        if (CloseRequested is not { } closeRequested)
        {
            return true;
        }

        var requestArgs = new SurfaceCloseRequestedEventArgs();
        closeRequested.Invoke(this, requestArgs);
        return !requestArgs.Cancel;
    }

    /// <summary>
    /// Captures the current <see cref="Closed"/> invocation list so it can still be raised after a
    /// synchronous <see cref="Closing"/> handler disposes the surface, which otherwise nulls the field
    /// before the caller gets a chance to raise it.
    /// </summary>
    protected EventHandler? CaptureClosedHandlers() => Closed;

    /// <summary>Begins a fresh logical surface lifetime before presentation is available.</summary>
    private protected void BeginSurfaceOpenLifetime() => IsSurfaceOpen = true;

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
    protected void OpenSurface([InstantHandle] Action commitOpenState)
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

        if (_isClosing && !_allowsOpeningDuringClosing)
        {
            throw new InvalidOperationException("A floating surface cannot open while it is closing.");
        }

        if (IsSurfacePresented)
        {
            throw new InvalidOperationException("The floating surface is already open.");
        }

        _isOpening = true;
        _openingInvalidated = false;

        long presentationVersion;
        var fadeInDuration = ResolveFadeInDuration();

        try
        {
            commitOpenState();

            if (_openingInvalidated || Dispatcher is null)
            {
                throw new InvalidOperationException(
                    "A floating surface cannot finish opening after it becomes unavailable.");
            }

            IsSurfacePresented = true;
            IsSurfaceOpen = true;
            IncrementPresentationVersion();
            presentationVersion = SurfacePresentationVersion;
            _isEnteringFade = fadeInDuration > TimeSpan.Zero;
            SetFadeProgress(_isEnteringFade ? 0 : 1);
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

        if (_isEnteringFade &&
            IsSurfacePresented &&
            presentationVersion == SurfacePresentationVersion &&
            Dispatcher is not null)
        {
            StartFadeTransition(FadeProgress, 1, fadeInDuration, presentationVersion);
        }
    }

    /// <summary>Closes one presented surface through the common ordered cleanup transaction.</summary>
    /// <param name="commitClosingState">Commits family state that makes the surface ineligible.</param>
    /// <param name="commitUnavailableState">Makes the family-specific content unavailable.</param>
    /// <returns><see langword="true"/> when a presented surface was closed; otherwise false.</returns>
    /// <remarks>
    /// Cleanup continues after callback failures. After all stages complete, the earliest failure is rethrown.
    /// Repeated closure after committed cleanup or synchronously from <see cref="CloseRequested"/> is harmless.
    /// </remarks>
    /// <exception cref="ArgumentNullException">A callback is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The attached surface is mutated off-dispatcher, is opening, or closure is reentered.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The surface is disposed.</exception>
    /// <exception cref="Exception">A state callback, lifecycle subscriber, or modal cleanup callback fails.</exception>
    protected bool CloseSurface(
        [InstantHandle] Action commitClosingState,
        [InstantHandle] Action commitUnavailableState)
    {
        ArgumentNullException.ThrowIfNull(commitClosingState);
        ArgumentNullException.ThrowIfNull(commitUnavailableState);
        return CloseSurfaceWithOutcome(commitClosingState, commitUnavailableState) is
            FloatingSurfaceCloseOutcome.Deferred or FloatingSurfaceCloseOutcome.Completed;
    }

    /// <summary>Closes one presented surface and returns its precise shared outcome.</summary>
    /// <param name="commitClosingState">Commits family state before Closing.</param>
    /// <param name="commitUnavailableState">Makes family content structurally unavailable.</param>
    /// <param name="completion">An optional exact-once callback after complete disappearance.</param>
    /// <returns>The committed close outcome.</returns>
    private protected FloatingSurfaceCloseOutcome CloseSurfaceWithOutcome(
        [InstantHandle] Action commitClosingState,
        [InstantHandle] Action commitUnavailableState,
        [InstantHandle] Action? completion = null)
    {
        ArgumentNullException.ThrowIfNull(commitClosingState);
        ArgumentNullException.ThrowIfNull(commitUnavailableState);
        return CloseSurfaceCore(
            commitClosingState,
            prepareClosingState: null,
            commitClosingStateAfterClosing: null,
            commitUnavailableState,
            publishCloseRequested: true,
            publishClosing: true,
            allowUnpresentedOpen: false,
            completion);
    }

    /// <summary>Closes one logical surface whose family state commits after Closing observers run.</summary>
    /// <param name="prepareClosingState">Begins family observation immediately before Closing.</param>
    /// <param name="commitClosingState">Commits family state and reports whether closure completed.</param>
    /// <param name="commitUnavailableState">Makes family-specific content unavailable.</param>
    /// <returns>True when closure completed; false when the surface was closed already, vetoed, or retained.</returns>
    /// <exception cref="ArgumentNullException">A callback is null.</exception>
    /// <exception cref="InvalidOperationException">Opening or closure is reentered.</exception>
    /// <exception cref="ObjectDisposedException">The surface is disposed.</exception>
    /// <exception cref="Exception">A state callback, lifecycle subscriber, or modal cleanup callback fails.</exception>
    private protected bool CloseSurfaceAfterClosing(
        [InstantHandle] Action prepareClosingState,
        [InstantHandle] Func<bool> commitClosingState,
        [InstantHandle] Action commitUnavailableState)
    {
        ArgumentNullException.ThrowIfNull(prepareClosingState);
        ArgumentNullException.ThrowIfNull(commitClosingState);
        ArgumentNullException.ThrowIfNull(commitUnavailableState);
        return CloseSurfaceAfterClosingWithOutcome(
            prepareClosingState,
            commitClosingState,
            commitUnavailableState) is
            FloatingSurfaceCloseOutcome.Deferred or FloatingSurfaceCloseOutcome.Completed;
    }

    /// <summary>Closes one retained-family surface after Closing observers determine whether it remains available.</summary>
    /// <param name="prepareClosingState">Begins concrete retention observation.</param>
    /// <param name="commitClosingState">Reports whether closure was accepted.</param>
    /// <param name="commitUnavailableState">Makes family content structurally unavailable.</param>
    /// <param name="completion">An optional exact-once callback after complete disappearance.</param>
    /// <returns>The committed close outcome.</returns>
    private protected FloatingSurfaceCloseOutcome CloseSurfaceAfterClosingWithOutcome(
        [InstantHandle] Action prepareClosingState,
        [InstantHandle] Func<bool> commitClosingState,
        [InstantHandle] Action commitUnavailableState,
        [InstantHandle] Action? completion = null)
    {
        ArgumentNullException.ThrowIfNull(prepareClosingState);
        ArgumentNullException.ThrowIfNull(commitClosingState);
        ArgumentNullException.ThrowIfNull(commitUnavailableState);
        return CloseSurfaceCore(
            commitClosingStateBeforeClosing: null,
            prepareClosingState,
            commitClosingState,
            commitUnavailableState,
            publishCloseRequested: true,
            publishClosing: true,
            allowUnpresentedOpen: true,
            completion);
    }

    /// <summary>Completes closure after the concrete surface has already published its closing request.</summary>
    /// <param name="commitClosingState">Commits family state that makes the surface ineligible.</param>
    /// <param name="commitUnavailableState">Makes the family-specific content unavailable.</param>
    /// <returns><see langword="true"/> when a presented surface was closed; otherwise false.</returns>
    /// <remarks>
    /// This seam avoids a duplicate <see cref="CloseRequested"/> and a duplicate <see cref="Closing"/>
    /// notification while preserving modal exit, unavailable-state commit, bounds clearing, and one
    /// final <see cref="Closed"/> notification.
    /// Cleanup continues after callback failures and rethrows the earliest failure.
    /// </remarks>
    /// <exception cref="ArgumentNullException">A callback is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The attached surface is mutated off-dispatcher, is opening, or closure is reentered.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The surface is disposed.</exception>
    /// <exception cref="Exception">A state callback, lifecycle subscriber, or modal cleanup callback fails.</exception>
    private protected bool CloseSurfaceAfterClosingRequest(
        [InstantHandle] Action commitClosingState,
        [InstantHandle] Action commitUnavailableState)
    {
        ArgumentNullException.ThrowIfNull(commitClosingState);
        ArgumentNullException.ThrowIfNull(commitUnavailableState);
        return CloseSurfaceAfterClosingRequestWithOutcome(
            commitClosingState,
            commitUnavailableState) is
            FloatingSurfaceCloseOutcome.Deferred or FloatingSurfaceCloseOutcome.Completed;
    }

    /// <summary>Completes one close whose request and Closing notifications were already published.</summary>
    /// <param name="commitClosingState">Commits family closing state.</param>
    /// <param name="commitUnavailableState">Makes family content structurally unavailable.</param>
    /// <param name="completion">An optional exact-once callback after complete disappearance.</param>
    /// <returns>The committed close outcome.</returns>
    private protected FloatingSurfaceCloseOutcome CloseSurfaceAfterClosingRequestWithOutcome(
        [InstantHandle] Action commitClosingState,
        [InstantHandle] Action commitUnavailableState,
        [InstantHandle] Action? completion = null)
    {
        ArgumentNullException.ThrowIfNull(commitClosingState);
        ArgumentNullException.ThrowIfNull(commitUnavailableState);
        return CloseSurfaceCore(
            commitClosingState,
            prepareClosingState: null,
            commitClosingStateAfterClosing: null,
            commitUnavailableState,
            publishCloseRequested: false,
            publishClosing: false,
            allowUnpresentedOpen: false,
            completion);
    }

    private FloatingSurfaceCloseOutcome CloseSurfaceCore(
        [InstantHandle] Action? commitClosingStateBeforeClosing,
        [InstantHandle] Action? prepareClosingState,
        [InstantHandle] Func<bool>? commitClosingStateAfterClosing,
        [InstantHandle] Action commitUnavailableState,
        bool publishCloseRequested,
        bool publishClosing,
        bool allowUnpresentedOpen,
        [InstantHandle] Action? completion)
    {
        Debug.Assert(commitUnavailableState is not null, "A close transaction requires unavailable-state cleanup.");
        Debug.Assert(
            commitClosingStateBeforeClosing is not null ||
            (prepareClosingState is not null && commitClosingStateAfterClosing is not null),
            "A close transaction requires one complete family commit shape.");

        VerifyMutable();

        if (_isOpening)
        {
            throw new InvalidOperationException("A floating surface cannot close while it is opening.");
        }

        if (_isClosing)
        {
            throw new InvalidOperationException("Floating surface closure cannot be reentered.");
        }

        if (IsSurfaceExiting)
        {
            if (!publishCloseRequested && !publishClosing && completion is not null)
            {
                _deferredCloseCompletion += completion;
                return FloatingSurfaceCloseOutcome.Deferred;
            }

            return FloatingSurfaceCloseOutcome.Ignored;
        }

        if (!IsSurfacePresented && (!allowUnpresentedOpen || !IsSurfaceOpen))
        {
            return FloatingSurfaceCloseOutcome.Ignored;
        }

        if (_isRequestingClose)
        {
            return FloatingSurfaceCloseOutcome.Ignored;
        }

        _isRequestingClose = true;

        try
        {
            if (publishCloseRequested && !RaiseCloseRequested())
            {
                return FloatingSurfaceCloseOutcome.Vetoed;
            }
        }
        finally
        {
            _isRequestingClose = false;
        }

        _isClosing = true;
        ExceptionDispatchInfo? failure = null;
        var closedHandlers = CaptureClosedHandlers();
        var wasPresented = IsSurfacePresented;
        var closureCompleted = true;

        try
        {
            if (commitClosingStateBeforeClosing is { } commitBeforeClosing)
            {
                CaptureFailure(commitBeforeClosing, ref failure);
            }

            if (prepareClosingState is { } prepare)
            {
                CaptureFailure(prepare, ref failure);
            }

            if (publishClosing)
            {
                _allowsOpeningDuringClosing = commitClosingStateAfterClosing is not null;

                try
                {
                    CaptureFailure(RaiseSurfaceClosing, ref failure);
                }
                finally
                {
                    _allowsOpeningDuringClosing = false;
                }
            }

            if (commitClosingStateAfterClosing is { } commitAfterClosing)
            {
                CaptureFailure(
                    () => closureCompleted = commitAfterClosing(),
                    ref failure);
            }

        }
        finally
        {
            _isClosing = false;
        }

        if (!closureCompleted)
        {
            failure?.Throw();
            return FloatingSurfaceCloseOutcome.Vetoed;
        }

        UpdateFadeProgressFromClock();
        var fadeOutDuration = ResolveFadeOutDuration();

        if (failure is null && wasPresented && fadeOutDuration > TimeSpan.Zero && FadeProgress > 0)
        {
            BeginDeferredClose(
                fadeOutDuration,
                commitUnavailableState,
                closedHandlers,
                wasPresented,
                completion);
            return FloatingSurfaceCloseOutcome.Deferred;
        }

        CompleteSurfaceClose(commitUnavailableState, closedHandlers, wasPresented, completion, ref failure);
        failure?.Throw();
        return FloatingSurfaceCloseOutcome.Completed;
    }

    private void BeginDeferredClose(
        TimeSpan duration,
        Action commitUnavailableState,
        EventHandler? closedHandlers,
        bool wasPresented,
        Action? completion)
    {
        DisposeFadeTimer();
        _isEnteringFade = false;
        IsSurfaceExiting = true;
        _deferredUnavailableCommit = commitUnavailableState;
        _deferredClosedHandlers = closedHandlers;
        _deferredWasPresented = wasPresented;
        _deferredCloseCompletion = completion;
        ExceptionDispatchInfo? failure = null;

        CaptureFailure(OnSurfaceExitAccepted, ref failure);
        CaptureFailure(() => Invalidate(Invalidation.Render), ref failure);

        if (failure is null)
        {
            try
            {
                StartFadeTransition(
                    FadeProgress,
                    0,
                    duration,
                    SurfacePresentationVersion);
                return;
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        }

        CompleteDeferredClose(ref failure);
        failure?.Throw();
    }

    private void CompleteDeferredClose()
    {
        ExceptionDispatchInfo? failure = null;
        CompleteDeferredClose(ref failure);
        failure?.Throw();
    }

    private void CompleteDeferredClose(ref ExceptionDispatchInfo? failure)
    {
        var commitUnavailableState = _deferredUnavailableCommit ?? (static () => { });
        var closedHandlers = _deferredClosedHandlers;
        var wasPresented = _deferredWasPresented;
        var completion = _deferredCloseCompletion;
        ClearDeferredClose();
        CompleteSurfaceClose(commitUnavailableState, closedHandlers, wasPresented, completion, ref failure);
    }

    private void CompleteSurfaceClose(
        Action commitUnavailableState,
        EventHandler? closedHandlers,
        bool wasPresented,
        Action? completion,
        ref ExceptionDispatchInfo? failure)
    {
        DisposeFadeTimer();
        _isEnteringFade = false;
        IsSurfaceOpen = false;
        _isCompletingClose = true;

        try
        {
            CaptureFailure(commitUnavailableState, ref failure);
            CaptureFailure(ExitSurfaceModal, ref failure);

            if (IsSurfacePresented)
            {
                SurfaceBounds = default;
                IsSurfacePresented = false;
                IncrementPresentationVersion();
            }
        }
        finally
        {
            _isCompletingClose = false;
            IsSurfaceExiting = false;
            SetFadeProgressCapturing(0, ref failure);
        }

        // Closed describes complete disappearance. The transition guards are released first so a
        // handler can begin a distinct presentation without reentering this close transaction.
        if (wasPresented && closedHandlers is { } capturedClosed)
        {
            CaptureFailure(() => capturedClosed.Invoke(this, EventArgs.Empty), ref failure);
        }

        if (completion is not null)
        {
            CaptureFailure(completion, ref failure);
        }
    }

    private void ClearDeferredClose()
    {
        _deferredUnavailableCommit = null;
        _deferredClosedHandlers = null;
        _deferredWasPresented = false;
        _deferredCloseCompletion = null;
    }

    private void StartFadeTransition(
        double start,
        double target,
        TimeSpan duration,
        long presentationVersion)
    {
        Debug.Assert(duration > TimeSpan.Zero, "Only positive fades own a timer.");
        var dispatcher = Dispatcher ?? throw new InvalidOperationException(
            "A floating-surface fade requires an attached dispatcher.");
        DisposeFadeTimer();
        _fadeTransition = new FloatingSurfaceTransition(dispatcher.TimeProvider, duration, start, target);
        _fadePresentationVersion = presentationVersion;
        var timer = new DispatcherTimer(dispatcher, ResolveFadeTimerInterval(duration));
        timer.Tick += OnFadeTimerTick;
        _fadeTimer = timer;
        timer.Start();
    }

    private void OnFadeTimerTick(object? sender, EventArgs eventArgs)
    {
        _ = eventArgs;

        if (!ReferenceEquals(sender, _fadeTimer) ||
            !IsSurfacePresented ||
            Dispatcher is null ||
            _fadePresentationVersion != SurfacePresentationVersion ||
            _fadeTransition is not { } transition)
        {
            return;
        }

        SetFadeProgress(transition.Progress);
        var completed = IsSurfaceExiting ? FadeProgress <= 0 : FadeProgress >= 1;

        if (completed)
        {
            DisposeFadeTimer();

            if (IsSurfaceExiting)
            {
                CompleteDeferredClose();
            }
            else
            {
                _isEnteringFade = false;
                OnSurfaceEntranceCompleted();
            }

            return;
        }

        var interval = ResolveFadeTimerInterval(transition.Remaining);

        if (_fadeTimer is { } timer && timer.Interval != interval)
        {
            timer.Interval = interval;
        }
    }

    private void UpdateFadeProgressFromClock()
    {
        if (_fadeTransition is { } transition &&
            IsSurfacePresented &&
            _fadePresentationVersion == SurfacePresentationVersion)
        {
            SetFadeProgress(transition.Progress);
        }
    }

    private void DisposeFadeTimer()
    {
        var timer = _fadeTimer;
        _fadeTimer = null;
        _fadeTransition = null;

        if (timer is not null)
        {
            timer.Tick -= OnFadeTimerTick;
            timer.Dispose();
        }
    }

    private void AbortFadeTransition()
    {
        var wasTransitioning = _isEnteringFade || IsSurfaceExiting || _fadeTimer is not null;
        var completion = _deferredCloseCompletion;
        DisposeFadeTimer();
        _isEnteringFade = false;
        IsSurfaceExiting = false;
        ClearDeferredClose();

        if (wasTransitioning && !_isCompletingClose)
        {
            OnSurfaceTransitionAborted();

            if (completion is not null)
            {
                var dispatcher = Dispatcher;

                if (dispatcher is null)
                {
                    completion();
                }
                else
                {
                    dispatcher.Post(completion);
                }
            }
        }
    }

    private static TimeSpan ResolveFadeTimerInterval(TimeSpan remaining)
    {
        var minimum = TimeSpan.FromMilliseconds(1);

        return remaining <= minimum
            ? minimum
            : remaining < _fadeRefreshInterval
                ? remaining
                : _fadeRefreshInterval;
    }

    private void SetFadeProgress(double value)
    {
        value = Math.Clamp(value, 0, 1);

        if (FadeProgress == value)
        {
            return;
        }

        FadeProgress = value;
        OnFadeProgressChanged();
        Invalidate(Invalidation.Render);
        NotifyPropertyChanged(nameof(FadeProgress), InvalidationImpact.None);
    }

    private void SetFadeProgressCapturing(double value, ref ExceptionDispatchInfo? failure) =>
        CaptureFailure(() => SetFadeProgress(value), ref failure);

    private void SetFadeDuration(ref TimeSpan field, TimeSpan value, string propertyName)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(propertyName, value, "Fade duration cannot be negative.");
        }

        if (value > TimeSpan.Zero)
        {
            DispatcherTimer.ValidateInterval(value, propertyName);
        }

        VerifyMutable();

        if (IsSurfacePresented || IsSurfaceExiting)
        {
            throw new InvalidOperationException("Fade durations cannot change while a floating surface is presented or exiting.");
        }

        if (field == value)
        {
            return;
        }

        field = value;
        NotifyPropertyChanged(propertyName, InvalidationImpact.None);
    }

    /// <summary>Resolves the effective entrance fade duration for one new presentation.</summary>
    /// <returns>A validated non-negative duration.</returns>
    private protected virtual TimeSpan ResolveFadeInDuration() => FadeInDuration;

    /// <summary>Resolves the effective exit fade duration for one accepted close.</summary>
    /// <returns>A validated non-negative duration.</returns>
    private protected virtual TimeSpan ResolveFadeOutDuration() => FadeOutDuration;

    /// <summary>Responds after a positive entrance reaches full cell visibility.</summary>
    private protected virtual void OnSurfaceEntranceCompleted()
    {
    }

    /// <summary>Responds after shared fade progress commits and before its public notification.</summary>
    private protected virtual void OnFadeProgressChanged()
    {
    }

    /// <summary>Cancels family interaction and source timers immediately after positive exit is accepted.</summary>
    private protected virtual void OnSurfaceExitAccepted() =>
        CaptureOwner?.Unavailable(this, ReleaseReason.Hidden);

    /// <summary>Gets whether routed and semantic input must be consumed for the supplied subtree member.</summary>
    /// <param name="control">The candidate control, or null.</param>
    /// <returns>True when an exiting floating-surface ancestor owns the candidate.</returns>
    internal static bool SuppressesInteraction(ControlBase? control)
    {
        for (var current = control; current is not null; current = current.Parent)
        {
            if (current is FloatingSurfaceBase { IsSurfaceExiting: true })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Settles family state when direct hide, detach, or disposal aborts a transition.</summary>
    private protected virtual void OnSurfaceTransitionAborted()
    {
    }

    #endregion

    #region Rendering

    /// <inheritdoc/>
    private protected override bool RequiresCompleteRenderEffect =>
        IsSurfacePresented && (IsSurfaceExiting || FadeProgress < 1);

    /// <inheritdoc/>
    private protected override void RenderFreshWithCompleteEffect(
        TerminalCanvas canvas,
        TerminalCanvas visual,
        Rect contentClip) =>
        visual.DrawWithCurrentFrameDissolve(
            FadeProgress,
            revealNewImages: !IsSurfaceExiting && FadeProgress >= 1,
            () => base.RenderFreshWithCompleteEffect(canvas, visual, contentClip));

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
    [MustDisposeResource]
    protected ModalScope EnterSurfaceModal(
        OutsideInteraction outsideInteraction,
        ControlBase? initialFocus)
    {
        VerifyMutable();

        ArgumentOutOfRangeException.ThrowIfNotDefined(outsideInteraction, nameof(outsideInteraction), "The outside-interaction policy is unknown.");

        if (_isClosing)
        {
            throw new InvalidOperationException("A floating surface cannot enter modality while it is closing.");
        }

        if (!IsSurfacePresented)
        {
            throw new InvalidOperationException("A floating surface must be presented before it can enter modality.");
        }

        if (_isEnteringModal || _modalSession.IsEntering)
        {
            throw new InvalidOperationException("Floating surface modal entry cannot be reentered.");
        }

        if (_modalSession.IsActive)
        {
            throw new InvalidOperationException("The floating surface is already modal.");
        }

        var modality = ModalityOwner ?? throw new InvalidOperationException(
            "A modal floating surface must belong to an attached application tree.");
        var presentationVersion = SurfacePresentationVersion;
        _isEnteringModal = true;

        try
        {
            return _modalSession.Enter(
                () => modality.Enter(this, outsideInteraction, initialFocus),
                () => IsSurfacePresented &&
                    presentationVersion == SurfacePresentationVersion &&
                    Dispatcher is not null &&
                    ReferenceEquals(ModalityOwner, modality));
        }
        finally
        {
            _isEnteringModal = false;
        }
    }

    /// <summary>Ends this surface's active modal presentation, if any.</summary>
    /// <remarks>The tracked identity clears before callbacks run so replacement lifetimes remain distinct.</remarks>
    /// <exception cref="Exception">Modal focus restoration or an exit callback fails after cleanup.</exception>
    protected void ExitSurfaceModal() => _modalSession.Exit();

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        _presentationReleasedForPendingDetach = reason == ReleaseReason.Detached;

        if (reason is ReleaseReason.Hidden or ReleaseReason.Disposed)
        {
            IsSurfaceOpen = false;
        }
        ExceptionDispatchInfo? failure = null;
        CaptureFailure(ExitSurfaceModal, ref failure);
        CaptureFailure(() => base.OnUnavailable(reason), ref failure);

        if (RemovesPresentation(reason))
        {
            ReleasePresentation();
        }

        if (reason == ReleaseReason.Disposed)
        {
            Opened = null;
            CloseRequested = null;
            Closing = null;
            Closed = null;
        }

        failure?.Throw();
    }

    /// <inheritdoc/>
    protected override void OnDetached()
    {
        var presentationAlreadyReleased = _presentationReleasedForPendingDetach;
        _presentationReleasedForPendingDetach = false;
        ExceptionDispatchInfo? failure = null;
        CaptureFailure(ExitSurfaceModal, ref failure);
        CaptureFailure(base.OnDetached, ref failure);

        if (!presentationAlreadyReleased)
        {
            CaptureFailure(ReleasePresentation, ref failure);
        }

        failure?.Throw();
    }

    /// <summary>Applies family policy for a current active modal dismissal request.</summary>
    /// <param name="scope">The exact current active scope.</param>
    private protected virtual void OnSurfaceModalDismissRequested(ModalScope scope) =>
        _ = scope;

    /// <summary>Applies family policy after an externally ended scope clears from the session.</summary>
    /// <param name="scope">The exact exited scope.</param>
    private protected virtual void OnSurfaceModalExited(ModalScope scope) =>
        _ = scope;

    /// <summary>
    /// Clears this surface's presented bounds and flag outside the normal close path, so a caller that
    /// bypasses <see cref="OnUnavailable"/>'s own <see cref="ReleaseReason.Detached"/> handling — such as
    /// a descendant of a removed subtree root, which never receives its own <c>OnUnavailable</c> call —
    /// can still leave <see cref="OpenSurface"/> reopenable afterward.
    /// </summary>
    private protected void ReleasePresentation()
    {
        ExceptionDispatchInfo? failure = null;
        CaptureFailure(AbortFadeTransition, ref failure);
        SurfaceBounds = default;
        IsSurfacePresented = false;
        IsSurfaceOpen = false;
        _openingInvalidated = _isOpening;
        IncrementPresentationVersion();
        SetFadeProgressCapturing(0, ref failure);
        failure?.Throw();
    }

    private void IncrementPresentationVersion() =>
        SurfacePresentationVersion = unchecked(SurfacePresentationVersion + 1);

    [Pure]
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
