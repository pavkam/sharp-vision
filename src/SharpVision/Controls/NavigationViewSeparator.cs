// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Draws one non-interactive separator entry inside a <see cref="NavigationView"/>.</summary>
public sealed class NavigationViewSeparator: Control
{
    /// <summary>Initializes a non-focusable and non-hit-testable separator.</summary>
    public NavigationViewSeparator() => IsHitTestVisible = false;

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

        for (var x = Bounds.X; x < Bounds.Right; x++)
        {
            _ = canvas.Draw("─".AsSpan(), new Point(x, Bounds.Y), ResolvedStyle, background: BackgroundMode.Transparent);
        }
    }
}
