// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

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
    private bool _isHeaderPointerOver;
    private Visibility? _requestedContentVisibility;

    /// <summary>Initializes an expanded borderless section with an empty header.</summary>
    public Expander()
    {
        _style = InitializeStyle(ExpanderStyle.Definition);
        _interaction = new PressBehavior(
            () => HeaderBounds,
            () => EffectiveIsEnabled && EffectiveIsVisible,
            () => FocusOwner is null || IsFocused, RequestFocus, CapturePointer,
            () => HasPointerCapture, ReleasePointerCapture, SetPressed,
            _ => IsExpanded = !IsExpanded,
            () => Capabilities.KeyReleaseEvents.Authoritative);
        IsFocusable = true;
        IsTabStop = true;
    }

    /// <summary>Raised after the expanded state changes.</summary>
    public event EventHandler<ExpandedChangedEventArgs>? ExpandedChanged;

    /// <summary>Gets or sets whether the content is visible.</summary>
    public bool IsExpanded
    {
        get;
        set
        {
            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                ApplyContentVisibility();
                ExpandedChanged?.Invoke(this, new ExpandedChangedEventArgs(value));
            }
        }
    } = true;

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
                : (int) Math.Min(int.MaxValue, (long) hd.Width + header.Margin.Horizontal);
        }

        var hw = (int) Math.Min(int.MaxValue, (long) HeaderChromeWidth + headerContentWidth);
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
            : (int) Math.Min(int.MaxValue, (long) d.Width + child.Margin.Horizontal);
        var ch = child.Visibility == Visibility.Collapsed
            ? 0
            : (int) Math.Min(int.MaxValue, (long) d.Height + child.Margin.Vertical);
        var indentedWidth = (int) Math.Min(int.MaxValue, (long) ActualStyle.ContentIndent + cw);
        return new Size(
            Math.Max(hw, indentedWidth),
            (int) Math.Min(int.MaxValue, (long) _headerHeightCells + ch));
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
        var s = ResolvedStyle;
        var background = BackgroundMode.Transparent;

        if (IsFocused)
        {
            background = BackgroundMode.Opaque;
        }

        if (IsFocused && content.Width > 0)
        {
            canvas.Clear(new Rect(content.X, content.Y, content.Width, _headerHeightCells), s);
        }

        var themed = IsExpanded ? ControlGlyphs.Disclosure.Expanded : ControlGlyphs.Disclosure.Collapsed;
        var style = ActualStyle;
        var selected = IsExpanded ? style.ExpandedGlyph : style.CollapsedGlyph;
        canvas.DrawRune(
            selected.Resolve(themed.Fallback, CellPolicy.AmbiguousWidth),
            new Point(content.X, content.Y),
            s,
            background);

        if (Header is DisplayText text && content.Width > HeaderChromeWidth)
        {
            var caption = canvas.Clip(HeaderBounds);
            _ = text.Content.Draw(
                caption,
                new Point(content.X + HeaderChromeWidth, content.Y),
                s,
                background,
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
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);
        _interaction.FocusChanged(focused);
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason)
    {
        base.OnLostPointerCapture(reason);
        _interaction.CaptureLost();
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        _interaction.Unavailable();
        if (reason == ReleaseReason.Disposed)
        {
            ExpandedChanged = null;
        }
    }
}
