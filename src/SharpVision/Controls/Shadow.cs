using System.Diagnostics;
using System.Text;

using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Protocols;

using BackgroundMode = SharpVision.Terminal.Rendering.BackgroundMode;
using TerminalAttributes = SharpVision.Terminal.Rendering.Attributes;
using TerminalCanvas = SharpVision.Terminal.Rendering.Canvas;
using TerminalStyle = SharpVision.Terminal.Rendering.Style;

namespace SharpVision.Controls;

/// <summary>Decorates one child with composite or block-glyph visual overflow.</summary>
public sealed class Shadow: Container
{
    #region Construction and properties

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
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The shadow mode is unknown.");
            }

            _ = Set(ref field, value, Invalidation.Render);
        }
    }

    /// <summary>Gets or sets the signed visual offset in terminal cells.</summary>
    /// <exception cref="InvalidOperationException">The attached Shadow is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Shadow is disposed.</exception>
    public Point Offset
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    } = new(2, 1);

    /// <summary>Gets or sets the printable narrow block-mode Rune.</summary>
    /// <exception cref="ArgumentException">The value is a control or is not one cell wide.</exception>
    /// <exception cref="InvalidOperationException">The attached Shadow is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Shadow is disposed.</exception>
    public Rune Glyph
    {
        get;
        set
        {
            ValidateGlyph(value);
            _ = Set(ref field, value, Invalidation.Render);
        }
    } = new('▓');

    /// <summary>Gets or sets an optional direct shadow foreground.</summary>
    /// <exception cref="InvalidOperationException">The attached Shadow is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Shadow is disposed.</exception>
    public Color? Foreground
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    }

    /// <summary>Gets or sets an optional direct shadow background.</summary>
    /// <exception cref="InvalidOperationException">The attached Shadow is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Shadow is disposed.</exception>
    public Color? Background
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    }

    /// <summary>Gets or sets optional direct shadow rendition attributes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown flags.</exception>
    /// <exception cref="InvalidOperationException">The attached Shadow is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Shadow is disposed.</exception>
    public TerminalAttributes? Attributes
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
    } = TerminalAttributes.Dim;

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    protected override Rect VisualBounds => Union(Bounds, Shift(Bounds, Offset));

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
        var shifted = Shift(Bounds, Offset);
        var target = shifted.Intersect(canvas.Bounds);
        var style = ResolveShadowStyle();
        var background = Background.HasValue || Appearance.Background.HasValue
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

                if (Mode == ShadowMode.Composite)
                {
                    canvas.ApplyStyle(new Rect(x, y, 1, 1), style, background);
                }
                else
                {
                    Debug.Assert(Mode == ShadowMode.BlockGlyph, "Public validation limits shadow modes.");
                    canvas.DrawRune(
                        CellGlyph.Resolve(Glyph, new Rune('#'), CellPolicy.AmbiguousWidth),
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
        var (attributes, underline, underlineColor) = Decoration.Resolve(inherited, Attributes);
        return new TerminalStyle(
            Foreground ?? inherited.Foreground,
            Background ?? inherited.Background,
            attributes,
            inherited.Hyperlink,
            underline,
            underlineColor);
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

    private static void ValidateGlyph(Rune value)
    {
        Span<char> buffer = stackalloc char[2];
        var length = value.EncodeToUtf16(buffer);
        var measurement = Terminal.Unicode.Width.Measure(buffer[..length]);

        if (measurement.Cells != 1 || measurement.Controls != 0)
        {
            throw new ArgumentException(
                "A shadow glyph must be printable and exactly one cell wide.",
                nameof(value));
        }
    }
}
