// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Popups;

using System.Runtime.ExceptionServices;

using SharpVision.Surfaces;
using SharpVision.Terminal.Input;

/// <summary>Displays one owned content control on an opaque, framed, anchor-relative modal surface.</summary>
[PublicAPI]
public class Popup: FloatingSurface
{
    /// <summary>Gets or sets the complete locally authored border.</summary>
    public new Border Border { get => base.Border; set => base.Border = value; }

    /// <summary>Returns border ownership to the active Theme.</summary>
    public new void ResetBorder() => base.ResetBorder();

    /// <summary>Gets or sets the complete locally authored shadow.</summary>
    public new Shadow Shadow { get => base.Shadow; set => base.Shadow = value; }

    /// <summary>Returns shadow ownership to the active Theme.</summary>
    public new void ResetShadow() => base.ResetShadow();

    #region Construction and ownership

    /// <inheritdoc/>
    protected override ThemeRole ThemeRole => ThemeRole.Popup;

    /// <summary>Initializes a closed themed surface below its eventual anchor.</summary>
    public Popup()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Focusable = false;
        PropertyChanged += OnPopupPropertyChanged;
    }

    /// <inheritdoc/>
    protected override void OnContentChanged(Control? previous, Control? current)
    {
        base.OnContentChanged(previous, current);

        _ = current?.Visibility = IsOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Gets or sets the optional sibling anchor used to place the open surface.</summary>
    /// <exception cref="InvalidOperationException">The attached popup is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The popup is disposed.</exception>
    public Control? Anchor
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
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The popup placement is unknown.");
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Arrange);
        }
    } = PopupPlacement.Below;

    #endregion

    #region Surface appearance

    /// <summary>Gets or sets whether the surface omits the frame edge adjoining its anchor.</summary>
    public bool ConnectsToAnchor { get; init; }

    /// <summary>Gets or sets whether placement flips and clamps inside the owning root.</summary>
    internal bool ConstrainToRoot { get; set; } = true;

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
    private Control? _availabilityAncestor;
    private ModalScope? _modalCallbackScope;

    /// <summary>Gets or sets whether this Popup self-manages its modal scope on open.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached popup is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The popup is disposed.</exception>
    public PopupModalBehavior ModalBehavior
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The popup modal behavior is unknown.");
            }

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
    /// <see cref="FloatingSurface.Closing"/> after this property is false:
    /// current content retains its pre-close availability and the previous <see cref="FloatingSurface.SurfaceBounds"/>
    /// remains readable, while the surface is already ineligible for rendering and hit testing. The
    /// transition raises <see cref="FloatingSurface.Closing"/> while the automatic modal scope is still active, then exits
    /// modality, collapses current content, clears the surface bounds, and raises
    /// <see cref="FloatingSurface.Closed"/>. Every stage completes when a callback fails, after which the earliest
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
    /// modeless accidentally. Closing the popup ordinarily raises <see cref="FloatingSurface.Closing"/>,
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
    public ModalScope OpenModal(
        OutsideInteraction outsideInteraction = OutsideInteraction.Dismiss,
        Control? initialFocus = null)
    {
        VerifyMutable();

        if (_isOpeningModal)
        {
            throw new InvalidOperationException("Popup modal presentations cannot be reentered.");
        }

        _isOpeningModal = true;

        try
        {
            if (!Enum.IsDefined(outsideInteraction))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(outsideInteraction),
                    outsideInteraction,
                    "The outside-interaction policy is unknown.");
            }

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
        Control? initialFocus,
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
    internal override Control? HitTest(Point point)
    {
        return !IsOpen || IsDisposed || !IsHitTestVisible || !EffectiveIsVisible || !EffectiveIsEnabled
            ? null
            : Content?.HitTest(point) ?? (SurfaceBounds.Contains(point) ? this : null);
    }

    /// <inheritdoc/>
    internal override Control? HitTestPopupCore(Point point) => IsOpen ? HitTest(point) : null;

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
            return ControlChrome.ExpandVisualBounds(SurfaceBounds, shadow.IsVisible, shadow.Mode, shadow.Offset);
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
                Stroke.Code: Code.Escape, Stroke.Action: KeyAction.Press
            })
        {
            IsOpen = false;
            eventArgs.Handled = true;
        }
    }

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        ReconcilePresentationAvailability();
    }

    /// <inheritdoc/>
    protected override void OnParentChanged(Control? previous, Control? current)
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
        // surface can reopen instead of permanently failing FloatingSurface's already-open guard.
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

    private Control? FindUnavailableAncestor()
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
    internal virtual void OnContentAvailable()
    {
    }

    private void CommitClosedState()
    {
        _isOpen = false;
        NotifyPropertyChanged(nameof(IsOpen), InvalidationImpact.Measure);
    }

    private void CollapseContent()
    {
        if (Content is { } child)
        {
            child.Visibility = Visibility.Collapsed;
        }
    }

    private void CloseUnpresented()
    {
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

    private static Size SurfaceSize(
        Control child,
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
                            ? new Rune('▲')
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
                            ? new Rune('▼')
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
                    ? new Rune('◀')
                    : glyphs.Left;
                canvas.DrawRune(glyph, new Point(SurfaceBounds.X, y), style);
            }

            if (frame.Right > 0 && (SurfaceBounds.Width > 1 || frame.Left == 0))
            {
                var glyph = indicatorEdge is PopupPlacement.Left && y == anchorMid
                    ? new Rune('▶')
                    : glyphs.Right;
                canvas.DrawRune(
                    glyph,
                    new Point(SurfaceBounds.Right - 1, y),
                    style);
            }
        }
    }

    private static Control? FindFocusable(Control control)
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

    private static bool ContainsOpenDescendantSurface(Control owner, Point point)
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
        Control root = opening;

        while (root.Parent is { } parent)
        {
            root = parent;
        }

        CloseDescendantPopups(root, opening);
    }

    private static void CloseDescendantPopups(Control control, Popup except)
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

    private static bool IsAncestorOf(Control candidate, Control descendant)
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
}
