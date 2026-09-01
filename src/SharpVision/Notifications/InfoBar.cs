// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Notifications;

using System.Runtime.ExceptionServices;

/// <summary>Displays one persistent in-flow notification with retained caller content.</summary>
[PublicAPI]
public sealed class InfoBar: ContentControl, IStyled<InfoBarStyle>
{
    private readonly InfoBarDismissButton _dismissButton;
    private readonly RetainedPropertyOverrideService _contentAvailability;
    private readonly RetainedPropertyOverrideService _dismissAvailability;
    private readonly CallbackTransitionStream _dismissTransitions = new();
    private readonly StyleSlot<InfoBarStyle> _style;
    private bool _isOpen = true;
    private int _dismissDepth;

    /// <summary>Initializes an open, dismissible informational bar.</summary>
    public InfoBar()
    {
        _style = InitializeStyle(InfoBarStyle.Definition);
        _contentAvailability = new RetainedPropertyOverrideService(
            this,
            ContentOwnershipSlot,
            OnAuthoredContentAvailabilityChanged);
        _dismissButton = new InfoBarDismissButton(this);
        var dismissSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: "dismiss",
                InvalidationImpact.Measure),
            capacity: 1);
        dismissSlot.Add(_dismissButton);
        _dismissAvailability = new RetainedPropertyOverrideService(this, dismissSlot);
        _ = _dismissAvailability.Acquire(_dismissButton, RetainedPropertyOverrides.Visibility);
        ApplyDismissAvailability();
        IsFocusable = false;
        IsTabStop = false;
    }

    /// <summary>Gets or sets the optional title rendered above retained content.</summary>
    /// <exception cref="ArgumentException">The value contains a terminal control cluster.</exception>
    /// <exception cref="InvalidOperationException">The attached bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The bar is disposed.</exception>
    public string? Title
    {
        get;
        set
        {
            if (value is not null)
            {
                ArgumentException.ThrowIfContainsControls(
                    value,
                    nameof(value),
                    "An InfoBar title cannot contain terminal controls.");
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets or sets the optional grapheme adornment rendered before the title.</summary>
    /// <exception cref="InvalidOperationException">The attached bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The bar is disposed.</exception>
    public Affix? Adornment
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    }

    /// <summary>Gets or sets whether this retained notification occupies layout and accepts input.</summary>
    /// <exception cref="InvalidOperationException">The attached bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The bar is disposed.</exception>
    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (value)
            {
                Open();
            }
            else
            {
                Dismiss();
            }
        }
    }

    /// <summary>Gets or sets whether the private close affordance accepts input.</summary>
    /// <exception cref="InvalidOperationException">The attached bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The bar is disposed.</exception>
    public bool IsDismissible
    {
        get;
        set
        {
            if (!SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                return;
            }

            ApplyDismissAvailability();
        }
    } = true;

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The bar is disposed.</exception>
    public InfoBarStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the resolved complete InfoBar presentation.</summary>
    public InfoBarStyle ActualStyle => _style.Actual;

    /// <summary>Raised before a close commits, allowing cancellation.</summary>
    public event EventHandler<InfoBarDismissRequestedEventArgs>? DismissRequested;

    /// <summary>Raised after a successful close and required availability cleanup commit.</summary>
    public event EventHandler? Dismissed;

    /// <summary>Requests cancellable dismissal of this bar.</summary>
    /// <exception cref="InvalidOperationException">The attached bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The bar is disposed.</exception>
    public void Dismiss()
    {
        VerifyMutable();

        if (!IsOpen || _dismissDepth > 0)
        {
            return;
        }

        var token = _dismissTransitions.Commit(this);
        var request = new InfoBarDismissRequestedEventArgs();
        ExceptionDispatchInfo? failure = null;
        _dismissDepth++;

        try
        {
            InvokeRequested(token, request, ref failure);
        }
        finally
        {
            _dismissDepth--;
        }

        if (request.Cancel || !token.IsCurrent)
        {
            _dismissButton.CancelInteraction();
            failure?.Throw();
            return;
        }

        _isOpen = false;
        ExceptionAggregation.Capture(ApplyClosedAvailability, ref failure);
        ExceptionAggregation.Capture(
            () => NotifyPropertyChanged(nameof(IsOpen), InvalidationImpact.Measure),
            ref failure);

        if (token.IsCurrent)
        {
            InvokeDismissed(token, ref failure);
        }

        failure?.Throw();
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) =>
        IsOpen ? MeasureOpen(constraint) : default;

    /// <inheritdoc/>
    internal override Size OnMeasuredDesired(Constraint constraint, Size desired)
    {
        _ = constraint;
        return IsOpen ? desired : default;
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (!IsOpen)
        {
            ArrangeCurrentParts(default, default);
            return;
        }

        var style = ActualStyle;
        var inner = style.Padding.Deflate(bounds);
        var hasHeader = HasHeader;
        var headerHeight = hasHeader ? Math.Min(1, inner.Height) : 0;
        var gap = hasHeader && HasVisibleContent
            ? Math.Min(style.ContentGap, Math.Max(0, inner.Height - headerHeight))
            : 0;
        var bodyY = inner.Y.Add(headerHeight).Add(gap);
        var body = new Rect(
            inner.X,
            Math.Min(inner.Bottom, bodyY),
            inner.Width,
            Math.Max(0, inner.Bottom - bodyY));
        var dismiss = IsDismissible && headerHeight != 0 && inner.Width != 0
            ? new Rect(inner.Right - 1, inner.Y, 1, 1)
            : default;
        ArrangeCurrentParts(body, dismiss);
    }

    /// <inheritdoc/>
    internal override ControlBase? HitTest(Point point) => IsOpen ? base.HitTest(point) : null;

    /// <inheritdoc/>
    protected override Rect VisualBounds => IsOpen ? base.VisualBounds : default;

    /// <inheritdoc/>
    protected override ChromeRenderOptions GetChromeRenderOptions() => IsOpen
        ? default
        : new ChromeRenderOptions { SkipBodyFill = true, SkipBorder = true, SkipShadow = true };

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (!IsOpen)
        {
            return;
        }

        base.OnRenderContent(canvas);
        var header = ResolveHeaderBounds();

        if (header.Width == 0 || header.Height == 0)
        {
            return;
        }

        var style = ActualStyle;
        var leading = IsDismissible
            ? new Rect(header.X, header.Y, Math.Max(0, header.Width - 1), header.Height)
            : header;
        var affixes = MeasureAffixes(Adornment, null, style.AdornmentGap);

        if (affixes.StartCells <= leading.Width)
        {
            var adornmentStyle = ResolvedStyle.WithForeground(ResolveColor(style.AdornmentColor));
            RenderAffixes(canvas, leading, affixes, Adornment, null, adornmentStyle);
        }
        else
        {
            affixes = default;
        }

        if (!string.IsNullOrEmpty(Title))
        {
            var titleBounds = DeflateForAffixes(leading, affixes);
            var titleStyle = ResolveFaceStyle(style.TitleFace);
            _ = canvas.Clip(titleBounds).Draw(
                Title.AsSpan(),
                new Point(titleBounds.X, titleBounds.Y),
                titleStyle,
                background: BackgroundMode.Transparent);
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
    protected override void OnUnavailable(ReleaseReason reason)
    {
        _ = _dismissTransitions.Commit(this);
        ExceptionDispatchInfo? failure = null;

        if (reason == ReleaseReason.Disposed)
        {
            _contentAvailability.Dispose();
            _dismissAvailability.Dispose();
        }

        ExceptionAggregation.Capture(_dismissButton.CancelInteraction, ref failure);
        ExceptionAggregation.Capture(() => base.OnUnavailable(reason), ref failure);

        if (reason == ReleaseReason.Disposed)
        {
            DismissRequested = null;
            Dismissed = null;
        }

        failure?.Throw();
    }

    /// <inheritdoc/>
    protected override void OnContentChanged(ControlBase? previous, ControlBase? current)
    {
        _ = _dismissTransitions.Commit(this);

        if (previous is not null)
        {
            _contentAvailability.Restore(previous);
        }

        if (current is not null && !IsDisposing)
        {
            var lease = _contentAvailability.Acquire(
                current,
                RetainedPropertyOverrides.Visibility);
            ApplyContentAvailability(lease);
        }

        base.OnContentChanged(previous, current);
    }

    private void Open()
    {
        VerifyMutable();

        if (IsOpen)
        {
            if (_dismissDepth > 0)
            {
                _ = _dismissTransitions.Commit(this);
            }

            return;
        }

        _ = _dismissTransitions.Commit(this);
        _isOpen = true;
        RestoreOpenAvailability();
        NotifyPropertyChanged(nameof(IsOpen), InvalidationImpact.Measure);
    }

    private bool HasHeader =>
        !string.IsNullOrEmpty(Title) || Adornment is not null || IsDismissible;

    private bool HasVisibleContent =>
        Content is { Visibility: not Visibility.Collapsed };

    private Size MeasureOpen(Constraint constraint)
    {
        var style = ActualStyle;
        var horizontal = style.Padding.Horizontal;
        var vertical = style.Padding.Vertical;
        var headerWidth = MeasureHeaderWidth(style);
        var headerHeight = HasHeader ? 1 : 0;
        var contentWidth = 0;
        var contentHeight = 0;

        if (Content is { } content)
        {
            var gap = headerHeight == 0 ? 0 : style.ContentGap;
            var desired = MeasureChild(
                content,
                new Constraint(
                    constraint.Width.Subtract(horizontal),
                    constraint.Height.Subtract(vertical.Add(headerHeight).Add(gap))));

            if (content.Visibility != Visibility.Collapsed)
            {
                contentWidth = desired.Width.Add(content.Margin.Horizontal);
                contentHeight = desired.Height.Add(content.Margin.Vertical);
            }
        }

        var contentGap = headerHeight != 0 && contentHeight != 0 ? style.ContentGap : 0;
        return new Size(
            Math.Max(headerWidth, contentWidth).Add(horizontal),
            headerHeight.Add(contentGap).Add(contentHeight).Add(vertical));
    }

    private int MeasureHeaderWidth(InfoBarStyle style)
    {
        var titleWidth = string.IsNullOrEmpty(Title)
            ? 0
            : Title.Measure(CellPolicy.AmbiguousWidth, useMnemonic: false);
        var adornmentWidth = Adornment is null
            ? 0
            : MeasureAffixes(Adornment, null, style.AdornmentGap).StartCells;
        return titleWidth.Add(adornmentWidth).Add(IsDismissible ? 1 : 0);
    }

    private Rect ResolveHeaderBounds()
    {
        var inner = ActualStyle.Padding.Deflate(ContentBounds);
        return HasHeader
            ? new Rect(inner.X, inner.Y, inner.Width, Math.Min(1, inner.Height))
            : default;
    }

    private void ArrangeCurrentParts(Rect contentBounds, Rect dismissBounds)
    {
        if (Content is { } content)
        {
            ArrangeChild(content, contentBounds, ResolvedAxes.Both);
        }

        ArrangeChild(_dismissButton, dismissBounds, ResolvedAxes.Both);
    }

    private void ApplyClosedAvailability()
    {
        if (Content is { } content)
        {
            _contentAvailability.Get(content).SetLive(
                RetainedControlProperty.Visibility,
                Visibility.Collapsed);
        }

        ApplyDismissAvailability();
        _dismissButton.CancelInteraction();
    }

    private void RestoreOpenAvailability()
    {
        if (Content is { } content)
        {
            ApplyContentAvailability(_contentAvailability.Get(content));
        }

        ApplyDismissAvailability();
    }

    private void ApplyContentAvailability(RetainedPropertyOverrideLease lease)
    {
        var authored = lease.GetAuthored<Visibility>(RetainedControlProperty.Visibility);
        lease.SetLive(
            RetainedControlProperty.Visibility,
            IsOpen ? authored : Visibility.Collapsed);
    }

    private void ApplyDismissAvailability()
    {
        var lease = _dismissAvailability.Get(_dismissButton);
        lease.SetLive(
            RetainedControlProperty.Visibility,
            IsOpen && IsDismissible ? Visibility.Visible : Visibility.Collapsed);

        if (!IsOpen || !IsDismissible)
        {
            _dismissButton.CancelInteraction();
        }
    }

    private void OnAuthoredContentAvailabilityChanged(
        ControlBase control,
        RetainedControlProperty property)
    {
        if (property == RetainedControlProperty.Visibility &&
            ReferenceEquals(control, Content))
        {
            ApplyContentAvailability(_contentAvailability.Get(control));
        }
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

    private void InvokeRequested(
        CallbackTransitionToken token,
        InfoBarDismissRequestedEventArgs eventArgs,
        ref ExceptionDispatchInfo? failure)
    {
        if (DismissRequested is not { } handlers)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            if (!token.IsCurrent)
            {
                break;
            }

            ExceptionAggregation.Capture(
                () => ((EventHandler<InfoBarDismissRequestedEventArgs>) handler)(this, eventArgs),
                ref failure);
        }
    }

    private void InvokeDismissed(
        CallbackTransitionToken token,
        ref ExceptionDispatchInfo? failure)
    {
        if (Dismissed is not { } handlers)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            if (!token.IsCurrent)
            {
                break;
            }

            ExceptionAggregation.Capture(
                () => ((EventHandler) handler)(this, EventArgs.Empty),
                ref failure);
        }
    }
}
