// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Popups;

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
        HitTestVisible = false;
        Focusable = false;

        // A Tooltip is never a normal tree member of its anchor - it lives in the anchor's
        // Popup-layer owned slot, which the framework's cascading Measure/Arrange walk never
        // visits (unlike ComboBox/DateInput/MenuItem, which re-arrange their own popup child
        // from their own ArrangeOverride every pass). LayoutPopup below is therefore the only
        // place this placement is ever resolved; Closed lets go of the relayout subscriptions
        // taken out in OnContentAvailable so a closed tooltip does not keep reacting to a
        // surface it is no longer presenting on.
        Closed += OnSurfaceClosed;
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
        SetContent(anchor, content);
        GetTooltip(anchor)!.Placement = placement;
    }

    /// <summary>Gets the tooltip associated with the specified control, or null.</summary>
    /// <param name="anchor">The non-null control to inspect.</param>
    /// <returns>The associated tooltip, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="anchor"/> is null.</exception>
    public static Tooltip? GetTooltip(ControlBase anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        return _attachedTooltips.TryGetValue(anchor, out var tooltip) ? tooltip : null;
    }

    /// <summary>Removes any tooltip associated with the specified control.</summary>
    /// <param name="anchor">The non-null control to clear.</param>
    /// <exception cref="ArgumentNullException"><paramref name="anchor"/> is null.</exception>
    public static void ClearTooltip(ControlBase anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);

        if (_attachedTooltips.TryGetValue(anchor, out var tooltip))
        {
            tooltip.Detach(anchor, clearOwnership: true);
            _ = _attachedTooltips.Remove(anchor);
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
    internal override void OnContentAvailable()
    {
        base.OnContentAvailable();

        if (_attachedAnchor is not null)
        {
            LayoutPopup();
            SubscribeSurfaceRelayout();
        }
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
        CancelShowTimer();
        CancelHideTimer();
        Hide();
        anchor.PointerEntered -= OnAnchorPointerEntered;
        anchor.PointerExited -= OnAnchorPointerExited;
        anchor.GotFocus -= OnAnchorGotFocus;
        anchor.LostFocus -= OnAnchorLostFocus;
        anchor.PointerPressed -= OnAnchorPointerPressed;

        if (clearOwnership)
        {
            _ = OwningSlot?.Remove(this);
        }

        _attachedAnchor = null;
        Anchor = null;
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

    private void LayoutPopup()
    {
        var rootBounds = RootBounds(default);

        if (rootBounds.Width == 0 || rootBounds.Height == 0)
        {
            return;
        }

        // Forced rather than left to Measure/Arrange's own dirty-phase check: an anchor reflow
        // (OnAnchorBoundsChanged) moves the point placement was resolved against without ever
        // changing rootBounds itself, so the constraint/slot passed below is byte-for-byte the
        // same one already recorded from the tooltip's last layout pass. Measure and Arrange both
        // short-circuit on an unchanged constraint/slot when nothing is already marked dirty, and
        // this tooltip's own subtree has nothing dirty - only its foreign anchor moved - so
        // without this, the calls below would silently no-op and leave placement pinned to
        // wherever the anchor used to be. Self-scoped rather than a plain Invalidate: only this
        // control's own short-circuit needs bypassing, and the two calls immediately below are
        // that synchronous follow-up InvalidateSelf's own contract requires - a full Invalidate
        // would additionally walk the dirty phase up through every ancestor to the root, forcing
        // an unrelated full-tree render pass to fix what is really a two-cell local reposition.
        InvalidateSelf(Invalidation.Measure);
        Measure(new Constraint(rootBounds.Width, rootBounds.Height));
        Arrange(rootBounds, widthResolved: true, heightResolved: true);
    }

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
        if (_showTimer is { Running: true })
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
        if (_hideTimer is { Running: true })
        {
            _hideTimer.Stop();
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
        // Cancel here, not only in OnUnavailable(Disposed): OnUnavailable is
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
        CancelShowTimer();
        CancelHideTimer();
        base.OnDetached();
        UnsubscribeSurfaceRelayout();
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        if (reason == ReleaseReason.Disposed)
        {
            if (_attachedAnchor is { } anchor)
            {
                Detach(anchor, clearOwnership: false);
                _ = _attachedTooltips.Remove(anchor);
            }

            _showTimer?.Dispose();
            _hideTimer?.Dispose();
        }

        base.OnUnavailable(reason);
    }

    #endregion
}
