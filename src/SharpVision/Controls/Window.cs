using System.Diagnostics;
using System.Text;

using SharpVision.Input;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;
using SharpVision.Terminal.Protocols;

using BackgroundMode = SharpVision.Terminal.Rendering.BackgroundMode;
using KeyAction = SharpVision.Terminal.Input.Action;
using TerminalAttributes = SharpVision.Terminal.Rendering.Attributes;
using TerminalCanvas = SharpVision.Terminal.Rendering.Canvas;
using TerminalStyle = SharpVision.Terminal.Rendering.Style;

namespace SharpVision.Controls;

/// <summary>Frames one owned child as a titled terminal window with optional Turbo Vision-style shadowing.</summary>
public sealed class Window: Container
{
    #region Construction and properties

    /// <summary>Initializes an empty window with a rounded border and composite shadow.</summary>
    public Window() : base(capacity: 1)
    {
    }

    /// <summary>Gets or atomically sets the single control arranged in the framed interior.</summary>
    /// <exception cref="ArgumentException">The value cannot be owned by this window.</exception>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window or child is disposed.</exception>
    public Control? Child
    {
        get => Children.Count == 0 ? null : Children[0];
        set => Children.SetOnly(value);
    }

    /// <summary>Gets or sets the non-null title written into the top edge.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
    public string Title
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = Set(ref field, value, Invalidation.Measure);
        }
    } = string.Empty;

    /// <summary>Gets or sets the left, centered, or right title placement inside the top frame edge.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
    public WindowTitlePlacement TitlePlacement
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The title placement is unknown.");
            }

            _ = Set(ref field, value, Invalidation.Render);
        }
    } = WindowTitlePlacement.Left;

    /// <summary>Gets or sets the terminal-safe physical glyph family used for the frame.</summary>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
    public Glyphs Glyphs
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    } = Glyphs.Rounded;

    /// <summary>Gets or sets an optional direct foreground override for frame cells.</summary>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
    public Color? BorderColor
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    }

    /// <summary>Gets or sets an optional direct background used by the complete window body.</summary>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
    public Color? Background
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    }

    /// <summary>Gets or sets optional attributes applied to frame and title cells.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown flags.</exception>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
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
    }

    /// <summary>Gets or sets whether the translated shadow is rendered outside the framed body.</summary>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
    public bool HasShadow
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    } = true;

    /// <summary>Gets or sets the composite or block-glyph shadow behavior.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
    public ShadowMode ShadowMode
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
    } = ShadowMode.Composite;

    /// <summary>Gets or sets the signed terminal-cell translation applied to the optional shadow.</summary>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
    public Point ShadowOffset
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    } = new(2, 1);

    /// <summary>Gets or sets the printable one-cell Rune used by a block-glyph shadow.</summary>
    /// <exception cref="ArgumentException">The value is a control or does not occupy exactly one cell.</exception>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
    public Rune ShadowGlyph
    {
        get;
        set
        {
            ValidateGlyph(value);
            _ = Set(ref field, value, Invalidation.Render);
        }
    } = new('▓');

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    protected override Rect VisualBounds => HasShadow ? Union(Bounds, Shift(Bounds, ShadowOffset)) : Bounds;

    /// <inheritdoc/>
    protected override Size MeasureCore(Constraint constraint)
    {
        var child = Child;
        var titleWidth = Title.Length == 0 ? 0 : Add(2, Terminal.Unicode.Width.Measure(Title).Cells);

        if (child is null)
        {
            return new Size(Math.Max(2, titleWidth + 2), 2);
        }

        child.Measure(new Constraint(Subtract(constraint.Width, 2), Subtract(constraint.Height, 2)));
        return new Size(
            Math.Max(Add(Add(child.DesiredSize.Width, child.Margin.Horizontal), 2), titleWidth + 2),
            Add(Add(child.DesiredSize.Height, child.Margin.Vertical), 2));
    }

    /// <inheritdoc/>
    protected override void ArrangeCore(Rect bounds) =>
        Child?.Arrange(new Thickness(1).Deflate(bounds), widthResolved: true, heightResolved: true);

    /// <inheritdoc/>
    protected override void RenderCore(TerminalCanvas canvas)
    {
        var inherited = ResolvedStyle;
        var background = Background ?? inherited.Background;
        var (attributes, underline, underlineColor) = Decoration.Resolve(inherited, Attributes);
        var opaque = Background.HasValue || Appearance.Background.HasValue;
        var body = new TerminalStyle(
            inherited.Foreground,
            background,
            attributes,
            inherited.Hyperlink,
            underline,
            underlineColor);

        if (opaque)
        {
            canvas.Clear(Bounds, body);
        }

        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var border = new TerminalStyle(
            BorderColor ?? Appearance.BorderColor ?? inherited.Foreground,
            background,
            attributes,
            inherited.Hyperlink,
            underline,
            underlineColor);
        var backgroundMode = opaque ? BackgroundMode.Opaque : BackgroundMode.Transparent;
        DrawFrame(canvas, border, backgroundMode);

        if (!string.IsNullOrEmpty(Title) && Bounds.Width > 3)
        {
            var text = $" {Title} ";
            var available = Bounds.Width - 2;
            var cells = Terminal.Unicode.Width.Measure(text).Cells;
            var offset = TitlePlacement switch
            {
                WindowTitlePlacement.Left => 0,
                WindowTitlePlacement.Center => Math.Max(0, (available - cells) / 2),
                WindowTitlePlacement.Right => Math.Max(0, available - cells),
                _ => throw new InvalidOperationException("The validated title placement is unknown."),
            };
            var title = canvas.Clip(new Rect(Bounds.X + 1, Bounds.Y, available, 1));
            _ = title.Draw(
                text.AsSpan(),
                new Point(Bounds.X + 1 + offset, Bounds.Y),
                border,
                background: backgroundMode);
        }

        if (HasShadow)
        {
            DrawShadow(canvas, backgroundMode);
        }
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (eventArgs.Handled || eventArgs is not KeyEventArgs { Stroke.Action: KeyAction.Press } key)
        {
            return;
        }

        var button = key.Stroke.Code == Code.Enter
            ? FindButton(this, static candidate => candidate.IsDefault)
            : key.Stroke.Code == Code.Escape
                ? FindButton(this, static candidate => candidate.IsCancel)
                : null;

        if (button is not null)
        {
            button.PerformClick();
            eventArgs.Handled = true;
        }
    }

    #endregion

    #region Implementation

    private void DrawFrame(TerminalCanvas canvas, TerminalStyle style, BackgroundMode background)
    {
        for (var x = Bounds.X; x < Bounds.Right; x++)
        {
            var top = x == Bounds.X ? Glyphs.TopLeft : x == Bounds.Right - 1 ? Glyphs.TopRight : Glyphs.Top;
            var bottom = x == Bounds.X ? Glyphs.BottomLeft : x == Bounds.Right - 1 ? Glyphs.BottomRight : Glyphs.Bottom;
            canvas.DrawRune(top, new Point(x, Bounds.Y), style, background);

            if (Bounds.Height > 1)
            {
                canvas.DrawRune(bottom, new Point(x, Bounds.Bottom - 1), style, background);
            }
        }

        for (var y = Bounds.Y + 1; y < Bounds.Bottom - 1; y++)
        {
            canvas.DrawRune(Glyphs.Left, new Point(Bounds.X, y), style, background);

            if (Bounds.Width > 1)
            {
                canvas.DrawRune(Glyphs.Right, new Point(Bounds.Right - 1, y), style, background);
            }
        }
    }

    private void DrawShadow(TerminalCanvas canvas, BackgroundMode background)
    {
        var shifted = Shift(Bounds, ShadowOffset).Intersect(canvas.Bounds);
        var inherited = ResolvedStyle;
        var style = new TerminalStyle(
            inherited.Foreground,
            Background ?? inherited.Background,
            TerminalAttributes.Dim,
            inherited.Hyperlink);

        for (var y = shifted.Y; y < shifted.Bottom; y++)
        {
            for (var x = shifted.X; x < shifted.Right; x++)
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
                    canvas.DrawRune(ShadowGlyph, point, style, background);
                }
            }
        }
    }

    private static int Add(int left, int right)
    {
        var result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private static int? Subtract(int? value, int extent) => value.HasValue
        ? Math.Max(0, value.Value - extent)
        : null;

    private static Rect Shift(Rect value, Point offset) => new(
        SaturatingAdd(value.X, offset.X),
        SaturatingAdd(value.Y, offset.Y),
        value.Width,
        value.Height);

    private static Rect Union(Rect left, Rect right)
    {
        var x = Math.Min(left.X, right.X);
        var y = Math.Min(left.Y, right.Y);
        var rightEdge = Math.Max(left.Right, right.Right);
        var bottom = Math.Max(left.Bottom, right.Bottom);
        return new Rect(x, y, Extent(x, rightEdge), Extent(y, bottom));
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

    private static Button? FindButton(Control control, Func<Button, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(predicate);

        if (control is Button button && button.EffectiveIsEnabled && button.EffectiveIsVisible && predicate(button))
        {
            return button;
        }

        Button? result = null;
        control.VisitChildren(child => result ??= FindButton(child, predicate));
        return result;
    }

    #endregion
}
