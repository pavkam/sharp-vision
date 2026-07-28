// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Menus;

/// <summary>Draws one non-interactive separator entry inside a <see cref="Menu"/>.</summary>
[PublicAPI]
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
        get => _glyph ?? ControlGlyphs.Separators.Menu.Value;
        set
        {
            _ = new ControlGlyph(value, value);
            VerifyMutable();
            if (_glyph == value)
            {
                return;
            }

            _glyph = value;
            NotifyPropertyChanged(nameof(Glyph), InvalidationImpact.Render);
        }
    }

    /// <summary>Clears the local separator glyph to the code-owned default.</summary>
    /// <exception cref="InvalidOperationException">The attached separator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The separator is disposed.</exception>
    public void ResetGlyph()
    {
        VerifyMutable();

        if (_glyph.HasValue)
        {
            _glyph = null;
            NotifyPropertyChanged(nameof(Glyph), InvalidationImpact.Render);
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

        var themed = ControlGlyphs.Separators.Menu;
        var glyph = CellGlyphResolver.Resolve(Glyph, themed.Fallback, CellPolicy.AmbiguousWidth);
        ControlChrome.DrawHorizontalLine(canvas, Bounds, glyph, ResolvedStyle);
    }
}
