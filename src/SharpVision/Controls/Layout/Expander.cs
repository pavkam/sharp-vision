// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Displays a collapsible section with a focusable header toggle and optional content.</summary>
[PublicAPI]
public sealed class Expander: ContentControl
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

    /// <summary>Gets or sets the non-null header label.</summary>
    public string Header
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (Terminal.Unicode.Width.Measure(value).Controls > 0)
            {
                throw new ArgumentException("An expander header cannot contain terminal controls.", nameof(value));
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = string.Empty;

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

    /// <inheritdoc/>
    protected override string? AccessKeyText => Header;

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
    protected override void OnContentChanged(Control? previous, Control? current)
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
        var hw = (int) Math.Min(
            int.MaxValue,
            (long) _headerChromeWidthCells +
            SharpVision.Input.AccessKeyText.Measure(Header, CellPolicy.AmbiguousWidth, UseMnemonic));
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

        var themed = IsExpanded ? ControlGlyphs.Disclosure.Expanded : ControlGlyphs.Disclosure.Collapsed;
        var selected = IsExpanded ? ExpandedGlyph : CollapsedGlyph;
        canvas.DrawRune(
            CellGlyphResolver.Resolve(selected, themed.Fallback, CellPolicy.AmbiguousWidth),
            new Point(content.X, content.Y),
            s,
            background);
        if (Header.Length > 0 && content.Width > _headerChromeWidthCells)
        {
            _ = SharpVision.Input.AccessKeyText.Draw(
                canvas.Clip(new Rect(
                    content.X + _headerChromeWidthCells,
                    content.Y,
                    content.Width - _headerChromeWidthCells,
                    _headerHeightCells)),
                Header,
                new Point(content.X + _headerChromeWidthCells, content.Y),
                s,
                background,
                CellPolicy.AmbiguousWidth,
                UseMnemonic,
                EffectiveIsEnabled ? Theme?.Hotkey ?? Color.Default : null);
        }
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
