// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Draws a non-interactive horizontal or vertical divider line.</summary>
public sealed class Separator: Control
{
    /// <summary>Initializes a non-focusable horizontal separator.</summary>
    public Separator() => IsHitTestVisible = false;

    /// <summary>Gets or sets the separator orientation.</summary>
    public Orientation Orientation
    {
        get;
        set { if (!Enum.IsDefined(value)) { throw new ArgumentOutOfRangeException(nameof(value)); } _ = SetProperty(ref field, value, ChangeImpact.Measure); }
    } = Orientation.Horizontal;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) { _ = constraint; return new Size(1, 1); }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0) { return; }
        var s = ResolvedStyle;
        if (Orientation == Orientation.Horizontal) { for (var x = Bounds.X; x < Bounds.Right; x++) { _ = canvas.Draw("─".AsSpan(), new Point(x, Bounds.Y), s, background: BackgroundMode.Transparent); } }
        else { for (var y = Bounds.Y; y < Bounds.Bottom; y++) { _ = canvas.Draw("│".AsSpan(), new Point(Bounds.X, y), s, background: BackgroundMode.Transparent); } }
    }
}
