// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Displays a collapsible section with a focusable header toggle and optional content.</summary>
[PublicAPI]
public sealed class Expander: HeaderedContentControl
{
    // The header occupies one terminal row and reserves one cell for the
    // disclosure glyph plus one cell before the label.
    private const int _headerHeightCells = 1;
    private const int _headerChromeWidthCells = 2;

    private readonly PressBehavior _interaction;
    private Rune? _collapsedGlyph;
    private Rune? _expandedGlyph;
    private bool _isHeaderPointerOver;
    private Visibility? _requestedContentVisibility;

    /// <summary>Initializes an expanded borderless section with an empty header.</summary>
    public Expander()
    {
        _interaction = new PressBehavior(
            () => HeaderBounds,
            () => EffectiveIsEnabled && EffectiveIsVisible,
            () => FocusOwner is null || IsFocused, RequestFocus, CapturePointer,
            () => HasPointerCapture, ReleasePointerCapture, SetPressed,
            _ => IsExpanded = !IsExpanded);
        Focusable = true;
        TabStop = true;
    }

    /// <summary>Raised after the expanded state changes.</summary>
    public event EventHandler<ExpandedChangedEventArgs>? ExpandedChanged;

    /// <summary>Gets or sets the number of cells used to indent expanded content below the header.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached Expander is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Expander is disposed.</exception>
    public int ContentIndent
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = 2;

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

    /// <summary>Gets or sets the local collapsed-state indicator.</summary>
    public Rune CollapsedGlyph
    {
        get => _collapsedGlyph ?? ControlGlyphs.Disclosure.Collapsed.Value;
        set => SetOptionalGlyph(ref _collapsedGlyph, value, nameof(CollapsedGlyph));
    }

    /// <summary>Gets or sets the local expanded-state indicator.</summary>
    public Rune ExpandedGlyph
    {
        get => _expandedGlyph ?? ControlGlyphs.Disclosure.Expanded.Value;
        set => SetOptionalGlyph(ref _expandedGlyph, value, nameof(ExpandedGlyph));
    }

    /// <summary>Clears local disclosure indicators to the code-owned defaults.</summary>
    public void ResetGlyphs()
    {
        VerifyMutable();
        _ = ResetOptionalGlyph(ref _collapsedGlyph, nameof(CollapsedGlyph));
        _ = ResetOptionalGlyph(ref _expandedGlyph, nameof(ExpandedGlyph));
    }

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
    // be Visible, so content the caller already collapsed for its own
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

        var hw = (int) Math.Min(int.MaxValue, (long) _headerChromeWidthCells + headerContentWidth);
        if (!IsExpanded || Content is not { } child)
        {
            return new Size(hw, _headerHeightCells);
        }

        var d = MeasureChild(child,
            new Constraint(
                constraint.Width.HasValue ? Math.Max(0, constraint.Width.Value - ContentIndent) : null,
                constraint.Height.HasValue ? Math.Max(0, constraint.Height.Value - _headerHeightCells) : null));
        var cw = child.Visibility == Visibility.Collapsed
            ? 0
            : (int) Math.Min(int.MaxValue, (long) d.Width + child.Margin.Horizontal);
        var ch = child.Visibility == Visibility.Collapsed
            ? 0
            : (int) Math.Min(int.MaxValue, (long) d.Height + child.Margin.Vertical);
        var indentedWidth = (int) Math.Min(int.MaxValue, (long) ContentIndent + cw);
        return new Size(
            Math.Max(hw, indentedWidth),
            (int) Math.Min(int.MaxValue, (long) _headerHeightCells + ch));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (Header is { } header)
        {
            var headerSlot = bounds.Width > _headerChromeWidthCells
                ? new Rect(
                    bounds.X + _headerChromeWidthCells,
                    bounds.Y,
                    bounds.Width - _headerChromeWidthCells,
                    Math.Min(_headerHeightCells, bounds.Height))
                : default;
            ArrangeChild(header, headerSlot, ResolvedAxes.Height);
        }

        if (Content is not { } content)
        {
            return;
        }

        var indent = Math.Min(ContentIndent, bounds.Width);
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
            s = new TerminalStyle(s.Foreground, Theme?.ResolveColor(ThemeColor.FocusedControl) ?? Color.Default,
                s.Attributes | TerminalAttributes.Bold, s.Hyperlink, s.Underline, s.UnderlineColor);
            background = BackgroundMode.Opaque;
        }

        if (IsFocused && content.Width > 0)
        {
            canvas.Clear(new Rect(content.X, content.Y, content.Width, _headerHeightCells), s);
        }

        var themed = IsExpanded ? ControlGlyphs.Disclosure.Expanded : ControlGlyphs.Disclosure.Collapsed;
        var selected = IsExpanded ? ExpandedGlyph : CollapsedGlyph;
        canvas.DrawRune(
            selected.Resolve(themed.Fallback, CellPolicy.AmbiguousWidth),
            new Point(content.X, content.Y),
            s,
            background);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The focused header background resolves <see cref="ThemeColor.FocusedControl"/> directly at
    /// render time rather than through a style contract (see #161), so the base role-profile
    /// comparison alone cannot detect a theme swap confined to that role.
    /// </remarks>
    protected override InvalidationImpact GetThemeChangeImpact(
        Theme? previous,
        Theme? current,
        Face? previousParentAmbientFace,
        Face? currentParentAmbientFace)
    {
        var baseImpact = base.GetThemeChangeImpact(
            previous,
            current,
            previousParentAmbientFace,
            currentParentAmbientFace);
        var colorImpact = ResolveDirectColor(previous, ThemeColor.FocusedControl) != ResolveDirectColor(current, ThemeColor.FocusedControl)
            ? InvalidationImpact.Render
            : InvalidationImpact.None;

        return MaximumImpact(baseImpact, colorImpact);
    }

    private static Color ResolveDirectColor(Theme? theme, ThemeColor color) =>
        theme?.ResolveColor(color) ?? Color.Default;

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

    private void UpdateHeaderPointerOver(PointerEventArgs eventArgs)
    {
        var value = IsPointerDirectlyOver &&
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
        return _isHeaderPointerOver && IsPointerDirectlyOver
            ? state
            : state & ~VisualState.PointerOver;
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
