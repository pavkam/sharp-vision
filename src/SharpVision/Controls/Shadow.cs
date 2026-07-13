namespace SharpVision.Controls;

using System.Text;

using SharpVision.Layout;
using SharpVision.Terminal.Geometry;

using BackgroundMode = Terminal.Rendering.BackgroundMode;
using TerminalAttributes = Terminal.Rendering.Attributes;
using TerminalCanvas = Terminal.Rendering.Canvas;

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
    protected override Rect VisualBounds =>
        ControlChrome.Union(Bounds, ControlChrome.Shift(Bounds, ShadowOffset));

    /// <inheritdoc/>
    protected override void RenderCore(TerminalCanvas canvas)
    {
        var background = ControlAppearance.HasOpaqueFill(this, GetVisualState())
            ? BackgroundMode.Opaque
            : BackgroundMode.Transparent;
        ControlChrome.DrawShadow(
            canvas,
            this,
            Bounds,
            Bounds,
            background,
            ResolvedStyle);
    }

    #endregion

    private static int Add(int left, int right)
    {
        var result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }
}
