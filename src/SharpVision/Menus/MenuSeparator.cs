// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Menus;

/// <summary>Draws one non-interactive separator entry inside a <see cref="Menu"/>.</summary>
[PublicAPI]
public sealed class MenuSeparator: Control<MenuSeparatorStyle>
{

    /// <summary>Initializes a non-focusable and non-hit-testable separator.</summary>
    public MenuSeparator() : base(MenuSeparatorStyle.Definition)
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HitTestVisible = false;
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

        var glyph = ActualStyle.Glyph.Resolve(ControlGlyphs.Separators.Menu.Fallback, CellPolicy.AmbiguousWidth);
        canvas.DrawHorizontalLine(Bounds, glyph, ResolvedStyle);
    }
}
