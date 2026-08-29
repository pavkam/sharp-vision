// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Notifications;

using System.Runtime.ExceptionServices;

using SharpVision.Controls.Layout;
using SharpVision.Surfaces;
using SharpVision.Terminal.Input;

/// <summary>Displays one retained content control as a non-modal transient notification surface.</summary>
/// <remarks>
/// A Toast is the directly mounted floating surface. Showing never creates a proxy control and
/// never enters modality or takes focus. The caller owns the Toast and its retained content.
/// </remarks>
[PublicAPI]
public sealed class Toast: FloatingSurfaceBase, IStyled<ToastStyle>, IOverlayPositionConstraint
{
    private static readonly TimeSpan _animationRefreshInterval = TimeSpan.FromMilliseconds(16);

    private readonly PressBehavior _closeInteraction;
    private readonly StyleSlot<ToastStyle> _style;
    private DispatcherTimer? _animationTimer;
    private ToastAnimationState? _animationState;
    private ToastCoordinator? _coordinator;
    private DispatcherTimer? _displayTimer;
    private Dispatcher? _removalDispatcher;
    private ToastCoordinator? _pendingRemovalCoordinator;

    #region Construction and appearance

    /// <summary>Initializes a closed, focusable, informational Toast with default timing.</summary>
    public Toast()
    {
        _style = InitializeStyle(ToastStyle.Definition);
        _closeInteraction = new PressBehavior(
            ResolveCloseTargetBounds,
            () => !IsDisposed && IsDismissible && IsOpen && EffectiveIsEnabled && EffectiveIsVisible &&
                ResolveCloseTargetBounds().Width > 0,
            static () => true,
            RequestFocus,
            CapturePointer,
            () => HasPointerCapture,
            ReleasePointerCapture,
            SetPressed,
            _ => Dismiss(),
            () => Capabilities.KeyReleaseEvents.Authoritative);
        RegisterLifecycleParticipant(_closeInteraction);
        IsFocusable = true;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
    }

    /// <summary>Gets or sets the complete local Toast presentation, or null for Popup-theme fallback.</summary>
    /// <exception cref="InvalidOperationException">The attached Toast is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Toast is disposed.</exception>
    public ToastStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the resolved complete Toast presentation.</summary>
    public ToastStyle ActualStyle => _style.Actual;

    #endregion

    #region Content and presentation options

    /// <summary>Gets or sets the optional single-line title rendered above content.</summary>
    /// <exception cref="ArgumentException">The value contains a control grapheme.</exception>
    /// <exception cref="InvalidOperationException">The attached Toast is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Toast is disposed.</exception>
    public string? Title
    {
        get;
        set
        {
            if (value is not null &&
                Terminal.Unicode.Width.Measure(value, Ambiguous.Narrow).Controls != 0)
            {
                throw new ArgumentException("A Toast title cannot contain control graphemes.", nameof(value));
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets or sets the optional grapheme adornment rendered before the title.</summary>
    /// <exception cref="InvalidOperationException">The attached Toast is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Toast is disposed.</exception>
    public Affix? Adornment
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    }

    /// <summary>Gets or sets the screen-edge stack receiving this Toast.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is undefined.</exception>
    /// <exception cref="InvalidOperationException">The Toast is open or is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Toast is disposed.</exception>
    public ToastPosition Position
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The Toast position is unknown.");
            VerifyClosedMutation();
            _ = SetProperty(ref field, value, InvalidationImpact.Arrange);
        }
    } = ToastPosition.TopRight;

    /// <summary>Gets or sets the deterministic entrance animation.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is undefined.</exception>
    /// <exception cref="InvalidOperationException">The Toast is open or is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Toast is disposed.</exception>
    public ToastAnimation Animation
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The Toast animation is unknown.");
            VerifyClosedMutation();
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = ToastAnimation.Fade;

    /// <summary>Gets or sets the entrance duration; zero completes synchronously.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or exceeds timer limits.</exception>
    /// <exception cref="InvalidOperationException">The Toast is open or is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Toast is disposed.</exception>
    public TimeSpan AnimationDuration
    {
        get;
        set
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Animation duration cannot be negative.");
            }

            if (value > TimeSpan.Zero)
            {
                DispatcherTimer.ValidateInterval(value, nameof(value));
            }

            VerifyClosedMutation();
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = TimeSpan.FromMilliseconds(200);

    /// <summary>Gets or sets the visible lifetime after entrance completes.</summary>
    /// <remarks>
    /// <see cref="Timeout.InfiniteTimeSpan"/> keeps the Toast open until dismissed. Cancelling a
    /// timeout-driven close request leaves this interval active for a later retry.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is neither infinite nor a valid positive timer interval.</exception>
    /// <exception cref="InvalidOperationException">The Toast is open or is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Toast is disposed.</exception>
    public TimeSpan DisplayDuration
    {
        get;
        set
        {
            if (value != Timeout.InfiniteTimeSpan)
            {
                DispatcherTimer.ValidateInterval(value, nameof(value));
            }

            VerifyClosedMutation();
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets whether keyboard and pointer input may request dismissal.</summary>
    /// <exception cref="InvalidOperationException">The attached Toast is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Toast is disposed.</exception>
    public bool IsDismissible
    {
        get;
        set
        {
            var wasDismissible = field;
            ExceptionDispatchInfo? failure = null;
            ExceptionAggregation.Capture(
                () => _ = SetProperty(ref field, value, InvalidationImpact.Measure),
                ref failure);

            if (!wasDismissible || field)
            {
                failure?.Throw();
                return;
            }

            ExceptionAggregation.Capture(_closeInteraction.Unavailable, ref failure);
            ExceptionAggregation.Capture(
                () =>
                {
                    if (HasPointerCapture)
                    {
                        ReleasePointerCapture();
                    }
                },
                ref failure);
            failure?.Throw();
        }
    } = true;

    /// <summary>Gets whether this Toast is currently presented.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Gets normalized entrance progress from zero through one.</summary>
    public double AnimationProgress { get; private set; }

    #endregion

    #region Lifecycle

    /// <summary>Shows this Toast above the presentation plane owning the supplied control.</summary>
    /// <param name="owner">The non-null attached control whose ancestry resolves the host.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    /// <exception cref="ArgumentException">The owner has no presentation host.</exception>
    /// <exception cref="InvalidOperationException">The Toast is already open, already owned, detached after mounting, or accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Toast or owner is disposed.</exception>
    /// <exception cref="Exception">An opening callback fails after presentation rollback completes.</exception>
    public void Show(ControlBase owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        VerifyClosedMutation();
        CompletePendingRemoval();
        var coordinator = ToastCoordinator.Present(owner, this);
        _coordinator = coordinator;

        try
        {
            OpenSurface(
                () =>
                {
                    IsOpen = true;
                    AnimationProgress = AnimationDuration == TimeSpan.Zero ? 1 : 0;
                    NotifyPropertyChanged(nameof(IsOpen), InvalidationImpact.Measure);
                    NotifyPropertyChanged(nameof(AnimationProgress), InvalidationImpact.Render);
                });

            if (IsOpen && Dispatcher is not null)
            {
                StartPresentationTimers();
            }
        }
        catch (Exception exception)
        {
            var failure = ExceptionDispatchInfo.Capture(exception);
            ExceptionAggregation.Capture(ReleasePresentation, ref failure);

            if (IsOpen)
            {
                IsOpen = false;
                AnimationProgress = 0;
                ExceptionAggregation.Capture(
                    () => NotifyPropertyChanged(nameof(IsOpen), InvalidationImpact.None),
                    ref failure);
                ExceptionAggregation.Capture(
                    () => NotifyPropertyChanged(nameof(AnimationProgress), InvalidationImpact.None),
                    ref failure);
            }

            _coordinator = null;
            ExceptionAggregation.Capture(() => coordinator.Remove(this), ref failure);
            failure!.Throw();
        }
    }

    /// <summary>Requests dismissal; a closed Toast is unchanged.</summary>
    /// <exception cref="InvalidOperationException">The attached Toast is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Toast is disposed.</exception>
    /// <exception cref="Exception">A close callback fails.</exception>
    public void Dismiss()
    {
        VerifyMutable();

        if (!IsOpen)
        {
            return;
        }

        var coordinator = _coordinator;

        try
        {
            _ = CloseSurface(
                () =>
                {
                    DisposeTimers();
                    IsOpen = false;
                    AnimationProgress = 0;
                    NotifyPropertyChanged(nameof(IsOpen), InvalidationImpact.None);
                    NotifyPropertyChanged(nameof(AnimationProgress), InvalidationImpact.None);
                },
                () =>
                {
                    _coordinator = null;
                    coordinator?.Remove(this);
                });
        }
        finally
        {
            if (!IsOpen)
            {
                _coordinator = null;
                coordinator?.Remove(this);
            }
        }
    }

    #endregion

    #region Layout

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var style = ActualStyle;
        var titleWidth = string.IsNullOrEmpty(Title)
            ? 0
            : Title.Measure(CellPolicy.AmbiguousWidth, useMnemonic: false);
        var adornmentWidth = Adornment is null
            ? 0
            : MeasureAffixes(Adornment, null, style.AdornmentGap).StartCells;
        var closeWidth = IsDismissible ? 1 : 0;
        var headerWidth = titleWidth.Add(adornmentWidth).Add(closeWidth);
        var headerHeight = titleWidth == 0 && adornmentWidth == 0 && closeWidth == 0 ? 0 : 1;
        var child = Content;
        var contentWidth = 0;
        var contentHeight = 0;

        if (child is not null)
        {
            var horizontal = style.Padding.Horizontal;
            var vertical = style.Padding.Vertical.Add(headerHeight == 0 ? 0 : headerHeight.Add(style.ContentGap));
            var desired = MeasureChild(
                child,
                new Constraint(
                    constraint.Width.Subtract(horizontal),
                    constraint.Height.Subtract(vertical)));

            if (child.Visibility != Visibility.Collapsed)
            {
                contentWidth = desired.Width.Add(child.Margin.Horizontal);
                contentHeight = desired.Height.Add(child.Margin.Vertical);
            }
        }

        var gap = headerHeight != 0 && contentHeight != 0 ? style.ContentGap : 0;
        return new Size(
            Math.Max(headerWidth, contentWidth).Add(style.Padding.Horizontal),
            headerHeight.Add(gap).Add(contentHeight).Add(style.Padding.Vertical));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (Content is { } content)
        {
            var style = ActualStyle;
            var contentBounds = style.Padding.Deflate(bounds);
            var hasHeader = !string.IsNullOrEmpty(Title) || Adornment is not null || IsDismissible;
            if (hasHeader)
            {
                contentBounds = new Rect(
                    contentBounds.X,
                    Math.Min(contentBounds.Bottom, contentBounds.Y.Add(1).Add(style.ContentGap)),
                    contentBounds.Width,
                    Math.Max(0, contentBounds.Height - 1 - style.ContentGap));
            }

            ArrangeChild(content, contentBounds, ResolvedAxes.Both);
        }

        if (IsOpen)
        {
            SurfaceBounds = Bounds;
        }
    }

    /// <inheritdoc/>
    [Pure]
    Rect IOverlayPositionConstraint.ConstrainOverlaySlot(Rect slot, Rect contentBounds) =>
        _coordinator?.Constrain(this, contentBounds) ?? slot;

    #endregion

    #region Rendering

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        base.OnRenderContent(canvas);

        if (Animation == ToastAnimation.Fade && IsOpen && AnimationProgress == 0)
        {
            return;
        }

        var header = ResolveHeaderBounds();
        if (header.Width == 0 || header.Height == 0)
        {
            return;
        }

        var style = ActualStyle;
        var close = ResolveCloseTargetBounds();
        var affixBox = close.Width == 0
            ? header
            : new Rect(header.X, header.Y, Math.Max(0, close.X - header.X), header.Height);
        var affixes = MeasureAffixes(Adornment, null, style.AdornmentGap);
        var adornmentStyle = ResolvedStyle.WithForeground(ResolveColor(style.AdornmentColor));
        RenderAffixes(canvas, affixBox, affixes, Adornment, null, adornmentStyle);

        if (!string.IsNullOrEmpty(Title))
        {
            var titleBounds = DeflateForAffixes(affixBox, affixes);
            var titleStyle = ResolveFaceStyle(style.TitleFace);
            _ = canvas.Clip(titleBounds).Draw(
                Title.AsSpan(),
                new Point(titleBounds.X, titleBounds.Y),
                titleStyle,
                background: BackgroundMode.Transparent);
        }

        if (close.Width != 0)
        {
            var closeStyle = ResolvedStyle.WithForeground(ResolveColor(style.CloseColor));
            canvas.DrawRune(
                ResolveControlGlyph(style.CloseGlyph),
                new Point(close.X, close.Y),
                closeStyle,
                BackgroundMode.Transparent);
        }
    }

    /// <inheritdoc/>
    protected override ChromeRenderOptions GetChromeRenderOptions() =>
        Animation == ToastAnimation.Fade && IsOpen && AnimationProgress < 1
            ? new ChromeRenderOptions { SkipBodyFill = true, SkipBorder = true, SkipShadow = true }
            : default;

    /// <inheritdoc/>
    internal override void RenderChildren(TerminalCanvas canvas, Rect contentClip)
    {
        if (Animation != ToastAnimation.Fade || AnimationProgress > 0)
        {
            base.RenderChildren(canvas, contentClip);
        }
    }

    /// <inheritdoc/>
    protected override void OnRenderAdornment(TerminalCanvas canvas)
    {
        base.OnRenderAdornment(canvas);

        if (Animation != ToastAnimation.Fade || !IsOpen || AnimationProgress >= 1)
        {
            return;
        }

        if (AnimationProgress == 0)
        {
            return;
        }

        this.RenderBorder(canvas, GetAppearanceState());

        if (!canvas.HasPreviousFrame)
        {
            return;
        }

        var area = Math.Max(1L, (long) Bounds.Width * Bounds.Height);
        var threshold = (long) Math.Floor(AnimationProgress * area);

        for (var y = Bounds.Y; y < Bounds.Bottom; y++)
        {
            for (var x = Bounds.X; x < Bounds.Right; x++)
            {
                var relativeX = x - Bounds.X;
                var relativeY = y - Bounds.Y;
                var ordinal = (((long) relativeX * 37) + ((long) relativeY * 17)) % area;

                if (ordinal >= threshold)
                {
                    canvas.CopyFromPrevious(new Rect(x, y, 1, 1));
                }
            }
        }
    }

    #endregion

    #region Input and availability

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        base.OnEvent(eventArgs);

        if (eventArgs.IsHandled || !IsDismissible)
        {
            return;
        }

        if (eventArgs is KeyEventArgs
            {
                Stroke.Action: KeyAction.Press,
                Stroke.Code: Code.Escape,
                Stroke.Modifiers: var modifiers
            } && modifiers.IsActivationEligible())
        {
            eventArgs.IsHandled = true;
            Dismiss();
            return;
        }

        _closeInteraction.Handle(eventArgs);
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        ExceptionDispatchInfo? failure = null;
        ToastCoordinator? hiddenCoordinator = null;
        var publishClosedState = false;

        if (reason is ReleaseReason.Detached or ReleaseReason.Hidden or ReleaseReason.Disposed)
        {
            ExceptionAggregation.Capture(DisposeTimers, ref failure);
            var coordinator = _coordinator;
            _coordinator = null;
            hiddenCoordinator = reason == ReleaseReason.Hidden ? coordinator : null;
            ExceptionAggregation.Capture(() => coordinator?.Forget(this), ref failure);

            if (reason is ReleaseReason.Detached or ReleaseReason.Disposed)
            {
                CancelPendingRemoval();
                _pendingRemovalCoordinator = null;
            }

            if (IsOpen)
            {
                IsOpen = false;
                AnimationProgress = 0;
                publishClosedState = true;
            }
        }

        ExceptionAggregation.Capture(() => base.OnUnavailable(reason), ref failure);

        if (hiddenCoordinator is not null)
        {
            SchedulePendingRemoval(hiddenCoordinator);
        }

        if (publishClosedState)
        {
            ExceptionAggregation.Capture(
                () => NotifyPropertyChanged(nameof(IsOpen), InvalidationImpact.None),
                ref failure);
            ExceptionAggregation.Capture(
                () => NotifyPropertyChanged(nameof(AnimationProgress), InvalidationImpact.None),
                ref failure);
        }

        failure?.Throw();
    }

    private void SchedulePendingRemoval(ToastCoordinator coordinator)
    {
        Debug.Assert(coordinator is not null, "Deferred Toast removal requires its presentation coordinator.");
        var dispatcher = Dispatcher;
        _pendingRemovalCoordinator = coordinator;

        if (dispatcher is null || ReferenceEquals(_removalDispatcher, dispatcher))
        {
            return;
        }

        CancelPendingRemoval();
        _removalDispatcher = dispatcher;
        dispatcher.Idle += OnRemovalDispatcherIdle;
        dispatcher.RequestIdle();
    }

    private void CancelPendingRemoval()
    {
        if (_removalDispatcher is not { } dispatcher)
        {
            return;
        }

        _removalDispatcher = null;
        dispatcher.Idle -= OnRemovalDispatcherIdle;
    }

    private void OnRemovalDispatcherIdle(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        CompletePendingRemoval();
    }

    private void CompletePendingRemoval()
    {
        if (_pendingRemovalCoordinator is not { } coordinator)
        {
            return;
        }

        CancelPendingRemoval();

        try
        {
            coordinator.Remove(this);
        }
        finally
        {
            if (Parent is null)
            {
                _pendingRemovalCoordinator = null;
            }
        }
    }

    #endregion

    #region Animation and timing

    /// <summary>Projects one stable final slot through the active entrance animation.</summary>
    internal Rect ProjectAnimation(Rect finalSlot, Rect contentBounds)
    {
        if (AnimationProgress >= 1 || Animation == ToastAnimation.Fade)
        {
            return finalSlot;
        }

        var progress = AnimationProgress;
        return Animation switch
        {
            ToastAnimation.SlideTop => new Rect(
                finalSlot.X,
                Interpolate(contentBounds.Y - finalSlot.Height, finalSlot.Y, progress),
                finalSlot.Width,
                finalSlot.Height),
            ToastAnimation.SlideDown => new Rect(
                finalSlot.X,
                Interpolate(finalSlot.Y - finalSlot.Height, finalSlot.Y, progress),
                finalSlot.Width,
                finalSlot.Height),
            ToastAnimation.SlideLeft => new Rect(
                Interpolate(finalSlot.X - finalSlot.Width, finalSlot.X, progress),
                finalSlot.Y,
                finalSlot.Width,
                finalSlot.Height),
            ToastAnimation.SlideRight => new Rect(
                Interpolate(finalSlot.Right, finalSlot.X, progress),
                finalSlot.Y,
                finalSlot.Width,
                finalSlot.Height),
            ToastAnimation.Expand => Expand(finalSlot, progress),
            ToastAnimation.Fade => finalSlot,
            _ => throw new UnreachableException()
        };
    }

    private static Rect Expand(Rect finalSlot, double progress)
    {
        var width = Interpolate(0, finalSlot.Width, progress);
        var height = Interpolate(0, finalSlot.Height, progress);
        var x = finalSlot.X + Interpolate(0, finalSlot.Width - width, 0.5);
        var y = finalSlot.Y + Interpolate(0, finalSlot.Height - height, 0.5);
        return new Rect(x, y, width, height);
    }

    private static int Interpolate(int start, int end, double progress) =>
        (int) Math.Round(start + ((end - start) * progress), MidpointRounding.AwayFromZero);

    [Pure]
    private Rect ResolveHeaderBounds()
    {
        var inner = ActualStyle.Padding.Deflate(ContentBounds);
        var hasHeader = !string.IsNullOrEmpty(Title) || Adornment is not null || IsDismissible;
        return hasHeader && inner.Height > 0
            ? new Rect(inner.X, inner.Y, inner.Width, 1)
            : new Rect(inner.X, inner.Y, inner.Width, 0);
    }

    [Pure]
    private Rect ResolveCloseTargetBounds()
    {
        var header = ResolveHeaderBounds();
        return IsDismissible && header.Width > 0
            ? new Rect(header.Right - 1, header.Y, 1, header.Height)
            : new Rect(header.Right, header.Y, 0, header.Height);
    }

    [Pure]
    private TerminalStyle ResolveFaceStyle(Face face)
    {
        var attributes = face.Attributes.IsLiteral
            ? face.Attributes.Literal
            : Theme?.ResolveAttributes(face.Attributes.SemanticDecoration) ?? TerminalAttributes.None;
        return new TerminalStyle(
            ResolveColor(face.Foreground),
            ResolveColor(face.Background),
            attributes,
            underline: face.Underline,
            underlineColor: ResolveColor(face.UnderlineColor));
    }

    private void StartPresentationTimers()
    {
        Debug.Assert(Dispatcher is not null, "An open Toast has an owning dispatcher.");

        if (AnimationDuration == TimeSpan.Zero)
        {
            SetAnimationProgress(1);
            StartDisplayTimer();
            return;
        }

        _animationState = new ToastAnimationState(Dispatcher.TimeProvider, AnimationDuration);
        var interval = AnimationDuration < _animationRefreshInterval
            ? AnimationDuration
            : _animationRefreshInterval;
        _animationTimer = new DispatcherTimer(Dispatcher, interval);
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
    }

    private void OnAnimationTick(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        var progress = _animationState?.Progress ?? 1;
        SetAnimationProgress(progress);

        if (progress < 1)
        {
            return;
        }

        _animationTimer?.Dispose();
        _animationTimer = null;
        _animationState = null;
        StartDisplayTimer();
    }

    private void SetAnimationProgress(double value)
    {
        value = Math.Clamp(value, 0, 1);

        if (AnimationProgress == value)
        {
            return;
        }

        AnimationProgress = value;
        Invalidate(Animation == ToastAnimation.Fade ? Invalidation.Render : Invalidation.Arrange);
        NotifyPropertyChanged(nameof(AnimationProgress), InvalidationImpact.None);
    }

    private void StartDisplayTimer()
    {
        Debug.Assert(Dispatcher is not null, "An open Toast has an owning dispatcher.");

        if (DisplayDuration == Timeout.InfiniteTimeSpan || !IsOpen)
        {
            return;
        }

        _displayTimer = new DispatcherTimer(Dispatcher, DisplayDuration);
        _displayTimer.Tick += OnDisplayTimerTick;
        _displayTimer.Start();
    }

    private void OnDisplayTimerTick(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        Dismiss();
    }

    private void DisposeTimers()
    {
        _animationTimer?.Dispose();
        _animationTimer = null;
        _animationState = null;
        _displayTimer?.Dispose();
        _displayTimer = null;
    }

    #endregion

    private void VerifyClosedMutation()
    {
        VerifyMutable();

        if (IsOpen)
        {
            throw new InvalidOperationException("Toast presentation options cannot change while it is open.");
        }
    }
}
