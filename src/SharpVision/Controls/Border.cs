using System.Text;

using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;

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

    /// <summary>Gets or sets zero-or-one physical edge thicknesses.</summary>
    /// <exception cref="ArgumentOutOfRangeException">An edge exceeds one cell.</exception>
    /// <exception cref="InvalidOperationException">The attached Border is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Border is disposed.</exception>
    public Thickness BorderThickness
    {
        get;
        set
        {
            if (value.Left > 1 || value.Top > 1 || value.Right > 1 || value.Bottom > 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Every border edge must be zero or one cell.");
            }

            _ = Set(ref field, value, Invalidation.Measure);
        }
    }

    /// <summary>Gets or sets the validated physical border glyph set.</summary>
    /// <exception cref="InvalidOperationException">The attached Border is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Border is disposed.</exception>
    public Glyphs Glyphs
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    } = Glyphs.Default;

    /// <summary>Gets or sets an optional direct border-color override.</summary>
    /// <exception cref="InvalidOperationException">The attached Border is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Border is disposed.</exception>
    public Color? BorderColor
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    }

    /// <summary>Gets or sets an optional direct background override.</summary>
    /// <exception cref="InvalidOperationException">The attached Border is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Border is disposed.</exception>
    public Color? Background
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    }

    /// <summary>Gets or sets optional direct border rendition attributes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown flags.</exception>
    /// <exception cref="InvalidOperationException">The attached Border is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Border is disposed.</exception>
    public Attributes? Attributes
    {
        get;
        set
        {
            if (value.HasValue)
            {
                _ = new TerminalStyle(attributes: value.Value);
            }

            _ = Set(ref field, value, Invalidation.Render);
        }
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
        var background = Background ?? inherited.Background;
        var attributes = Attributes ?? inherited.Attributes;
        var opaque = Background.HasValue || Appearance.Background.HasValue;

        if (opaque)
        {
            canvas.Clear(Bounds, new TerminalStyle(inherited.Foreground, background, attributes));
        }

        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var color = BorderColor ?? Appearance.BorderColor ?? inherited.Foreground;
        var style = new TerminalStyle(color, background, attributes);
        var backgroundMode = opaque ? BackgroundMode.Opaque : BackgroundMode.Transparent;
        DrawHorizontal(canvas, Bounds.Y, top: true, style, backgroundMode);

        if (Bounds.Height > 1)
        {
            DrawHorizontal(canvas, Bounds.Bottom - 1, top: false, style, backgroundMode);
        }

        DrawVertical(canvas, Bounds.X, left: true, style, backgroundMode);

        if (Bounds.Width > 1)
        {
            DrawVertical(canvas, Bounds.Right - 1, left: false, style, backgroundMode);
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
            var glyph = top ? Glyphs.Top : Glyphs.Bottom;

            if (x == Bounds.X && BorderThickness.Left != 0)
            {
                glyph = top ? Glyphs.TopLeft : Glyphs.BottomLeft;
            }
            else if (x == Bounds.Right - 1 && BorderThickness.Right != 0)
            {
                glyph = top ? Glyphs.TopRight : Glyphs.BottomRight;
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
            var glyph = left ? Glyphs.Left : Glyphs.Right;
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
