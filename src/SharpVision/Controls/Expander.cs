// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Displays a collapsible section with a focusable header toggle and optional content.</summary>
public sealed class Expander: ContentControl
{
    private readonly PressBehavior _interaction;
    private Rune? _collapsedGlyph;
    private Rune? _expandedGlyph;

    /// <summary>Initializes an expanded square semantic surface with an empty header.</summary>
    public Expander()
    {
        _interaction = new PressBehavior(
            () => new Rect(Bounds.X, Bounds.Y, Bounds.Width, Math.Min(1, Bounds.Height)),
            () => EffectiveIsEnabled && EffectiveIsVisible,
            () => FocusOwner is null || IsFocused, RequestFocus, CapturePointer,
            () => HasPointerCapture, ReleasePointerCapture, SetPressed,
            _ => IsExpanded = !IsExpanded);
        BorderThickness = new Thickness(1);
        BorderGlyphs = Glyphs.Light;
        Background = ColorRole.Surface;
        Focusable = true;
        TabStop = true;
    }

    /// <summary>Raised after the expanded state changes.</summary>
    public event EventHandler? ExpandedChanged;

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

            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    } = string.Empty;

    /// <summary>Gets or sets whether the content is visible.</summary>
    public bool IsExpanded
    {
        get;
        set { if (SetProperty(ref field, value, ChangeImpact.Measure)) { ExpandedChanged?.Invoke(this, EventArgs.Empty); } }
    } = true;

    /// <summary>Gets or sets the local collapsed-state indicator.</summary>
    public Rune CollapsedGlyph
    {
        get => _collapsedGlyph ?? ResolveThemeGlyphs().Disclosure.Collapsed.Value;
        set => SetGlyph(ref _collapsedGlyph, value, nameof(CollapsedGlyph));
    }

    /// <summary>Gets or sets the local expanded-state indicator.</summary>
    public Rune ExpandedGlyph
    {
        get => _expandedGlyph ?? ResolveThemeGlyphs().Disclosure.Expanded.Value;
        set => SetGlyph(ref _expandedGlyph, value, nameof(ExpandedGlyph));
    }

    /// <summary>Clears local disclosure indicators so the active theme supplies them.</summary>
    public void ResetGlyphs()
    {
        VerifyMutable();
        if (!_collapsedGlyph.HasValue && !_expandedGlyph.HasValue) { return; }
        _collapsedGlyph = null;
        _expandedGlyph = null;
        NotifyPropertyChanged(nameof(CollapsedGlyph), ChangeImpact.Render);
        NotifyPropertyChanged(nameof(ExpandedGlyph), ChangeImpact.Render);
    }

    /// <inheritdoc/>

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var hw = (int) Math.Min(int.MaxValue, 2L + Terminal.Unicode.Width.Measure(Header).Cells);
        if (!IsExpanded || Content is not { } child) { return new Size(hw, 1); }
        var d = MeasureChild(child, new Constraint(constraint.Width, constraint.Height.HasValue ? Math.Max(0, constraint.Height.Value - 1) : null));
        var cw = child.Visibility == Visibility.Collapsed ? 0 : (int) Math.Min(int.MaxValue, (long) d.Width + child.Margin.Horizontal);
        var ch = child.Visibility == Visibility.Collapsed ? 0 : (int) Math.Min(int.MaxValue, (long) d.Height + child.Margin.Vertical);
        return new Size(Math.Max(hw, cw), (int) Math.Min(int.MaxValue, 1L + ch));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (Content is not { } content)
        {
            return;
        }

        var slot = IsExpanded && bounds.Height > 1
            ? new Rect(bounds.X, bounds.Y + 1, bounds.Width, bounds.Height - 1)
            : default;
        ArrangeChild(content, slot, ResolvedAxes.Both);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0) { return; }
        var content = ContentBounds;
        var s = ResolvedStyle;
        var themed = IsExpanded ? ResolveThemeGlyphs().Disclosure.Expanded : ResolveThemeGlyphs().Disclosure.Collapsed;
        var selected = IsExpanded ? ExpandedGlyph : CollapsedGlyph;
        canvas.DrawRune(
            CellGlyph.Resolve(selected, themed.Fallback, CellPolicy.AmbiguousWidth),
            new Point(content.X, content.Y),
            s,
            BackgroundMode.Transparent);
        if (Header.Length > 0 && content.Width > 2) { _ = canvas.Clip(new Rect(content.X + 2, content.Y, content.Width - 2, 1)).Draw(Header.AsSpan(), new Point(content.X + 2, content.Y), s, background: BackgroundMode.Transparent); }
    }

    private void SetGlyph(ref Rune? storage, Rune value, string propertyName)
    {
        _ = new ThemedGlyph(value, value);
        VerifyMutable();
        if (storage == value) { return; }
        storage = value;
        NotifyPropertyChanged(propertyName, ChangeImpact.Render);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs) { base.OnEvent(eventArgs); _interaction.Handle(eventArgs); }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused) { base.OnFocusChanged(focused); _interaction.FocusChanged(focused); }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason) { base.OnLostPointerCapture(reason); _interaction.CaptureLost(); }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason) { base.OnUnavailable(reason); _interaction.Unavailable(); if (reason == ReleaseReason.Disposed) { ExpandedChanged = null; } }
}
