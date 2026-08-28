// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

using System.Runtime.ExceptionServices;

using DisplayText = Display.Text;

/// <summary>Displays a collapsible section with a focusable header toggle and optional content.</summary>
/// <remarks>
/// A plain <see cref="DisplayText"/> header (the common case, materialized by
/// <see cref="HeaderedContentControl.HeaderText"/>) is excluded from the ordinary descendant render
/// pass and painted by the expander itself with its own state-resolved style, so the caption shows
/// the same hover, focus, and disabled cues as the disclosure glyph; any other header control
/// paints itself with its own resolved style.
/// </remarks>
[PublicAPI]
public sealed class Expander: HeaderedContentControl, IStyled<ExpanderStyle>
{
    // The header occupies one terminal row.
    private const int _headerHeightCells = 1;

    private readonly PressBehavior _interaction;
    private readonly StyleSlot<ExpanderStyle> _style;
    private long _expandedVersion;
    private bool _isHeaderPointerOver;
    private Visibility? _requestedContentVisibility;

    /// <summary>Initializes an expanded borderless section with an empty header.</summary>
    public Expander()
    {
        _style = InitializeStyle(ExpanderStyle.Definition);
        _interaction = new PressBehavior(
            () => HeaderBounds,
            () => !IsDisposed && EffectiveIsEnabled && EffectiveIsVisible,
            () => FocusOwner is null || IsFocused, RequestFocus, CapturePointer,
            () => HasPointerCapture, ReleasePointerCapture, SetPressed,
            _ => IsExpanded = !IsExpanded,
            () => Capabilities.KeyReleaseEvents.Authoritative);
        RegisterLifecycleParticipant(_interaction);
        IsFocusable = true;
        IsTabStop = true;
    }

    /// <summary>Gets the retained header-hover detail used to prove reconciliation with the
    /// framework pointer-over transition.</summary>
    /// <returns>Whether header-specific hover is retained.</returns>
    internal bool HasHeaderPointerOver() => _isHeaderPointerOver;

    /// <summary>Raised after the expanded state and content visibility commit.</summary>
    public event EventHandler<ExpandedChangedEventArgs>? ExpandedChanged;

    /// <summary>Gets or sets whether the content is visible.</summary>
    /// <remarks>A public property observer may commit a newer expansion state. That newer state
    /// owns content visibility and the typed event stream; the superseded outer transition does not
    /// publish a stale <see cref="ExpandedChanged"/> event.</remarks>
    /// <exception cref="InvalidOperationException">The attached expander is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The expander is disposed.</exception>
    public bool IsExpanded
    {
        get;
        set
        {
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            var version = ++_expandedVersion;
            ExceptionDispatchInfo? failure = null;
            ExceptionAggregation.Capture(
                () => NotifyPropertyChanged(nameof(IsExpanded), InvalidationImpact.Measure),
                ref failure);

            if (IsCurrentExpansion(version, value))
            {
                ExceptionAggregation.Capture(ApplyContentVisibility, ref failure);
            }

            if (IsCurrentExpansion(version, value))
            {
                ExceptionAggregation.Capture(
                    () => ExpandedChanged?.Invoke(this, new ExpandedChangedEventArgs(value)),
                    ref failure);
            }

            failure?.Throw();
        }
    } = true;

    private bool IsCurrentExpansion(long version, bool value) =>
        !IsDisposed && _expandedVersion == version && IsExpanded == value;

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached expander is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The expander is disposed.</exception>
    public ExpanderStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public ExpanderStyle ActualStyle => _style.Actual;

    // The header row's disclosure glyph and label share the same horizontal offset as the
    // expanded content below it, so this always mirrors ActualStyle.ContentIndent - floored at
    // one cell so the disclosure glyph always has room to paint even when a theme or instance
    // sets ContentIndent to zero.
    private int HeaderChromeWidth => Math.Max(1, ActualStyle.ContentIndent);

    /// <inheritdoc/>
    protected override void OnContentChanged(ControlBase? previous, ControlBase? current)
    {
        if (previous is not null && _requestedContentVisibility is { } requested)
        {
            previous.Visibility = requested;
        }

        _requestedContentVisibility = current?.Visibility;
        ApplyContentVisibility();
    }

    // Collapsed content stays Tab-focusable and hit-testable if only its
    // arranged size shrinks to zero, since focus eligibility is driven by
    // the Visibility chain, not arranged size — mirroring the pattern
    // TabControl uses to hide non-selected pages. The caller's authored
    // Visibility (captured in OnContentChanged) is restored, not assumed to
    // be IsVisible, so content the caller already collapsed for its own
    // reasons stays collapsed after re-expanding.
    private void ApplyContentVisibility()
    {
        if (Content is not { } content || _requestedContentVisibility is not { } requested)
        {
            return;
        }

        content.Visibility = IsExpanded ? requested : Visibility.Collapsed;
    }

    /// <inheritdoc/>
    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var headerContentWidth = 0;

        if (Header is { } header)
        {
            var hd = MeasureChild(header, new Constraint(null, _headerHeightCells));
            headerContentWidth = header.Visibility == Visibility.Collapsed
                ? 0
                : hd.Width.Add(header.Margin.Horizontal);
        }

        var hw = HeaderChromeWidth.Add(headerContentWidth);
        if (!IsExpanded || Content is not { } child)
        {
            return new Size(hw, _headerHeightCells);
        }

        var d = MeasureChild(child,
            new Constraint(
                constraint.Width.HasValue ? Math.Max(0, constraint.Width.Value - ActualStyle.ContentIndent) : null,
                constraint.Height.HasValue ? Math.Max(0, constraint.Height.Value - _headerHeightCells) : null));
        var cw = child.Visibility == Visibility.Collapsed
            ? 0
            : d.Width.Add(child.Margin.Horizontal);
        var ch = child.Visibility == Visibility.Collapsed
            ? 0
            : d.Height.Add(child.Margin.Vertical);
        var indentedWidth = ActualStyle.ContentIndent.Add(cw);
        return new Size(
            Math.Max(hw, indentedWidth),
            _headerHeightCells.Add(ch));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (Header is { } header)
        {
            var headerChromeWidth = HeaderChromeWidth;
            var headerSlot = bounds.Width > headerChromeWidth
                ? new Rect(
                    bounds.X + headerChromeWidth,
                    bounds.Y,
                    bounds.Width - headerChromeWidth,
                    Math.Min(_headerHeightCells, bounds.Height))
                : default;
            ArrangeChild(header, headerSlot, ResolvedAxes.Height);
        }

        if (Content is not { } content)
        {
            return;
        }

        var indent = Math.Min(ActualStyle.ContentIndent, bounds.Width);
        var slot = IsExpanded && bounds.Height > _headerHeightCells
            ? new Rect(
                bounds.X + indent,
                bounds.Y + _headerHeightCells,
                bounds.Width - indent,
                bounds.Height - _headerHeightCells)
            : default;
        ArrangeChild(content, slot, ResolvedAxes.Both);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var content = ContentBounds;
        var s = IsFocused
            ? ResolvedStyle.WithForeground(ResolveColor(ActualBorder.Foreground))
            : ResolvedStyle;

        var themed = IsExpanded ? ControlGlyphs.Disclosure.Expanded : ControlGlyphs.Disclosure.Collapsed;
        var style = ActualStyle;
        var selected = IsExpanded ? style.ExpandedGlyph : style.CollapsedGlyph;
        canvas.DrawRune(
            selected.Resolve(themed.Fallback, CellPolicy.AmbiguousWidth),
            new Point(content.X, content.Y),
            s,
            BackgroundMode.Transparent);

        // A Hidden or Collapsed header renders no caption at all, matching the disclosure-glyph-only
        // row a missing Header already draws and the zero width MeasureOverride already reports for
        // a Collapsed header - the ordinary Render(TerminalCanvas) gate every other header control
        // gets already gives this behavior for a rich header rendered through the owned pipeline in
        // RenderChildren; a DisplayText header instead paints directly here and needs the same check.
        if (Header is { EffectiveIsVisible: true } and DisplayText text && content.Width > HeaderChromeWidth)
        {
            var caption = canvas.Clip(HeaderBounds);
            _ = text.Content.Draw(
                caption,
                new Point(content.X + HeaderChromeWidth, content.Y),
                s,
                BackgroundMode.Transparent,
                CellPolicy.AmbiguousWidth,
                UseMnemonic,
                EffectiveIsEnabled ? Theme?.Hotkey ?? Color.Default : null);
        }
    }

    /// <summary>Renders only non-caption children through the ordinary descendant pass; a
    /// <see cref="DisplayText"/> header paints from <see cref="OnRenderContent"/> instead, with the
    /// expander's own state-resolved style.</summary>
    internal override void RenderChildren(TerminalCanvas canvas, Rect contentClip)
    {
        if (Header is { } header && header is not DisplayText)
        {
            header.Render(canvas, contentClip);
        }

        Content?.Render(canvas, contentClip);
    }

    /// <inheritdoc/>
    protected override bool OnAccessKey(Rune key)
    {
        _ = key;
        _ = FocusAccessKeyTarget();
        IsExpanded = !IsExpanded;
        return true;
    }

    private Rect HeaderBounds
    {
        get
        {
            var content = ContentBounds;
            return new Rect(
                content.X,
                content.Y,
                content.Width,
                Math.Min(_headerHeightCells, content.Height));
        }
    }

    // The caption child is hit-test visible, so pointer cells over the label target the header
    // control rather than the expander itself; both count as hovering the header row.
    private bool IsPointerOverHeaderTarget =>
        IsPointerDirectlyOver || Header is { IsPointerDirectlyOver: true };

    private void UpdateHeaderPointerOver(PointerEventArgs eventArgs)
    {
        var value = IsPointerOverHeaderTarget &&
                    eventArgs.Pointer.Cells is { } cells &&
                    HeaderBounds.Contains(cells);

        SetHeaderPointerOver(value);
    }

    private void SetHeaderPointerOver(bool value)
    {
        if (_isHeaderPointerOver == value)
        {
            return;
        }

        _isHeaderPointerOver = value;
        InvalidateVisualState();
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);
        if (eventArgs is PointerEventArgs pointer)
        {
            UpdateHeaderPointerOver(pointer);
        }

        _interaction.Handle(eventArgs);
    }

    /// <inheritdoc/>
    internal override VisualState GetAppearanceState()
    {
        var state = base.GetAppearanceState();
        return _isHeaderPointerOver && IsPointerOverHeaderTarget
            ? state
            : state & ~VisualState.IsPointerOver;
    }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused) => base.OnFocusChanged(focused);

    /// <inheritdoc/>
    protected override void OnPointerOverChanged(bool isPointerOver, bool isPointerDirectlyOver)
    {
        base.OnPointerOverChanged(isPointerOver, isPointerDirectlyOver);
        _ = isPointerDirectlyOver;

        if (!isPointerOver)
        {
            SetHeaderPointerOver(false);
        }
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason) =>
        base.OnLostPointerCapture(reason);

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        if (reason == ReleaseReason.Disposed)
        {
            ExpandedChanged = null;
        }
    }
}
