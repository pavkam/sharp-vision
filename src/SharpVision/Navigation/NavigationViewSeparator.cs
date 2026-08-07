// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

/// <summary>Draws one non-interactive separator entry inside a <see cref="NavigationView"/>.</summary>
[PublicAPI]
public sealed class NavigationViewSeparator: Control<NavigationViewSeparatorStyle>
{

    /// <summary>Initializes a non-focusable and non-hit-testable separator.</summary>
    public NavigationViewSeparator() : base(NavigationViewSeparatorStyle.Definition)
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HitTestVisible = false;
    }

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
