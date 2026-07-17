// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Draws a non-interactive horizontal or vertical divider line.</summary>
public sealed class Separator: Control
{
    private Rune? _horizontalGlyph;
    private Rune? _verticalGlyph;

    /// <summary>Initializes a non-focusable horizontal separator.</summary>
    public Separator()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        IsHitTestVisible = false;
    }

    /// <summary>Gets or sets the separator orientation.</summary>
    public Orientation Orientation
    {
        get;
        set { if (!Enum.IsDefined(value)) { throw new ArgumentOutOfRangeException(nameof(value)); } _ = SetProperty(ref field, value, ChangeImpact.Measure); }
    } = Orientation.Horizontal;

    /// <summary>Gets or sets the local horizontal separator glyph.</summary>
    public Rune HorizontalGlyph { get => _horizontalGlyph ?? ResolveThemeGlyphs().Separators.Horizontal.Value; set => SetGlyph(ref _horizontalGlyph, value, nameof(HorizontalGlyph)); }

    /// <summary>Gets or sets the local vertical separator glyph.</summary>
    public Rune VerticalGlyph { get => _verticalGlyph ?? ResolveThemeGlyphs().Separators.Vertical.Value; set => SetGlyph(ref _verticalGlyph, value, nameof(VerticalGlyph)); }

    /// <summary>Clears both local separator glyphs so the active theme supplies them.</summary>
    /// <exception cref="InvalidOperationException">The attached separator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The separator is disposed.</exception>
    public void ResetGlyphs()
    {
        VerifyMutable();

        if (_horizontalGlyph.HasValue)
        {
            _horizontalGlyph = null;
            NotifyPropertyChanged(nameof(HorizontalGlyph), ChangeImpact.Render);
        }

        if (_verticalGlyph.HasValue)
        {
            _verticalGlyph = null;
            NotifyPropertyChanged(nameof(VerticalGlyph), ChangeImpact.Render);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) { _ = constraint; return new Size(1, 1); }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0) { return; }
        var s = ResolvedStyle;
        var themed = Orientation == Orientation.Horizontal ? ResolveThemeGlyphs().Separators.Horizontal : ResolveThemeGlyphs().Separators.Vertical;
        var selected = Orientation == Orientation.Horizontal ? HorizontalGlyph : VerticalGlyph;
        var glyph = CellGlyph.Resolve(selected, themed.Fallback, CellPolicy.AmbiguousWidth);
        if (Orientation == Orientation.Horizontal) { for (var x = Bounds.X; x < Bounds.Right; x++) { canvas.DrawRune(glyph, new Point(x, Bounds.Y), s, BackgroundMode.Transparent); } }
        else { for (var y = Bounds.Y; y < Bounds.Bottom; y++) { canvas.DrawRune(glyph, new Point(Bounds.X, y), s, BackgroundMode.Transparent); } }
    }

    private void SetGlyph(ref Rune? storage, Rune value, string propertyName)
    {
        _ = new ThemedGlyph(value, value);
        VerifyMutable();
        if (storage == value) { return; }
        storage = value;
        NotifyPropertyChanged(propertyName, ChangeImpact.Render);
    }
}
