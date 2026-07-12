using System.Text;

using SharpVision.Layout;
using SharpVision.Terminal.Geometry;

using BackgroundMode = SharpVision.Terminal.Rendering.BackgroundMode;
using TerminalCanvas = SharpVision.Terminal.Rendering.Canvas;
using TerminalStyle = SharpVision.Terminal.Rendering.Style;

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
        var inherited = ResolvedStyle;
        var borderStyle = ControlAppearance.ResolveBorderStyle(this, GetVisualState());
        var opaque = ControlAppearance.HasOpaqueFill(this, GetVisualState());
        if (opaque)
        {
            canvas.Clear(Bounds, inherited);
        }

        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var mode = opaque ? BackgroundMode.Opaque : BackgroundMode.Transparent;
        DrawHorizontal(canvas, Bounds.Y, true, borderStyle, mode);
        if (Bounds.Height > 1)
        {
            DrawHorizontal(canvas, Bounds.Bottom - 1, false, borderStyle, mode);
        }

        DrawVertical(canvas, Bounds.X, true, borderStyle, mode);
        if (Bounds.Width > 1)
        {
            DrawVertical(canvas, Bounds.Right - 1, false, borderStyle, mode);
        }
    }

    private static int Add(int left, int right)
    {
        var result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private void DrawHorizontal(
        TerminalCanvas canvas,
        int y,
        bool top,
        TerminalStyle style,
        BackgroundMode background)
    {
        var active = top ? BorderThickness.Top != 0 : BorderThickness.Bottom != 0;

        if (!active)
        {
            return;
        }

        for (var x = Bounds.X; x < Bounds.Right; x++)
        {
            var glyph = top ? BorderStyle.Top : BorderStyle.Bottom;

            if (x == Bounds.X && BorderThickness.Left != 0)
            {
                glyph = top ? BorderStyle.TopLeft : BorderStyle.BottomLeft;
            }
            else if (x == Bounds.Right - 1 && BorderThickness.Right != 0)
            {
                glyph = top ? BorderStyle.TopRight : BorderStyle.BottomRight;
            }

            var fallback = x == Bounds.X || x == Bounds.Right - 1
                ? new Rune('+')
                : new Rune('-');
            canvas.DrawRune(
                CellGlyph.Resolve(glyph, fallback, CellPolicy.AmbiguousWidth),
                new Point(x, y),
                style,
                background);
        }
    }

    private void DrawVertical(
        TerminalCanvas canvas,
        int x,
        bool left,
        TerminalStyle style,
        BackgroundMode background)
    {
        var active = left ? BorderThickness.Left != 0 : BorderThickness.Right != 0;

        if (!active)
        {
            return;
        }

        var start = Bounds.Y + BorderThickness.Top;
        var end = Bounds.Bottom - BorderThickness.Bottom;

        for (var y = start; y < end; y++)
        {
            var glyph = left ? BorderStyle.Left : BorderStyle.Right;
            canvas.DrawRune(
                CellGlyph.Resolve(glyph, new Rune('|'), CellPolicy.AmbiguousWidth),
                new Point(x, y),
                style,
                background);
        }
    }

    private static int? Subtract(int? value, int extent) => value.HasValue
        ? Math.Max(0, value.Value - extent)
        : null;
}
