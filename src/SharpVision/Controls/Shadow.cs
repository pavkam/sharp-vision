using System.Diagnostics;
using System.Text;

using SharpVision.Layout;
using SharpVision.Terminal.Geometry;

using BackgroundMode = SharpVision.Terminal.Rendering.BackgroundMode;
using TerminalAttributes = SharpVision.Terminal.Rendering.Attributes;
using TerminalCanvas = SharpVision.Terminal.Rendering.Canvas;
using TerminalStyle = SharpVision.Terminal.Rendering.Style;

namespace SharpVision.Controls;

/// <summary>Decorates one child with composite or block-glyph visual overflow.</summary>
public sealed partial class Shadow: Container
{
    #region Construction and properties

    static Shadow()
    {
        _ = ShadowAttributesProperty.RegisterClassDefault<Shadow>(TerminalAttributes.Dim);
        _ = ShadowOffsetProperty.RegisterClassDefault<Shadow>(new Point(2, 1));
    }

    /// <summary>Initializes an empty capacity-one shadow with Turbo Vision geometry.</summary>
    public Shadow() : base(1)
    {
    }

    /// <summary>Gets or atomically sets the only managed child.</summary>
    /// <exception cref="ArgumentException">The value cannot be owned by this Shadow.</exception>
    /// <exception cref="InvalidOperationException">The attached Shadow is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Shadow or value is disposed.</exception>
    public Control? Child
    {
        get => Children.Count == 0 ? null : Children[0];
        set => Children.SetOnly(value);
    }

    /// <summary>Gets or sets the cell mutation mode.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached Shadow is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Shadow is disposed.</exception>
    public ShadowMode Mode
    {
        get => ShadowMode;
        set => ShadowMode = value;
    }

    /// <summary>Gets or sets the signed visual offset in terminal cells.</summary>
    /// <exception cref="InvalidOperationException">The attached Shadow is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Shadow is disposed.</exception>
    public Point Offset
    {
        get => ShadowOffset;
        set => ShadowOffset = value;
    }

    /// <summary>Gets or sets the printable narrow block-mode Rune.</summary>
    /// <exception cref="ArgumentException">The value is a control or is not one cell wide.</exception>
    /// <exception cref="InvalidOperationException">The attached Shadow is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Shadow is disposed.</exception>
    public Rune Glyph
    {
        get => ShadowGlyph;
        set => ShadowGlyph = value;
    }

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    protected override Rect VisualBounds => Union(Bounds, Shift(Bounds, ShadowOffset));

    /// <inheritdoc/>
    protected override Size MeasureCore(Constraint constraint)
    {
        var child = Child;

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
    protected override void ArrangeCore(Rect bounds) =>
        Child?.Arrange(bounds, widthResolved: true, heightResolved: true);

    /// <inheritdoc/>
    protected override void RenderCore(TerminalCanvas canvas)
    {
        var shifted = Shift(Bounds, ShadowOffset);
        var target = shifted.Intersect(canvas.Bounds);
        var style = ResolveShadowStyle();
        var background = ControlAppearance.HasOpaqueFill(this, GetVisualState())
            ? BackgroundMode.Opaque
            : BackgroundMode.Transparent;

        // The visual shadow is the translated rectangle minus the opaque body.
        // This yields Turbo Vision's right and bottom strips for offset (2, 1)
        // and the symmetric top and left strips for negative offsets.
        for (var y = target.Y; y < target.Bottom; y++)
        {
            for (var x = target.X; x < target.Right; x++)
            {
                var point = new Point(x, y);

                if (Bounds.Contains(point))
                {
                    continue;
                }

                if (ShadowMode == ShadowMode.Composite)
                {
                    canvas.ApplyStyle(new Rect(x, y, 1, 1), style, background);
                }
                else
                {
                    Debug.Assert(ShadowMode == ShadowMode.BlockGlyph, "Public validation limits shadow modes.");
                    canvas.DrawRune(
                        CellGlyph.Resolve(ShadowGlyph, new Rune('#'), CellPolicy.AmbiguousWidth),
                        point,
                        style,
                        background);
                }
            }
        }
    }

    #endregion

    private TerminalStyle ResolveShadowStyle()
    {
        var inherited = ResolvedStyle;
        var (attributes, underline, underlineColor) = Decoration.Resolve(inherited, ShadowAttributes);
        return new TerminalStyle(ShadowForeground ?? inherited.Foreground, ShadowBackground ?? inherited.Background, attributes, inherited.Hyperlink, underline, underlineColor);
    }

    private static int Add(int left, int right)
    {
        var result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private static Rect Shift(Rect value, Point offset) => new(
        SaturatingAdd(value.X, offset.X),
        SaturatingAdd(value.Y, offset.Y),
        value.Width,
        value.Height);

    private static Rect Union(Rect left, Rect right)
    {
        var x = Math.Min(left.X, right.X);
        var y = Math.Min(left.Y, right.Y);
        var outerRight = Math.Max(left.Right, right.Right);
        var bottom = Math.Max(left.Bottom, right.Bottom);
        return new Rect(x, y, Extent(x, outerRight), Extent(y, bottom));
    }

    private static int Extent(int start, int end) =>
        (int) Math.Min(int.MaxValue, Math.Max(0L, (long) end - start));

    private static int SaturatingAdd(int left, int right) =>
        (int) Math.Clamp((long) left + right, int.MinValue, int.MaxValue);
}
