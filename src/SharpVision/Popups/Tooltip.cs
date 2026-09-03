// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Popups;

using System.Runtime.ExceptionServices;

using DisplayText = Controls.Display.Text;

/// <summary>Displays passive anchored popup content after a configurable pointer or focus delay.</summary>
[PublicAPI]
public sealed class Tooltip: Popup
{
    private const string _tooltipPartKey = "tooltip";
    private static readonly ConditionalWeakTable<ControlBase, Tooltip> _attachedTooltips = [];

    private DispatcherTimer? _showTimer;
    private DispatcherTimer? _hideTimer;
    private ControlBase? _attachedAnchor;
    private DisplayText? _textContent;
    private ControlBase? _layoutRoot;

    /// <summary>Initializes a closed passive tooltip with default delays.</summary>
    public Tooltip()
    {
        FocusOnOpen = false;
        ModalBehavior = PopupModalBehavior.None;
        SuppressCloseOtherPopups = true;
        CloseOnEscape = false;
        IsHitTestVisible = false;
        IsFocusable = false;

        // A Tooltip is never a normal tree member of its anchor - it lives in the anchor's
        // Popup-layer owned slot, which the framework's cascading Measure/Arrange walk never
        // visits (unlike ComboBox/DateInput/MenuItem, which re-arrange their own popup child
        // from their own ArrangeOverride every pass). LayoutPopup below is therefore the only
        // place this placement is ever resolved; Closed lets go of the relayout subscriptions
        // taken out in OnContentAvailable so a closed tooltip does not keep reacting to a
        // surface it is no longer presenting on.
        Closed += OnSurfaceClosed;

        // A Popup whose anchor (or an ancestor of it) becomes hidden deliberately stays logically
        // open so it can re-present when that ancestor recovers - right for a drop-down the user
        // explicitly opened, wrong for a passive hint: a tooltip that reappeared on its own once
        // the anchor was shown again would have no hover or focus behind it. Effective visibility
        // is published to every descendant of the changed subtree, so the tooltip can simply hide
        // itself and let a fresh hover or focus start over. A disabled anchor is different: the
        // presentation stays and merely paints disabled, and the pointer path retiring from the
        // disabled anchor already runs the ordinary exit-then-hide-delay flow.
        PropertyChanged += OnTooltipPropertyChanged;
    }

    private void OnTooltipPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName == nameof(EffectiveIsVisible) && !EffectiveIsVisible)
        {
            Hide();
        }
    }

    /// <summary>Gets the passive, non-interactive hint role, distinct from Popup's framed
    /// appearance so a hint reads as a quiet transient label rather than an interactive
    /// menu or drop-down.</summary>
    /// <inheritdoc/>
    protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
        (theme ?? ThemeCatalog.Dark).GetStyleSet(TooltipStyle.Default).ToAppearanceStates();

    #region Attached data

    /// <summary>Sets a text tooltip on the specified anchor control.</summary>
    /// <param name="anchor">The non-null control to annotate.</param>
    /// <param name="text">The non-null tooltip text.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static void SetText(ControlBase anchor, string text)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(text);
        var tooltip = GetOrCreateTooltip(anchor);
        tooltip.Text = text;
    }

    /// <summary>Sets a text tooltip with placement on the specified anchor control.</summary>
    /// <param name="anchor">The non-null control to annotate.</param>
    /// <param name="text">The non-null tooltip text.</param>
    /// <param name="placement">The preferred placement relative to the anchor.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="placement"/> is unknown.</exception>
    public static void SetText(ControlBase anchor, string text, PopupPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNotDefined(
            placement,
            nameof(placement),
            "The popup placement is unknown.");
        SetText(anchor, text);
        GetTooltip(anchor)!.Placement = placement;
    }

    /// <summary>Sets a text tooltip with placement and delay on the specified anchor control.</summary>
    /// <param name="anchor">The non-null control to annotate.</param>
    /// <param name="text">The non-null tooltip text.</param>
    /// <param name="placement">The preferred placement relative to the anchor.</param>
    /// <param name="showDelay">The delay before showing on hover or focus.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The placement is unknown or the delay is invalid.</exception>
    public static void SetText(ControlBase anchor, string text, PopupPlacement placement, TimeSpan showDelay)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNotDefined(
            placement,
            nameof(placement),
            "The popup placement is unknown.");
        DispatcherTimer.ValidateInterval(showDelay, nameof(showDelay));
        SetText(anchor, text, placement);
        GetTooltip(anchor)!.ShowDelay = showDelay;
    }

    /// <summary>Sets a rich-content tooltip on the specified anchor control.</summary>
    /// <param name="anchor">The non-null control to annotate.</param>
    /// <param name="content">The non-null content control to display.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static void SetContent(ControlBase anchor, ControlBase content)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(content);
        var tooltip = GetOrCreateTooltip(anchor);
        tooltip.Content = content;
    }

    /// <summary>Sets a rich-content tooltip with placement on the specified anchor control.</summary>
    /// <param name="anchor">The non-null control to annotate.</param>
    /// <param name="content">The non-null content control to display.</param>
    /// <param name="placement">The preferred placement relative to the anchor.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="placement"/> is unknown.</exception>
    public static void SetContent(ControlBase anchor, ControlBase content, PopupPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNotDefined(
            placement,
            nameof(placement),
            "The popup placement is unknown.");
        SetContent(anchor, content);
        GetTooltip(anchor)!.Placement = placement;
    }

    /// <summary>Gets the tooltip associated with the specified control, or null.</summary>
    /// <param name="anchor">The non-null control to inspect.</param>
    /// <returns>The associated tooltip, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="anchor"/> is null.</exception>
    [Pure]
    public static Tooltip? GetTooltip(ControlBase anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        return _attachedTooltips.TryGetValue(anchor, out var tooltip) ? tooltip : null;
    }

    /// <summary>Removes any tooltip associated with the specified control.</summary>
    /// <param name="anchor">The non-null control to clear.</param>
    /// <exception cref="ArgumentNullException"><paramref name="anchor"/> is null.</exception>
    /// <exception cref="Exception">A close callback fails after association and ownership cleanup completes.</exception>
    public static void ClearTooltip(ControlBase anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);

        if (_attachedTooltips.TryGetValue(anchor, out var tooltip))
        {
            ExceptionDispatchInfo? failure = null;
            ExceptionAggregation.Capture(() => tooltip.Detach(anchor, clearOwnership: true), ref failure);
            ExceptionAggregation.Capture(() => _ = _attachedTooltips.Remove(anchor), ref failure);
            failure?.Throw();
        }
    }

    private static Tooltip GetOrCreateTooltip(ControlBase anchor)
    {
        if (_attachedTooltips.TryGetValue(anchor, out var existing))
        {
            return existing;
        }

        var tooltip = new Tooltip();
        tooltip.Attach(anchor);
        _attachedTooltips.Add(anchor, tooltip);
        return tooltip;
    }

    #endregion

    #region Content and timing

    /// <summary>Gets or sets the text shorthand displayed by this tooltip.</summary>
    /// <exception cref="InvalidOperationException">The attached tooltip is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tooltip is disposed.</exception>
    public string? Text
    {
        get;
        set
        {
            if (!SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                return;
            }

            if (Content is null || ReferenceEquals(Content, _textContent))
            {
                _textContent ??= new DisplayText();
                _textContent.Content = value ?? string.Empty;

                // Content already referencing _textContent makes this a same-reference,
                // no-op assignment that never raises OnContentChanged - the desired height
                // this text now needs (it may have grown or shrunk) would otherwise never
                // be re-resolved against the anchor while the tooltip is open.
                var contentUnchanged = ReferenceEquals(Content, _textContent);
                Content = _textContent;

                if (contentUnchanged && IsOpen && _attachedAnchor is not null)
                {
                    LayoutPopup();
                }
            }
        }
    }

    /// <summary>Gets or sets the delay before showing on hover or focus.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The interval is negative or too large.</exception>
    /// <exception cref="InvalidOperationException">The attached tooltip is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tooltip is disposed.</exception>
    public TimeSpan ShowDelay
    {
        get;
        set
        {
            DispatcherTimer.ValidateInterval(value, nameof(value));
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = TimeSpan.FromMilliseconds(500);

    /// <summary>Gets or sets the delay before hiding on pointer exit.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The interval is negative or too large.</exception>
    /// <exception cref="InvalidOperationException">The attached tooltip is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tooltip is disposed.</exception>
    public TimeSpan HideDelay
    {
        get;
        set
        {
            DispatcherTimer.ValidateInterval(value, nameof(value));
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = TimeSpan.FromMilliseconds(100);

    /// <inheritdoc/>
    protected override void OnContentChanged(ControlBase? previous, ControlBase? current)
    {
        base.OnContentChanged(previous, current);

        if (!ReferenceEquals(current, _textContent))
        {
            _textContent = null;
        }

        // SetContent may swap in taller or shorter rich content while the tooltip is already
        // open (the Text setter above handles same-reference mutation; this handles the Content
        // reference itself changing), so the resolved placement must be re-run against the new
        // desired size rather than the one measured when it last opened.
        if (IsOpen && _attachedAnchor is not null)
        {
            LayoutPopup();
        }
    }

    /// <inheritdoc/>
    internal override bool OnContentAvailable()
    {
        if (!base.OnContentAvailable())
        {
            return false;
        }

        // Placement itself was already resolved by the base opening pass (Anchor is the attached
        // anchor); only the relayout subscription is tooltip-specific.
        if (_attachedAnchor is not null)
        {
            SubscribeSurfaceRelayout();
        }

        return true;
    }

    #endregion

    #region Anchor wiring

    private void Attach(ControlBase anchor)
    {
        _attachedAnchor = anchor;
        Anchor = anchor;
        var slot = anchor.FindOwnedSlot(_tooltipPartKey) ??
                   anchor.RegisterOwnedSlot(
                       new OwnedControlOptions(
                           OwnedControlRole.FrameworkPart,
                           OwnedControlLayer.Popup,
                           participatesInHitTesting: false,
                           participatesInNavigation: false,
                           partKey: _tooltipPartKey,
                           InvalidationImpact.None),
                       capacity: 1);
        slot.Add(this);

        anchor.PointerEntered += OnAnchorPointerEntered;
        anchor.PointerExited += OnAnchorPointerExited;
        anchor.GotFocus += OnAnchorGotFocus;
        anchor.LostFocus += OnAnchorLostFocus;
        anchor.PointerPressed += OnAnchorPointerPressed;
    }

    private void Detach(ControlBase anchor, bool clearOwnership)
    {
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(CancelShowTimer, ref failure);
        ExceptionAggregation.Capture(CancelHideTimer, ref failure);
        ExceptionAggregation.Capture(Hide, ref failure);
        ExceptionAggregation.Capture(
            () =>
            {
                anchor.PointerEntered -= OnAnchorPointerEntered;
                anchor.PointerExited -= OnAnchorPointerExited;
                anchor.GotFocus -= OnAnchorGotFocus;
                anchor.LostFocus -= OnAnchorLostFocus;
                anchor.PointerPressed -= OnAnchorPointerPressed;
            },
            ref failure);

        if (clearOwnership)
        {
            ExceptionAggregation.Capture(() => _ = OwningSlot?.Remove(this), ref failure);
        }

        _attachedAnchor = null;
        ExceptionAggregation.Capture(() => Anchor = null, ref failure);
        failure?.Throw();
    }

    private void OnAnchorPointerEntered(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        CancelHideTimer();
        StartShowTimer();
    }

    private void OnAnchorPointerExited(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        CancelShowTimer();

        if (IsOpen)
        {
            StartHideTimer();
        }
    }

    private void OnAnchorGotFocus(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        CancelHideTimer();
        StartShowTimer();
    }

    private void OnAnchorLostFocus(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        CancelShowTimer();
        Hide();
    }

    private void OnAnchorPointerPressed(object? sender, PointerEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        Hide();
    }

    #endregion

    #region Show and hide

    private void Show()
    {
        CancelShowTimer();
        CancelHideTimer();

        if (!IsOpen && _attachedAnchor is not null)
        {
            IsOpen = true;
        }
    }

    private void Hide()
    {
        CancelShowTimer();
        CancelHideTimer();

        if (IsOpen)
        {
            IsOpen = false;
        }
    }

    // A Tooltip lives in the anchor's Popup-layer owned slot, which the cascading layout walk
    // never visits, so the shared root-relative pass is the only place its placement is ever
    // resolved (see the constructor remarks).
    private void LayoutPopup() => LayoutAgainstRoot();

    /// <summary>Starts reacting to the presented surface re-laying out while this tooltip is
    /// open, so a resize or an anchor reflow that happens after opening re-resolves placement
    /// instead of leaving it pinned to the geometry that was current when it opened.</summary>
    private void SubscribeSurfaceRelayout()
    {
        ControlBase root = this;

        while (root.Parent is { } parent)
        {
            root = parent;
        }

        if (ReferenceEquals(root, _layoutRoot))
        {
            return;
        }

        UnsubscribeSurfaceRelayout();
        _layoutRoot = root;
        root.BoundsChanged += OnSurfaceBoundsChanged;
    }

    private void UnsubscribeSurfaceRelayout()
    {
        if (_layoutRoot is { } root)
        {
            root.BoundsChanged -= OnSurfaceBoundsChanged;
            _layoutRoot = null;
        }
    }

    private void OnSurfaceBoundsChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (IsOpen && _attachedAnchor is not null)
        {
            LayoutPopup();
        }
    }

    private void OnSurfaceClosed(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        UnsubscribeSurfaceRelayout();
    }

    /// <summary>Gets the show timer's current Tick subscriber count for duplicate-subscription
    /// test seams, or zero when no show timer has been created yet.</summary>
    internal int ShowTimerTickSubscribers => _showTimer?.TickSubscribers ?? 0;

    /// <summary>Gets the hide timer's current Tick subscriber count for duplicate-subscription
    /// test seams, or zero when no hide timer has been created yet.</summary>
    internal int HideTimerTickSubscribers => _hideTimer?.TickSubscribers ?? 0;

    /// <summary>Gets whether a show-delay timer is retained, proving detachment releases the
    /// dispatcher-owned timer rather than merely stopping it for reuse under another owner.</summary>
    internal bool HasShowTimer => _showTimer is not null;

    /// <summary>Gets whether the retained show-delay timer is armed, proving cancellation state
    /// without exposing the timer object itself.</summary>
    internal bool IsShowTimerRunning => _showTimer?.IsRunning == true;

    /// <summary>Gets whether this tooltip still holds a presented surface's relayout
    /// subscription, proving release without exposing the subscribed root itself.</summary>
    internal bool HasSurfaceRelayoutSubscription => _layoutRoot is not null;

    /// <summary>Starts the ordinary show delay for lifecycle tests whose anchor detachment would
    /// otherwise synthesize pointer-exit cancellation before the target state can be observed.</summary>
    internal void StartShowTimerForLifecycleTest() => StartShowTimer();

    private void StartShowTimer()
    {
        if (IsOpen || _attachedAnchor?.Dispatcher is not { } dispatcher)
        {
            return;
        }

        if (_showTimer is null)
        {
            _showTimer = new DispatcherTimer(dispatcher, ShowDelay);
            _showTimer.Tick += OnShowTimerTick;
        }

        _showTimer.Interval = ShowDelay;
        _showTimer.Start();
    }

    private void CancelShowTimer()
    {
        if (_showTimer is { IsRunning: true })
        {
            _showTimer.Stop();
        }
    }

    private void StartHideTimer()
    {
        if (!IsOpen || _attachedAnchor?.Dispatcher is not { } dispatcher)
        {
            return;
        }

        if (_hideTimer is null)
        {
            _hideTimer = new DispatcherTimer(dispatcher, HideDelay);
            _hideTimer.Tick += OnHideTimerTick;
        }

        _hideTimer.Interval = HideDelay;
        _hideTimer.Start();
    }

    private void CancelHideTimer()
    {
        if (_hideTimer is { IsRunning: true })
        {
            _hideTimer.Stop();
        }
    }

    private void ReleaseTimers()
    {
        var showTimer = _showTimer;
        var hideTimer = _hideTimer;
        _showTimer = null;
        _hideTimer = null;

        if (showTimer is not null)
        {
            showTimer.Tick -= OnShowTimerTick;
            showTimer.Dispose();
        }

        if (hideTimer is not null)
        {
            hideTimer.Tick -= OnHideTimerTick;
            hideTimer.Dispose();
        }
    }

    private void OnShowTimerTick(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        Show();
    }

    private void OnHideTimerTick(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        Hide();
    }

    #endregion

    #region Lifecycle

    /// <inheritdoc/>
    protected override void OnDetached()
    {
        // Cancel here, not only in OnUnavailable(IsDisposed): OnUnavailable is
        // only raised for the removed subtree's root (the anchor itself, not
        // this tooltip, its owned popup-layer child), but OnDetached cascades
        // to every owned-slot descendant on any detachment — including the
        // anchor merely detaching from its own parent (e.g. a virtualized
        // list row being recycled), not just this tooltip being disposed
        // outright. A pending show/hide timer must not survive that and fire
        // afterward: Show() would commit IsOpen=true while this popup's
        // Dispatcher is null, and a later reattachment (the recycled row
        // reused) would silently re-present the tooltip with no actual
        // hover/focus interaction from the user. Popup's own OnDetached
        // already force-closes an already-open popup; this only needs to
        // additionally stop a timer that hasn't fired yet. Popup's force-close on this path
        // also bypasses the public Closed event (it commits closed state directly rather than
        // running the normal CloseSurface sequence), so OnSurfaceClosed's cleanup would never
        // run here; drop the surface relayout subscription directly instead of leaking it.
        ReleaseTimers();
        base.OnDetached();
        UnsubscribeSurfaceRelayout();
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        ExceptionDispatchInfo? failure = null;

        if (reason == ReleaseReason.Disposed)
        {
            PropertyChanged -= OnTooltipPropertyChanged;
        }

        if (reason == ReleaseReason.Disposed && _attachedAnchor is { } anchor)
        {
            ExceptionAggregation.Capture(() => Detach(anchor, clearOwnership: false), ref failure);
            ExceptionAggregation.Capture(() => _ = _attachedTooltips.Remove(anchor), ref failure);
        }

        // Popup force-closes identically for Hidden and Disposed (see FloatingSurfaceBase and
        // Popup.OnUnavailable), but that force-close commits closed state directly rather than
        // running the normal CloseSurface sequence, so it never raises the public Closed event -
        // OnSurfaceClosed's UnsubscribeSurfaceRelayout call above never runs for either reason.
        // Disposed also reaches OnDetached (disposal cascades a slot removal that detaches this
        // control), which already releases both; widening this to Hidden too - and calling
        // UnsubscribeSurfaceRelayout here unconditionally for both reasons - means a merely-hidden
        // tooltip stops leaking its dispatcher timers and relayout subscription the same way an
        // already-covered Detached/Disposed tooltip does. A later duplicate call from OnDetached
        // on the Disposed path is a safe no-op: UnsubscribeSurfaceRelayout clears _layoutRoot on
        // its first call and only unsubscribes when a root is still recorded. No resume-from-Hidden
        // flow depends on either surviving: Visibility's setter has no re-arm hook, so any real
        // reshow goes through fresh hover/focus/Attach logic that re-arms timers and
        // re-subscribes (SubscribeSurfaceRelayout is itself ref-equality guarded against
        // redundant resubscription).
        if (reason is ReleaseReason.Hidden or ReleaseReason.Disposed)
        {
            ExceptionAggregation.Capture(ReleaseTimers, ref failure);
            ExceptionAggregation.Capture(UnsubscribeSurfaceRelayout, ref failure);
        }

        ExceptionAggregation.Capture(() => base.OnUnavailable(reason), ref failure);
        failure?.Throw();
    }

    #endregion
}
