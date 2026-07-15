// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Draws one non-interactive separator entry inside a <see cref="Menu"/>.</summary>
public sealed class MenuSeparator: Control
{
    /// <summary>Initializes a non-focusable and non-hit-testable separator.</summary>
    public MenuSeparator() => IsHitTestVisible = false;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(3, 1);
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        for (var x = Bounds.X; x < Bounds.Right; x++)
        {
            _ = canvas.Draw(
                "─".AsSpan(),
                new Point(x, Bounds.Y),
                ResolvedStyle,
                background: BackgroundMode.Transparent);
        }
    }
}
