// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

/// <summary>Draws one non-interactive separator entry inside a <see cref="NavigationView"/>.</summary>
[PublicAPI]
public sealed class NavigationViewSeparator: ControlBase, IStyled<NavigationViewSeparatorStyle>
{
    private readonly StyleSlot<NavigationViewSeparatorStyle> _style;

    /// <summary>Initializes a non-focusable and non-hit-testable separator.</summary>
    public NavigationViewSeparator()
    {
        _style = InitializeStyle(NavigationViewSeparatorStyle.Definition);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        IsHitTestVisible = false;
    }

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached separator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The separator is disposed.</exception>
    public NavigationViewSeparatorStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public NavigationViewSeparatorStyle ActualStyle => _style.Actual;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(1, 1);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var glyph = ActualStyle.Glyph.Resolve(ControlGlyphs.Navigation.Separator.Fallback, CellPolicy.AmbiguousWidth);
        canvas.DrawHorizontalLine(Bounds, glyph, ResolvedStyle);
    }
}
