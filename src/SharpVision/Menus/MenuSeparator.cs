// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Menus;

/// <summary>Draws one non-interactive separator entry inside a <see cref="Menu"/>.</summary>
[PublicAPI]
public sealed class MenuSeparator: ControlBase, IStyled<MenuSeparatorStyle>
{
    private readonly StyleSlot<MenuSeparatorStyle> _style;

    /// <summary>Initializes a non-focusable and non-hit-testable separator.</summary>
    public MenuSeparator()
    {
        _style = InitializeStyle(MenuSeparatorStyle.Definition);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        IsHitTestVisible = false;
    }

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached separator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The separator is disposed.</exception>
    public MenuSeparatorStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public MenuSeparatorStyle ActualStyle => _style.Actual;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return IsHorizontalMenu ? new Size(1, 1) : new Size(3, 1);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        if (IsHorizontalMenu)
        {
            var vertical = ActualStyle.VerticalGlyph.Resolve(
                ControlGlyphs.Separators.Vertical.Fallback,
                CellPolicy.AmbiguousWidth);
            canvas.DrawRune(
                vertical,
                new Point(Bounds.X, Bounds.Y),
                ResolvedStyle,
                BackgroundMode.Transparent);
            return;
        }

        var horizontal = ActualStyle.Glyph.Resolve(
            ControlGlyphs.Separators.Menu.Fallback,
            CellPolicy.AmbiguousWidth);
        canvas.DrawHorizontalLine(Bounds, horizontal, ResolvedStyle);
    }

    private bool IsHorizontalMenu => FindAncestor<Menu>()?.Orientation == Orientation.Horizontal;
}
