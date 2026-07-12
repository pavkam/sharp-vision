using System.Diagnostics;
using System.Text;
using System.Windows.Input;

using SharpVision.Input;
using SharpVision.Layout;
using SharpVision.Styling;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Rendering;

using BackgroundMode = SharpVision.Terminal.Rendering.BackgroundMode;
using TerminalCanvas = SharpVision.Terminal.Rendering.Canvas;
using TerminalStyle = SharpVision.Terminal.Rendering.Style;

namespace SharpVision.Controls;

/// <summary>Defines a focusable command control with one optional owned content child.</summary>
public sealed class Button: Pressable
{
    private ICommand? _command;

    #region Construction and command properties

    /// <summary>Initializes an empty focusable Button with rounded border, internal padding, and compact shadow.</summary>
    public Button() : base(capacity: 1) => Padding = new Thickness(1);

    /// <summary>Raised after released state commits and before command execution.</summary>
    public event EventHandler<ActivationEventArgs>? Click;

    /// <summary>Gets or atomically sets the optional owned content.</summary>
    /// <exception cref="ArgumentException">The value cannot be owned by this Button.</exception>
    /// <exception cref="InvalidOperationException">The attached Button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button or value is disposed.</exception>
    public Control? Content
    {
        get => Children.Count == 0 ? null : Children[0];
        set => Children.SetOnly(value);
    }

    /// <summary>Gets or sets the optional command invoked after Click.</summary>
    /// <exception cref="InvalidOperationException">The attached Button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button is disposed.</exception>
    public ICommand? Command
    {
        get => _command;
        set
        {
            VerifyMutable();

            if (EqualityComparer<ICommand?>.Default.Equals(_command, value))
            {
                return;
            }

            _command?.CanExecuteChanged -= OnCanExecuteChanged;
            _ = Set(ref _command, value, Invalidation.Render);
            _command?.CanExecuteChanged += OnCanExecuteChanged;
        }
    }

    /// <summary>Gets or sets the borrowed parameter passed to command queries and execution.</summary>
    /// <exception cref="InvalidOperationException">The attached Button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button is disposed.</exception>
    public object? CommandParameter
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    }

    /// <summary>Gets or sets whether an owning Window treats Enter as a fallback activation.</summary>
    /// <exception cref="InvalidOperationException">The attached Button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button is disposed.</exception>
    public bool IsDefault
    {
        get;
        set => _ = Set(ref field, value, Invalidation.None);
    }

    /// <summary>Gets or sets whether an owning Window treats Escape as a fallback activation.</summary>
    /// <exception cref="InvalidOperationException">The attached Button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button is disposed.</exception>
    public bool IsCancel
    {
        get;
        set => _ = Set(ref field, value, Invalidation.None);
    }

    /// <summary>Gets or sets the validated physical glyph family used by the button border.</summary>
    /// <exception cref="InvalidOperationException">The attached button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The button is disposed.</exception>
    public Glyphs Glyphs
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    } = Glyphs.Rounded;

    /// <summary>Gets or sets whether the compact translated shadow is rendered outside the button body.</summary>
    /// <exception cref="InvalidOperationException">The attached button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The button is disposed.</exception>
    public bool HasShadow
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    } = true;

    /// <summary>Gets or sets the signed terminal-cell translation applied to the compact button shadow.</summary>
    /// <exception cref="InvalidOperationException">The attached button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The button is disposed.</exception>
    public Point ShadowOffset
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    } = new(1, 1);

    /// <summary>Gets or sets how the button's visual shadow changes overflow cells.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a known shadow mode.</exception>
    /// <exception cref="InvalidOperationException">The attached button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The button is disposed.</exception>
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

    /// <summary>Gets or sets the visible one-cell Rune drawn for a block-glyph button shadow.</summary>
    /// <exception cref="ArgumentException">The Rune is a control or is not one terminal cell wide.</exception>
    /// <exception cref="InvalidOperationException">The attached button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The button is disposed.</exception>
    public Rune ShadowGlyph
    {
        get;
        set
        {
            ValidateShadowGlyph(value);
            _ = Set(ref field, value, Invalidation.Render);
        }
    } = new('▓');

    #endregion

    #region Activation and lifecycle


    /// <summary>Activates an available executable Button through its public API.</summary>
    /// <exception cref="InvalidOperationException">The attached Button is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button is disposed.</exception>
    public void PerformClick()
    {
        VerifyMutable();

        if (EffectiveIsEnabled && EffectiveIsVisible)
        {
            Activate(ActivationCause.Programmatic);
        }
    }

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        var command = Command;
        var parameter = CommandParameter;

        if (command is not null && !command.CanExecute(parameter))
        {
            return;
        }

        var eventArgs = new ActivationEventArgs(cause);
        Click?.Invoke(this, eventArgs);
        command?.Execute(parameter);
    }

    /// <inheritdoc/>
    protected override Size MeasureCore(Constraint constraint)
    {
        var content = Content;

        if (content is null)
        {
            return default;
        }

        content.Measure(constraint);
        return new Size(
            Add(content.DesiredSize.Width, content.Margin.Horizontal),
            Add(content.DesiredSize.Height, content.Margin.Vertical));
    }

    /// <inheritdoc/>
    protected override void ArrangeCore(Rect bounds) =>
        Content?.Arrange(FaceContentBounds(bounds), widthResolved: true, heightResolved: true);

    /// <inheritdoc/>
    protected override Rect VisualBounds => HasShadow ? Union(Bounds, Shift(Bounds, ShadowOffset)) : FaceBounds;

    /// <inheritdoc/>
    protected override void RenderCore(TerminalCanvas canvas)
    {
        var style = ResolvedStyle;
        var face = FaceBounds;

        var opaque = Appearance.Background.HasValue;

        if (HasShadow)
        {
            // Draw behind the face so a pressed face can physically cover the
            // translated strip, rather than tinting the strip as pressed.
            DrawShadow(canvas, NormalStyle, BackgroundMode.Transparent, face);
        }

        if (opaque || (IsPressed && HasShadow))
        {
            // A styled button owns its complete interaction surface, including
            // padding cells that do not belong to its content child. A pressed
            // face clears too, so the old shadow cannot shine through its body.
            canvas.Clear(face, style);
        }

        var background = opaque ? BackgroundMode.Opaque : BackgroundMode.Transparent;
        DrawFrame(canvas, face, style, background);
    }

    /// <inheritdoc/>
    protected override void OnPressedChanged(bool pressed)
    {
        base.OnPressedChanged(pressed);

        if (!HasShadow || Content is not { } content)
        {
            return;
        }

        // Pointer and keyboard press state must be drawable before a later
        // layout drain, so keep owned content in the same translated face box.
        content.Arrange(FaceContentBounds(ContentBounds), widthResolved: true, heightResolved: true);
        Invalidate(Invalidation.Arrange);
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed && _command is not null)
        {
            _command.CanExecuteChanged -= OnCanExecuteChanged;
            _command = null;
            Click = null;
        }
    }

    #endregion

    #region Layout and rendering

    private static int Add(int left, int right)
    {
        var value = (long) left + right;
        return value >= int.MaxValue ? int.MaxValue : (int) value;
    }

    private void DrawFrame(TerminalCanvas canvas, Rect bounds, TerminalStyle style, BackgroundMode background)
    {
        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return;
        }

        for (var x = bounds.X; x < bounds.Right; x++)
        {
            var top = x == bounds.X ? Glyphs.TopLeft : x == bounds.Right - 1 ? Glyphs.TopRight : Glyphs.Top;
            var bottom = x == bounds.X ? Glyphs.BottomLeft : x == bounds.Right - 1 ? Glyphs.BottomRight : Glyphs.Bottom;
            canvas.DrawRune(top, new Point(x, bounds.Y), style, background);

            if (bounds.Height > 1)
            {
                canvas.DrawRune(bottom, new Point(x, bounds.Bottom - 1), style, background);
            }
        }

        for (var y = bounds.Y + 1; y < bounds.Bottom - 1; y++)
        {
            canvas.DrawRune(Glyphs.Left, new Point(bounds.X, y), style, background);

            if (bounds.Width > 1)
            {
                canvas.DrawRune(Glyphs.Right, new Point(bounds.Right - 1, y), style, background);
            }
        }
    }

    private void DrawShadow(TerminalCanvas canvas, TerminalStyle source, BackgroundMode background, Rect face)
    {
        var target = Shift(Bounds, ShadowOffset).Intersect(canvas.Bounds);
        var style = new TerminalStyle(
            source.Foreground,
            source.Background,
            Attributes.Dim,
            source.Hyperlink);

        for (var y = target.Y; y < target.Bottom; y++)
        {
            for (var x = target.X; x < target.Right; x++)
            {
                var point = new Point(x, y);

                if (!face.Contains(point))
                {
                    // Keep one untouched cell before the bottom shadow begins.
                    // Without this gap, the shadow reads as an accidental extra border.
                    if (y >= Bounds.Bottom && x <= Bounds.X + Math.Abs(ShadowOffset.X))
                    {
                        continue;
                    }

                    if (ShadowMode == ShadowMode.Composite)
                    {
                        canvas.ApplyStyle(new Rect(x, y, 1, 1), style, background);
                    }
                    else
                    {
                        Debug.Assert(
                            ShadowMode == ShadowMode.BlockGlyph,
                            "Public validation limits button shadow modes.");
                        canvas.DrawRune(ShadowGlyph, point, style, background);
                    }
                }
            }
        }
    }

    #endregion

    #region Geometry and validation

    private static Rect Shift(Rect value, Point offset) => new(
        SaturatingAdd(value.X, offset.X),
        SaturatingAdd(value.Y, offset.Y),
        value.Width,
        value.Height);

    private Rect FaceBounds => IsPressed && HasShadow ? Shift(Bounds, ShadowOffset) : Bounds;

    private Rect FaceContentBounds(Rect bounds) => IsPressed && HasShadow ? Shift(bounds, ShadowOffset) : bounds;

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

    private static void ValidateShadowGlyph(Rune value)
    {
        Span<char> buffer = stackalloc char[2];
        var length = value.EncodeToUtf16(buffer);
        var measurement = Terminal.Unicode.Width.Measure(buffer[..length]);

        if (measurement.Cells != 1 || measurement.Controls != 0)
        {
            throw new ArgumentException(
                "A button shadow glyph must be printable and exactly one cell wide.",
                nameof(value));
        }
    }

    #endregion

    #region Command notification

    private void OnCanExecuteChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (IsDisposed)
        {
            return;
        }

        var dispatcher = Dispatcher;

        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Post(() =>
            {
                if (!IsDisposed)
                {
                    Invalidate(Invalidation.Render);
                }
            });
            return;
        }

        Invalidate(Invalidation.Render);
    }

    #endregion
}
