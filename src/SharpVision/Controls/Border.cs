using SharpVision.Layout;
using SharpVision.Terminal.Geometry;

using BackgroundMode = SharpVision.Terminal.Rendering.BackgroundMode;
using TerminalCanvas = SharpVision.Terminal.Rendering.Canvas;

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
        get => BorderStyle;
        set => BorderStyle = value;
    }

    /// <inheritdoc/>
    protected override Size MeasureCore(Constraint constraint)
    {
        var child = Child;

        if (child is null)
        {
            return new Size(BorderThickness.Horizontal, BorderThickness.Vertical);
        }

        child.Measure(new Constraint(
            Subtract(constraint.Width, BorderThickness.Horizontal),
            Subtract(constraint.Height, BorderThickness.Vertical)));
        return new Size(
            Add(Add(child.DesiredSize.Width, child.Margin.Horizontal), BorderThickness.Horizontal),
            Add(Add(child.DesiredSize.Height, child.Margin.Vertical), BorderThickness.Vertical));
    }

    /// <inheritdoc/>
    protected override void ArrangeCore(Rect bounds)
    {
        if (Child is { } child)
        {
            child.Arrange(
                BorderThickness.Deflate(bounds),
                widthResolved: true,
                heightResolved: true);
        }
    }

    /// <inheritdoc/>
    protected override void RenderCore(TerminalCanvas canvas)
    {
        var opaque = ControlAppearance.HasOpaqueFill(this, GetVisualState());

        if (opaque)
        {
            canvas.Clear(Bounds, ResolvedStyle);
        }

        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var borderStyle = ControlAppearance.ResolveBorderStyle(this, GetVisualState());
        var background = opaque ? BackgroundMode.Opaque : BackgroundMode.Transparent;
        ControlChrome.DrawPartialBorder(
            canvas,
            Bounds,
            BorderThickness,
            BorderStyle,
            borderStyle,
            background,
            CellPolicy);
    }

    private static int Add(int left, int right)
    {
        var result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private static int? Subtract(int? value, int extent) => value.HasValue
        ? Math.Max(0, value.Value - extent)
        : null;
}
