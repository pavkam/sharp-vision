// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Draws one non-interactive separator entry inside a <see cref="Menu"/>.</summary>
public sealed class MenuSeparator: Control
{
    private Rune? _glyph;

    /// <summary>Initializes a non-focusable and non-hit-testable separator.</summary>
    public MenuSeparator()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        IsHitTestVisible = false;
    }

    /// <summary>Gets or sets the local separator glyph.</summary>
    public Rune Glyph
    {
        get => _glyph ?? ResolveThemeGlyphs().Separators.Menu.Value;
        set
        {
            _ = new ThemedGlyph(value, value);
            VerifyMutable();
            if (_glyph == value) { return; }
            _glyph = value;
            NotifyPropertyChanged(nameof(Glyph), ChangeImpact.Render);
        }
    }

    /// <summary>Clears the local separator glyph so the active theme supplies it.</summary>
    /// <exception cref="InvalidOperationException">The attached separator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The separator is disposed.</exception>
    public void ResetGlyph()
    {
        VerifyMutable();

        if (_glyph.HasValue)
        {
            _glyph = null;
            NotifyPropertyChanged(nameof(Glyph), ChangeImpact.Render);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(3, 1);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var themed = ResolveThemeGlyphs().Separators.Menu;
        var glyph = CellGlyph.Resolve(Glyph, themed.Fallback, CellPolicy.AmbiguousWidth);

        for (var x = Bounds.X; x < Bounds.Right; x++)
        {
            canvas.DrawRune(
                glyph,
                new Point(x, Bounds.Y),
                ResolvedStyle,
                BackgroundMode.Transparent);
        }
    }
}
