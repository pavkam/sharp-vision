// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;


/// <summary>Owns one child and draws validated physical border edges around it.</summary>
public sealed class Border: Container
{
    /// <summary>Initializes an empty capacity-one Border.</summary>
    public Border() : base(1) => HorizontalAlignment = HorizontalAlignment.Stretch;

    /// <summary>Gets or atomically sets the only managed child.</summary>
    /// <exception cref="ArgumentException">The value cannot be owned by this Border.</exception>
    /// <exception cref="InvalidOperationException">The attached Border is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Border or value is disposed.</exception>
    public Control? Child
    {
        get => Children.Count == 0 ? null : Children[0];
        set => Children.SetOnly(value);
    }

    /// <summary>Gets or sets the validated physical glyph family used by the border edges.</summary>
    /// <exception cref="InvalidOperationException">The attached border is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The border is disposed.</exception>
    public Glyphs Glyphs
    {
        get => BorderGlyphs;
        set => BorderGlyphs = value;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        Control? child = Child;

        if (child is null)
        {
            return default;
        }

        child.Measure(constraint);
        return new Size(
            Add(child.DesiredSize.Width, child.Margin.Horizontal),
            Add(child.DesiredSize.Height, child.Margin.Vertical));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) =>
        Child?.Arrange(bounds, widthResolved: true, heightResolved: true);

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        bool opaque = ControlAppearance.HasOpaqueFill(this, GetVisualState());

        if (opaque)
        {
            canvas.Clear(Bounds, ResolvedStyle);
        }

        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        TerminalStyle borderStyle = ControlAppearance.ResolveBorderStyle(this, GetVisualState());
        BackgroundMode background = opaque ? BackgroundMode.Opaque : BackgroundMode.Transparent;
        ControlChrome.DrawPartialBorder(
            canvas,
            Bounds,
            BorderThickness,
            BorderGlyphs,
            borderStyle,
            background,
            CellPolicy);
    }

    private static int Add(int left, int right)
    {
        Debug.Assert(left >= 0, "Border accumulation uses non-negative extents.");
        Debug.Assert(right >= 0, "Border accumulation uses non-negative extents.");

        long result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }
}
