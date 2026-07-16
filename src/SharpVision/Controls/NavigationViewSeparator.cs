// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Draws one non-interactive separator entry inside a NavigationView.</summary>
public sealed class NavigationViewSeparator: Control
{
    /// <summary>Initializes a non-focusable and non-hit-testable separator.</summary>
    public NavigationViewSeparator()
    {
        CanFocus = false;
        IsHitTestVisible = false;
        Height = Length.Cells(1);
        HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(1, 1);
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        canvas.DrawLine(
            new Point(Bounds.X, Bounds.Y),
            new Point(Bounds.Right - 1, Bounds.Y),
            new Rune('─'),
            ResolvedStyle);
    }
}
