// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Popups;

using System.Runtime.ExceptionServices;

using SharpVision.Surfaces;
using SharpVision.Terminal.Input;

using MustDisposeResource = JetBrains.Annotations.MustDisposeResourceAttribute;

/// <summary>Displays one owned content control on an opaque, framed, anchor-relative modal surface.</summary>
[PublicAPI]
public class Popup: FloatingSurfaceBase, IOwnedChildDisposalObserver
{
    /// <summary>Raised before directly disposed content leaves this popup's owned slot.</summary>
    internal event EventHandler<OwnedContentDisposalEventArgs>? ContentDisposalRequested;

    /// <inheritdoc/>
    void IOwnedChildDisposalObserver.OnOwnedChildDisposalRequested(ControlBase child) =>
        ContentDisposalRequested?.Invoke(this, new OwnedContentDisposalEventArgs(child));

    /// <summary>Gets or sets the border and shadow together as one composite value; either component
    /// left null keeps that part on Theme ownership.</summary>
    public PopupChrome Style
    {
        get => new() { Border = LocalBorder, Shadow = LocalShadow };
        set
        {
            var previous = Style;

            if (value.Border.HasValue)
            {
                Border = value.Border.Value;
            }
            else
            {
                ResetBorder();
            }

            if (value.Shadow.HasValue)
            {
                Shadow = value.Shadow.Value;
            }
            else
            {
                ResetShadow();
            }

            if (!previous.Equals(Style))
            {
                NotifyPropertyChanged(nameof(Style), InvalidationImpact.None);
            }
        }
    }

    #region Construction and ownership

    /// <inheritdoc/>
    protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
        (theme ?? ThemeCatalog.Dark).GetStyleSet(PopupStyle.Default).ToAppearanceStates();

    /// <summary>Initializes a closed themed surface below its eventual anchor.</summary>
    public Popup()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        IsFocusable = false;
        PropertyChanged += OnPopupPropertyChanged;
        EnableChromeAuthoring();
    }

    /// <inheritdoc/>
    protected override void OnContentChanged(ControlBase? previous, ControlBase? current)
    {
        base.OnContentChanged(previous, current);

        _ = current?.Visibility = IsOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Gets or sets the optional sibling anchor used to place the open surface.</summary>
    /// <exception cref="InvalidOperationException">The attached popup is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The popup is disposed.</exception>
    public ControlBase? Anchor
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Arrange);
    }

    /// <summary>Gets or sets the preferred anchor-relative placement.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached popup is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The popup is disposed.</exception>
    public PopupPlacement Placement
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The popup placement is unknown.");

            _ = SetProperty(ref field, value, InvalidationImpact.Arrange);
        }
    } = PopupPlacement.Below;

    #endregion

    #region Surface appearance

    /// <summary>Gets or sets whether the surface omits the frame edge adjoining its anchor.</summary>
    public bool ConnectsToAnchor { get; init; }

    /// <summary>Gets or sets whether placement flips and clamps inside the owning root.</summary>
    internal bool ConstrainToRoot { get; set; } = true;

    /// <summary>Gets or sets whether this popup re-resolves placement when its foreign anchor
    /// reflows while open. A self-anchored composite (a ComboBox drop-down, a submenu, and the
    /// like) re-arranges its own popup child from its own ArrangeOverride every pass, so tracking
    /// here would be redundant; those owners set this false and keep their existing behavior.</summary>
    /// <remarks>
    /// Consulted only when a reflow subscription is (re)established - on content becoming
    /// available while opening, and on Anchor changing while already open. Set this before the
    /// popup first opens; every in-repo owner does so from its constructor. Toggling it while a
    /// subscription is already live does not retroactively detach or attach one - the change only
    /// takes effect the next time subscription is reconsidered.
    /// </remarks>
    internal bool TracksAnchorReflow { get; set; } = true;

    /// <summary>Gets or sets whether opening skips the tree-wide sibling popup close.</summary>
    public bool SuppressCloseOtherPopups { get; init; }

    /// <summary>Gets or sets whether the frame draws a directional arrow toward the anchor.</summary>
    /// <exception cref="InvalidOperationException">The attached popup is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The popup is disposed.</exception>
    public bool ShowAnchorIndicator
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Render);
    }

    /// <summary>Gets or sets an optional fixed origin that overrides anchor-based positioning.</summary>
    internal Point? FixedOrigin { get; set; }

    #endregion

    #region Visibility and interaction

    private bool _isOpen;
    private bool _isOpeningModal;

    private bool _isOpenTransitioning;
    private ControlBase? _availabilityAncestor;
    private ModalScope? _modalCallbackScope;
    private ControlBase? _subscribedReflowAnchor;

    /// <summary>Gets or sets whether this Popup self-manages its modal scope on open.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached popup is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The popup is disposed.</exception>
    public PopupModalBehavior ModalBehavior
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The popup modal behavior is unknown.");

            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    }

    /// <summary>Gets or sets whether opening transfers focus to the first eligible descendant.</summary>
    /// <remarks>
    /// The default preserves popup behavior for dialogs and menus. Composite controls whose popup
    /// is an implementation detail set this to <see langword="false"/> and retain focus on their
    /// public owner.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached popup is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The popup is disposed.</exception>
    public bool FocusOnOpen
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.None);
    } = true;

    /// <summary>Gets or sets whether the popup owns its visible default dismissing modal presentation.</summary>
    /// <remarks>
    /// A changed value commits and publishes first. Opening then exposes the current content, enters
    /// a default dismissing modal scope, and selects focus inside the plane. Closing therefore raises
    /// <see cref="FloatingSurfaceBase.Closing"/> after this property is false:
    /// current content retains its pre-close availability and the previous <see cref="FloatingSurfaceBase.SurfaceBounds"/>
    /// remains readable, while the surface is already ineligible for rendering and hit testing. The
    /// transition raises <see cref="FloatingSurfaceBase.Closing"/> while the automatic modal scope is still active, then exits
    /// modality, collapses current content, clears the surface bounds, and raises
    /// <see cref="FloatingSurfaceBase.Closed"/>. Every stage completes when a callback fails, after which the earliest
    /// failure is rethrown. Reentrant open-state transitions are rejected.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The attached popup is mutated off-dispatcher, an open-state transition is reentered, or modal entry fails.
    /// </exception>
    /// <exception cref="ArgumentException">The attached popup is not an eligible modal root.</exception>
    /// <exception cref="ObjectDisposedException">The popup is disposed.</exception>
    /// <exception cref="Exception">A focus, scope, pointer-cleanup, or user callback fails after committed cleanup.</exception>
    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (value && !_isOpen && ModalBehavior == PopupModalBehavior.Auto && ModalityOwner is not null)
            {
                _ = OpenModalCore(
                    OutsideInteraction.Dismiss,
                    initialFocus: null,
                    exposed: true);
                return;
            }

            SetOpen(value, suppressFocusOnOpen: false);
        }
    }

    /// <summary>Opens this popup and enters one application-owned modal presentation rooted at it.</summary>
    /// <param name="outsideInteraction">
    /// The outside-input policy. The default requests dismissal, which closes the popup.
    /// </param>
    /// <param name="initialFocus">
    /// An optional eligible focus target owned by this popup; null selects the first eligible descendant.
    /// </param>
    /// <returns>The disposable scope representing this presentation's modal lifetime.</returns>
    /// <remarks>
    /// One popup may own only one live modal presentation. The returned scope owns modality, not
    /// visual lifetime: disposing it externally closes the popup so an attached surface never remains
    /// modeless accidentally. Closing the popup ordinarily raises <see cref="FloatingSurfaceBase.Closing"/>,
    /// then disposes its live scope before content becomes unavailable. When this call exposes a closed
    /// popup, it defers legacy <see cref="FocusOnOpen"/> behavior so modal entry can snapshot and then
    /// replace background focus transactionally. A failed entry recloses only a popup exposed by this
    /// call; cleanup callback failures never replace the initiating exception.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="outsideInteraction"/> is undefined.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// This popup is not a valid available application modal root, or <paramref name="initialFocus"/>
    /// is unavailable, ineligible, foreign, or outside this popup.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The popup is detached, is mutated off-dispatcher, is already transitioning its open state,
    /// is reentering modal presentation, or already owns a live modal presentation.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The popup, modality manager, or supplied focus target is disposed.</exception>
    /// <exception cref="Exception">
    /// An opening, pointer-cleanup, focus, scope, or user callback fails. Modal entry and any visual
    /// exposure are rolled back before the earliest failure is rethrown.
    /// </exception>
    [MustDisposeResource]
    public ModalScope OpenModal(
        OutsideInteraction outsideInteraction = OutsideInteraction.Dismiss,
        ControlBase? initialFocus = null)
    {
        VerifyMutable();

        if (_isOpeningModal)
        {
            throw new InvalidOperationException("Popup modal presentations cannot be reentered.");
        }

        _isOpeningModal = true;

        try
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(outsideInteraction, nameof(outsideInteraction), "The outside-interaction policy is unknown.");

            if (_isOpenTransitioning)
            {
                throw new InvalidOperationException("Popup modal presentation cannot begin during an open-state transition.");
            }

            if (HasActiveSurfaceModal)
            {
                throw new InvalidOperationException("The Popup already has an active modal presentation.");
            }

            _ = ModalityOwner ?? throw new InvalidOperationException(
                "A modal Popup must belong to an attached application tree.");
            return OpenModalCore(outsideInteraction, initialFocus, exposed: !IsOpen);
        }
        finally
        {
            _isOpeningModal = false;
        }
    }

    private ModalScope OpenModalCore(
        OutsideInteraction outsideInteraction,
        ControlBase? initialFocus,
        bool exposed)
    {
        ModalScope? scope = null;

        try
        {
            if (exposed)
            {
                SetOpen(value: true, suppressFocusOnOpen: true);
            }

            scope = EnterSurfaceModal(outsideInteraction, initialFocus);

            // User callbacks reached during modal entry may synchronously close
            // the Popup or dispose the just-entered manager scope. Preserve that
            // decision and never retain a stale presentation handle.
            if (!scope.IsActive || !IsOpen)
            {
                if (scope.IsActive)
                {
                    scope.Dispose();
                }

                return scope;
            }

            TrackModalCallbacks(scope);
            return scope;
        }
        catch (Exception exception)
        {
            var failure = ExceptionDispatchInfo.Capture(exception);

            if (scope is { IsActive: true })
            {
                try
                {
                    scope.Dispose();
                }
                catch
                {
                    // The initiating failure remains authoritative.
                }
            }

            if (exposed && IsOpen)
            {
                try
                {
                    SetOpen(value: false, suppressFocusOnOpen: false);
                }
                catch
                {
                    // Entry rollback is complete enough to keep the initiating failure.
                }
            }

            failure.Throw();
            throw;
        }
    }

    /// <summary>Gets or sets whether Escape closes this open popup.</summary>
    /// <exception cref="InvalidOperationException">The attached popup is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The popup is disposed.</exception>
    public bool CloseOnEscape
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.None);
    } = true;

    /// <inheritdoc/>
    protected override bool ClipsChildren => false;

    /// <inheritdoc/>
    internal override ControlBase? HitTest(Point point)
    {
        return !IsOpen || IsDisposed || !IsHitTestVisible || !EffectiveIsVisible || !EffectiveIsEnabled
            ? null
            : Content?.HitTest(point) ?? (SurfaceBounds.Contains(point) ? this : null);
    }

    /// <inheritdoc/>
    internal override ControlBase? HitTestPopupCore(Point point) => IsOpen ? HitTest(point) : null;

    /// <inheritdoc/>
    internal override OwnedControlLayer IntrinsicLayer => OwnedControlLayer.Popup;

    #endregion

    #region Layout and rendering

    private PopupPlacement _resolvedPlacement = PopupPlacement.Below;

    /// <inheritdoc/>
    /// <inheritdoc/>
    protected override Rect VisualBounds
    {
        get
        {
            if (!IsOpen)
            {
                return default;
            }

            var shadow = ActualShadow;
            return SurfaceBounds.ExpandVisualBounds(shadow.IsVisible, shadow.Mode, shadow.Offset);
        }
    }

    /// <inheritdoc/>
    protected override ChromeRenderOptions GetChromeRenderOptions() => new()
    {
        BodyBounds = SurfaceBounds,
        ShadowExcludeBounds = SurfaceBounds,
        SkipBodyFill = true,
        SkipBorder = true
    };

    /// <inheritdoc/>
    // Popup Bounds describe the root placement plane. Its themed border belongs
    // to SurfaceBounds and is reserved by SurfaceFrame after placement resolves.
    private protected override Thickness BorderInset => default;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        if (Content is not { } child)
        {
            return default;
        }

        var frame = SurfaceFrame(Placement);
        _ = MeasureChild(
            child,
            new Constraint(
                constraint.Width.Subtract(frame.Horizontal),
                constraint.Height.Subtract(frame.Vertical)));
        return IsOpen
            ? SurfaceSize(child, anchorWidth: 0, constraint.Width, constraint.Height, frame)
            : default;
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (Content is not { } child)
        {
            SurfaceBounds = default;
            return;
        }

        if (!IsOpen)
        {
            ArrangeChild(child, bounds, ResolvedAxes.Both);
            SurfaceBounds = default;
            return;
        }

        var anchor = Anchor?.Bounds ?? bounds;
        var preferredFrame = SurfaceFrame(Placement);
        var anchorWidth = FixedOrigin is not null && Anchor is null ? 0 : anchor.Width;
        var desired = SurfaceSize(child, anchorWidth, bounds.Width, bounds.Height, preferredFrame);
        int x, y;
        Thickness resolvedFrame;

        if (FixedOrigin is { } origin)
        {
            x = origin.X;
            y = origin.Y;
            _resolvedPlacement = Placement;
            resolvedFrame = preferredFrame;
        }
        else
        {
            var placement = ConstrainToRoot ? ResolvePlacement(bounds, anchor, desired) : Placement;
            _resolvedPlacement = placement;
            resolvedFrame = SurfaceFrame(placement);

            // A flip to the opposite side can reserve a different amount of frame space than the
            // preferred placement did (an asymmetric border plus ConnectsToAnchor zeroes a different
            // edge per side). Recompute against the frame that is actually used below, so the origin
            // math, SurfaceBounds, and the deflated arrange rect all agree on the same size.
            desired = SurfaceSize(child, anchorWidth, bounds.Width, bounds.Height, resolvedFrame);

            x = placement is PopupPlacement.Left
                ? anchor.X - desired.Width
                : placement is PopupPlacement.Right
                    ? anchor.Right
                    : anchor.X;
            y = placement is PopupPlacement.Above
                ? anchor.Y - desired.Height
                : placement is PopupPlacement.Below
                    ? anchor.Bottom
                    : anchor.Y;
        }

        if (ConstrainToRoot)
        {
            x = Math.Clamp(x, bounds.X, Math.Max(bounds.X, bounds.Right - desired.Width));
            y = Math.Clamp(y, bounds.Y, Math.Max(bounds.Y, bounds.Bottom - desired.Height));
        }

        SurfaceBounds = new Rect(x, y, desired.Width, desired.Height);
        ArrangeChild(child, resolvedFrame.Deflate(SurfaceBounds), ResolvedAxes.Both);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (!IsOpen || SurfaceBounds.Width == 0 || SurfaceBounds.Height == 0)
        {
            return;
        }

        var style = GetResolvedStyle(VisualState.Normal);
        canvas.Clear(SurfaceBounds, style);
    }

    /// <inheritdoc/>
    internal override void RenderOverlay(TerminalCanvas canvas)
    {
        if (IsOpen && SurfaceBounds.Width > 0 && SurfaceBounds.Height > 0)
        {
            DrawFrame(canvas, GetResolvedAppearance(VisualState.Normal).BorderStyle);
        }
    }

    /// <inheritdoc/>
    internal override void RenderChildren(TerminalCanvas canvas, Rect contentClip)
    {
        if (IsOpen)
        {
            base.RenderChildren(canvas, contentClip);
        }
    }

    /// <inheritdoc/>
    internal override void RenderPopupLayer(TerminalCanvas canvas)
    {
        if (IsOpen)
        {
            Render(canvas);
        }
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (IsOpen && CloseOnEscape && eventArgs is KeyEventArgs
            {
                IsInitialKeyDown: true,
                Stroke.Code: Code.Escape,
                Stroke.Modifiers: var modifiers
            } && modifiers.IsActivationEligible())
        {
            IsOpen = false;
            eventArgs.IsHandled = true;
        }
    }

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        ReconcilePresentationAvailability();
    }

    /// <inheritdoc/>
    protected override void OnParentChanged(ControlBase? previous, ControlBase? current)
    {
        base.OnParentChanged(previous, current);

        if (current is null)
        {
            ClearAvailabilityAncestor();
        }
        else if (_availabilityAncestor is not null)
        {
            TrackUnavailableAncestor();
        }
    }

    /// <inheritdoc/>
    protected override void OnDetached()
    {
        ClearAvailabilityAncestor();
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(base.OnDetached, ref failure);

        if (IsOpen)
        {
            ExceptionAggregation.Capture(CommitClosedState, ref failure);
            ExceptionAggregation.Capture(CollapseContent, ref failure);
        }

        // A descendant of a removed subtree root receives OnDetached but never its own OnUnavailable
        // call (OwnedControlRegistry.Commit notifies unavailability only on removed roots), so the base
        // Detached release never runs for it. Release presentation here too so a reattached descendant
        // surface can reopen instead of permanently failing FloatingSurfaceBase's already-open guard.
        ExceptionAggregation.Capture(ReleasePresentation, ref failure);

        failure?.Throw();
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        if (reason == ReleaseReason.Disposed)
        {
            PropertyChanged -= OnPopupPropertyChanged;
        }

        if (reason is ReleaseReason.Hidden or ReleaseReason.Detached or ReleaseReason.Disposed)
        {
            ClearAvailabilityAncestor();
        }

        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(() => base.OnUnavailable(reason), ref failure);

        // Disposal joins Hidden/Detached here rather than only clearing the backing
        // field: a disposed-while-open popup (for example, a whole owner control
        // disposing with its context menu still open) must still raise the IsOpen
        // transition so subscribers — light-dismiss handlers chief among them —
        // can release resources they attached to an unrelated root control.
        if (reason is ReleaseReason.Hidden or ReleaseReason.Detached or ReleaseReason.Disposed && IsOpen)
        {
            ExceptionAggregation.Capture(CommitClosedState, ref failure);
            ExceptionAggregation.Capture(CollapseContent, ref failure);
        }

        failure?.Throw();
    }

    private void OnPopupPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName is nameof(Visibility) or nameof(IsEnabled))
        {
            ReconcilePresentationAvailability();
        }
        else if (eventArgs.PropertyName == nameof(Anchor) && IsOpen)
        {
            SubscribeAnchorReflow();
        }
    }

    private void ReconcilePresentationAvailability()
    {
        if (!IsOpen || Dispatcher is null || !EffectiveIsVisible || !EffectiveIsEnabled)
        {
            return;
        }

        if (!IsSurfacePresented)
        {
            PresentOpen(suppressFocusOnOpen: false, notifyOpenState: false);
        }

        if (ModalBehavior == PopupModalBehavior.Auto &&
            !HasActiveSurfaceModal &&
            ModalityOwner is not null)
        {
            _ = OpenModalCore(OutsideInteraction.Dismiss, initialFocus: null, exposed: false);
        }
    }

    private void TrackUnavailableAncestor()
    {
        var unavailable = FindUnavailableAncestor();

        if (ReferenceEquals(_availabilityAncestor, unavailable))
        {
            return;
        }

        ClearAvailabilityAncestor();

        if (unavailable is not null)
        {
            _availabilityAncestor = unavailable;
            unavailable.PropertyChanged += OnAvailabilityAncestorPropertyChanged;
            return;
        }

        ReconcilePresentationAvailability();
    }

    [Pure]
    private ControlBase? FindUnavailableAncestor()
    {
        for (var current = Parent; current is not null; current = current.Parent)
        {
            if (current.Visibility != Visibility.Visible || !current.IsEnabled)
            {
                return current;
            }
        }

        return null;
    }

    private void OnAvailabilityAncestorPropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName is nameof(Visibility) or nameof(IsEnabled))
        {
            TrackUnavailableAncestor();
        }
    }

    private void ClearAvailabilityAncestor()
    {
        if (_availabilityAncestor is not { } ancestor)
        {
            return;
        }

        _availabilityAncestor = null;
        ancestor.PropertyChanged -= OnAvailabilityAncestorPropertyChanged;
    }

    private void SetOpen(bool value, bool suppressFocusOnOpen)
    {
        VerifyMutable();

        if (_isOpenTransitioning)
        {
            throw new InvalidOperationException("Popup open-state transitions cannot be reentered.");
        }

        if (_isOpen == value)
        {
            return;
        }

        _isOpenTransitioning = true;
        try
        {
            if (value)
            {
                if (Dispatcher is null)
                {
                    CommitOpenState(suppressFocusOnOpen, notifyOpenState: true);
                }
                else
                {
                    PresentOpen(suppressFocusOnOpen, notifyOpenState: true);
                }
            }
            else if (IsSurfacePresented)
            {
                ClearAvailabilityAncestor();
                _ = CloseSurface(CommitClosedState, CollapseContent);
            }
            else
            {
                ClearAvailabilityAncestor();
                CloseUnpresented();
            }
        }
        finally
        {
            _isOpenTransitioning = false;
        }
    }

    private void PresentOpen(bool suppressFocusOnOpen, bool notifyOpenState)
    {
        try
        {
            OpenSurface(() => CommitOpenState(suppressFocusOnOpen, notifyOpenState));
        }
        catch (Exception exception)
        {
            var failure = ExceptionDispatchInfo.Capture(exception);

            if (_isOpen)
            {
                _isOpen = false;
                ExceptionAggregation.Capture(
                    () => NotifyPropertyChanged(nameof(IsOpen), InvalidationImpact.Measure),
                    ref failure);
            }

            ExceptionAggregation.Capture(CollapseContent, ref failure);
            failure!.Throw();
        }
    }

    private void CommitOpenState(bool suppressFocusOnOpen, bool notifyOpenState)
    {
        ExceptionDispatchInfo? failure = null;

        if (notifyOpenState)
        {
            _isOpen = true;
            ExceptionAggregation.Capture(
                () => NotifyPropertyChanged(nameof(IsOpen), InvalidationImpact.Measure),
                ref failure);
        }

        if (!SuppressCloseOtherPopups)
        {
            ExceptionAggregation.Capture(() => CloseOtherPopups(this), ref failure);
        }

        ExceptionAggregation.Capture(() => MakeContentAvailable(suppressFocusOnOpen), ref failure);
        ExceptionAggregation.Capture(OnContentAvailable, ref failure);

        if (failure is null)
        {
            return;
        }

        if (notifyOpenState)
        {
            _isOpen = false;
            ExceptionAggregation.Capture(
                () => NotifyPropertyChanged(nameof(IsOpen), InvalidationImpact.Measure),
                ref failure);
        }

        ExceptionAggregation.Capture(CollapseContent, ref failure);
        failure!.Throw();
    }

    private void MakeContentAvailable(bool suppressFocusOnOpen)
    {
        ExceptionDispatchInfo? failure = null;

        if (Content is { } child)
        {
            ExceptionAggregation.Capture(() => child.Visibility = Visibility.Visible, ref failure);
        }

        if (!suppressFocusOnOpen &&
            FocusOnOpen &&
            Content is { } focusableChild &&
            FindFocusable(focusableChild) is { } target)
        {
            ExceptionAggregation.Capture(() => _ = FocusOwner?.Focus(target), ref failure);
        }

        failure?.Throw();
    }

    /// <summary>Completes family-specific setup after current content becomes available during opening.</summary>
    /// <remarks>
    /// Failures participate in the Popup opening transaction: all opening stages complete, family state
    /// rolls back, and the earliest failure is rethrown.
    /// </remarks>
    /// <exception cref="Exception">Family-specific post-content setup fails.</exception>
    internal virtual void OnContentAvailable() => SubscribeAnchorReflow();

    /// <summary>Responds to the foreign anchor reflowing while this popup is open. The default
    /// re-resolves placement against the anchor's new position, line-for-line what a Tooltip's
    /// own layout pass already did before this tracking moved here. A family with different needs
    /// (a Flyout dismissing instead of following) overrides this instead of subscribing itself.</summary>
    /// <remarks>
    /// InvalidateSelf here is scoped to this control rather than a full-tree Invalidate: only this
    /// popup's own Measure/Arrange short-circuit needs bypassing (the anchor moved, not any of
    /// this popup's own content), and the constraint/slot passed to Measure/Arrange below is the
    /// synchronous follow-up InvalidateSelf's contract requires — leaving it pending, or invoking
    /// it without immediately completing Measure/Arrange, strands the dirty bit until some
    /// unrelated pass happens to visit this popup again.
    /// </remarks>
    internal virtual void OnAnchorReflow()
    {
        var root = RootBounds(default);

        if (root.Width == 0 || root.Height == 0)
        {
            return;
        }

        InvalidateSelf(Invalidation.Measure);
        Measure(new Constraint(root.Width, root.Height));
        Arrange(root, widthResolved: true, heightResolved: true);
    }

    /// <summary>Starts reacting to the current Anchor's own reflow while this popup is open, so a
    /// foreign sibling growing, shrinking, or moving elsewhere re-resolves placement instead of
    /// leaving it pinned to wherever the anchor was when the popup opened.</summary>
    private void SubscribeAnchorReflow()
    {
        // Anchor cleared while open (or tracking turned off) must still drop a live
        // subscription - falling through to the reference-equality check below would leave
        // this popup reacting to a control it no longer considers its anchor.
        if (!TracksAnchorReflow || Anchor is not { } anchor)
        {
            UnsubscribeAnchorReflow();
            return;
        }

        if (ReferenceEquals(_subscribedReflowAnchor, anchor))
        {
            return;
        }

        UnsubscribeAnchorReflow();
        _subscribedReflowAnchor = anchor;
        anchor.BoundsChanged += OnAnchorReflowBoundsChanged;
    }

    private void UnsubscribeAnchorReflow()
    {
        if (_subscribedReflowAnchor is { } anchor)
        {
            anchor.BoundsChanged -= OnAnchorReflowBoundsChanged;
            _subscribedReflowAnchor = null;
        }
    }

    private void OnAnchorReflowBoundsChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        // A reflow reaching here while an open-state transition is still in flight (an Opened
        // handler that itself grows the anchor synchronously) is harmlessly covered by the
        // initial placement Arrange that follows opening - responding here too would reenter
        // family logic (Flyout's IsOpen = false) in the middle of the very IsOpen = true call
        // that is still on the stack, which SetOpen's reentrancy guard rejects.
        if (IsOpen && !_isOpenTransitioning)
        {
            OnAnchorReflow();
        }
    }

    private void CommitClosedState()
    {
        _isOpen = false;
        NotifyPropertyChanged(nameof(IsOpen), InvalidationImpact.Measure);
    }

    private void CollapseContent()
    {
        UnsubscribeAnchorReflow();

        if (Content is { } child)
        {
            child.Visibility = Visibility.Collapsed;
        }
    }

    private void CloseUnpresented()
    {
        // CommitClosedState is the first mutation on this path (SetOpen never assigns
        // _isOpen), so a veto here leaves _isOpen == true untouched - matching the presented
        // path, where CloseSurfaceCore returns before committing.
        if (!RaiseCloseRequested())
        {
            return;
        }

        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(CommitClosedState, ref failure);
        ExceptionAggregation.Capture(RaiseSurfaceClosing, ref failure);
        ExceptionAggregation.Capture(CollapseContent, ref failure);
        SurfaceBounds = default;
        ExceptionAggregation.Capture(RaiseSurfaceClosed, ref failure);
        failure?.Throw();
    }

    private void TrackModalCallbacks(ModalScope scope)
    {
        Debug.Assert(scope is not null, "A Popup observes one concrete modal lifetime.");
        Debug.Assert(scope.IsActive, "A Popup observes only an active modal lifetime.");
        Debug.Assert(_modalCallbackScope is null, "A Popup observes at most one active modal lifetime.");
        _modalCallbackScope = scope;
        scope.DismissRequested += OnModalDismissRequested;
        scope.Exited += OnPopupModalExited;
    }

    private void OnModalDismissRequested(object? sender, EventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is ModalScope { IsActive: true } && IsOpen)
        {
            IsOpen = false;
        }
    }

    private void OnPopupModalExited(object? sender, EventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is ModalScope scope)
        {
            var preserveOpen = ModalityOwner?.IsUnavailable(this) == true;
            _modalCallbackScope = null;
            scope.DismissRequested -= OnModalDismissRequested;
            scope.Exited -= OnPopupModalExited;

            if (!preserveOpen && IsOpen)
            {
                IsOpen = false;
            }
            else if (preserveOpen && IsOpen)
            {
                TrackUnavailableAncestor();
            }
        }
    }

    #endregion

    #region Geometry

    [Pure]
    private static Size SurfaceSize(
        ControlBase child,
        int anchorWidth,
        int? availableWidth,
        int? availableHeight,
        Thickness frame)
    {
        Debug.Assert(anchorWidth >= 0, "Anchor width is non-negative.");

        var contentWidth = child.DesiredSize.Width.Add(child.Margin.Horizontal);
        var contentHeight = child.DesiredSize.Height.Add(child.Margin.Vertical);
        var width = Math.Max(anchorWidth, contentWidth.Add(frame.Horizontal));
        var height = contentHeight.Add(frame.Vertical);

        return new Size(
            availableWidth.HasValue ? Math.Min(width, availableWidth.Value) : width,
            availableHeight.HasValue ? Math.Min(height, availableHeight.Value) : height);
    }

    [Pure]
    private PopupPlacement ResolvePlacement(Rect bounds, Rect anchor, Size desired)
    {
        // Keep the preferred direction when it fits. A terminal-edge popup
        // flips before clamping so its framed surface remains legible.
        return Placement switch
        {
            PopupPlacement.Below when anchor.Bottom + desired.Height > bounds.Bottom &&
                                      anchor.Y - desired.Height >= bounds.Y => PopupPlacement.Above,
            PopupPlacement.Above when anchor.Y - desired.Height < bounds.Y &&
                                      anchor.Bottom + desired.Height <= bounds.Bottom => PopupPlacement.Below,
            PopupPlacement.Right when anchor.Right + desired.Width > bounds.Right &&
                                      anchor.X - desired.Width >= bounds.X => PopupPlacement.Left,
            PopupPlacement.Left when anchor.X - desired.Width < bounds.X &&
                                     anchor.Right + desired.Width <= bounds.Right => PopupPlacement.Right,
            _ => Placement
        };
    }

    [Pure]
    private Thickness SurfaceFrame(PopupPlacement placement)
    {
        var sides = ActualBorder.Sides;
        var frame = new Thickness(
            (sides & BorderSide.Left) != 0 ? 1 : 0,
            (sides & BorderSide.Top) != 0 ? 1 : 0,
            (sides & BorderSide.Right) != 0 ? 1 : 0,
            (sides & BorderSide.Bottom) != 0 ? 1 : 0);
        return !ConnectsToAnchor ? frame : placement switch
        {
            PopupPlacement.Below => new Thickness(frame.Left, 0, frame.Right, frame.Bottom),
            PopupPlacement.Above => new Thickness(frame.Left, frame.Top, frame.Right, 0),
            PopupPlacement.Right => new Thickness(0, frame.Top, frame.Right, frame.Bottom),
            PopupPlacement.Left => new Thickness(frame.Left, frame.Top, 0, frame.Bottom),
            _ => throw new UnreachableException()
        };
    }

    private void DrawFrame(TerminalCanvas canvas, TerminalStyle style)
    {
        var glyphs = ResolveBorderGlyphs(ActualBorder.GlyphStyle);

        // Resolved exactly as the eight border glyphs above are. These four share their frame slots
        // and are drawn by the same loops, so a glyph that measures two cells under Ambiguous.Wide -
        // which all four do - overruns the row unless it degrades the same way.
        var anchors = ResolvedAnchorGlyphs;
        var upArrow = ResolveAnchor(anchors.UpGlyph);
        var downArrow = ResolveAnchor(anchors.DownGlyph);
        var leftArrow = ResolveAnchor(anchors.LeftGlyph);
        var rightArrow = ResolveAnchor(anchors.RightGlyph);
        var frame = SurfaceFrame(_resolvedPlacement);
        var indicatorEdge = ShowAnchorIndicator ? _resolvedPlacement : (PopupPlacement?) null;
        var anchorMid = Anchor is { } anchorCtl
            ? indicatorEdge is PopupPlacement.Below or PopupPlacement.Above
                ? anchorCtl.Bounds.X + (anchorCtl.Bounds.Width / 2)
                : anchorCtl.Bounds.Y + (anchorCtl.Bounds.Height / 2)
            : -1;

        if (frame.Top > 0)
        {
            for (var x = SurfaceBounds.X; x < SurfaceBounds.Right; x++)
            {
                var glyph = x == SurfaceBounds.X && frame.Left > 0
                    ? glyphs.TopLeft
                    : x == SurfaceBounds.Right - 1 && frame.Right > 0
                        ? glyphs.TopRight
                        : indicatorEdge is PopupPlacement.Below && x == anchorMid
                            ? upArrow
                            : glyphs.Top;
                canvas.DrawRune(glyph, new Point(x, SurfaceBounds.Y), style);
            }
        }

        // A one-row complete frame keeps its top edge authoritative. A connected
        // below surface has no top edge, so its bottom edge owns that single row.
        if (frame.Bottom > 0 && (SurfaceBounds.Height > 1 || frame.Top == 0))
        {
            for (var x = SurfaceBounds.X; x < SurfaceBounds.Right; x++)
            {
                var glyph = x == SurfaceBounds.X && frame.Left > 0
                    ? glyphs.BottomLeft
                    : x == SurfaceBounds.Right - 1 && frame.Right > 0
                        ? glyphs.BottomRight
                        : indicatorEdge is PopupPlacement.Above && x == anchorMid
                            ? downArrow
                            : glyphs.Bottom;
                canvas.DrawRune(
                    glyph,
                    new Point(x, SurfaceBounds.Bottom - 1),
                    style);
            }
        }

        for (var y = SurfaceBounds.Y + frame.Top; y < SurfaceBounds.Bottom - frame.Bottom; y++)
        {
            if (frame.Left > 0)
            {
                var glyph = indicatorEdge is PopupPlacement.Right && y == anchorMid
                    ? leftArrow
                    : glyphs.Left;
                canvas.DrawRune(glyph, new Point(SurfaceBounds.X, y), style);
            }

            if (frame.Right > 0 && (SurfaceBounds.Width > 1 || frame.Left == 0))
            {
                var glyph = indicatorEdge is PopupPlacement.Left && y == anchorMid
                    ? rightArrow
                    : glyphs.Right;
                canvas.DrawRune(
                    glyph,
                    new Point(SurfaceBounds.Right - 1, y),
                    style);
            }
        }
    }

    [Pure]
    private static ControlBase? FindFocusable(ControlBase control)
    {
        if (control is { CanFocus: true, EffectiveIsEnabled: true, EffectiveIsVisible: true })
        {
            return control;
        }

        var count = control.OwnedControlCount;

        for (var index = 0; index < count; index++)
        {
            if (FindFocusable(control.OwnedControlAt(index)) is { } result)
            {
                return result;
            }
        }

        return null;
    }

    [Pure]
    private static bool ContainsOpenDescendantSurface(ControlBase owner, Point point)
    {
        var count = owner.OwnedControlCount;

        for (var index = 0; index < count; index++)
        {
            var child = owner.OwnedControlAt(index);

            if (child is Popup { IsOpen: true } popup && popup.SurfaceBounds.Contains(point))
            {
                return true;
            }

            if (ContainsOpenDescendantSurface(child, point))
            {
                return true;
            }
        }

        return false;
    }

    private static void CloseOtherPopups(Popup opening)
    {
        ControlBase root = opening;

        while (root.Parent is { } parent)
        {
            root = parent;
        }

        CloseDescendantPopups(root, opening);
    }

    private static void CloseDescendantPopups(ControlBase control, Popup except)
    {
        var count = control.OwnedControlCount;

        for (var index = 0; index < count; index++)
        {
            var child = control.OwnedControlAt(index);

            if (child is Popup { IsOpen: true } popup && !ReferenceEquals(popup, except) &&
                !IsAncestorOf(popup, except))
            {
                popup.IsOpen = false;
            }

            CloseDescendantPopups(child, except);
        }
    }

    [Pure]
    private static bool IsAncestorOf(ControlBase candidate, ControlBase descendant)
    {
        for (var current = descendant.Parent; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, candidate))
            {
                return true;
            }
        }

        return false;
    }

    #endregion
    // Read from the style set directly rather than through AppearanceStates: flattening a style to
    // AppearanceStates keeps only the appearance members and drops everything else, so a control
    // that needs a glyph member has to resolve its own style.
    [Pure]
    private Rune ResolveAnchor(ControlGlyph themed) =>
        themed.Value.Resolve(themed.Fallback, CellPolicy.AmbiguousWidth);

    /// <summary>Gets the resolved anchor family and records its render dependency for Theme swaps.</summary>
    /// <remarks>Internal so tests can prove the dependency is registered at the resolver boundary.</remarks>
    internal PopupAnchorGlyphs ResolvedAnchorGlyphs
    {
        get
        {
            TrackThemeStructuralDependency(ThemeStructuralDependency.PopupAnchorGlyphs);
            return (Theme ?? ThemeCatalog.Dark).GetStyleSet(PopupStyle.Default).Normal.AnchorGlyphs;
        }
    }

}
