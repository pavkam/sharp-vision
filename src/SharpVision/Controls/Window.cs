// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Terminal.Input;

/// <summary>Frames one owned child as a titled terminal window with optional Turbo Vision-style shadowing.</summary>
public sealed partial class Window: Container
{
    #region Construction and properties

    static Window()
    {
        _ = HasShadowProperty.RegisterClassDefault<Window>(true);
        _ = ShadowOffsetProperty.RegisterClassDefault<Window>(new Point(2, 1));
        _ = ShadowAttributesProperty.RegisterClassDefault<Window>(TerminalAttributes.Dim);
    }

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

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    protected override Rect VisualBounds =>
        ControlChrome.ExpandVisualBounds(Bounds, HasShadow, ShadowOffset);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        Control? child = Child;
        int titleWidth = Title.Length == 0 ? 0 : Add(2, Terminal.Unicode.Width.Measure(Title).Cells);

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
    protected override void ArrangeOverride(Rect bounds) =>
        Child?.Arrange(new Thickness(1).Deflate(bounds), widthResolved: true, heightResolved: true);

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

        TerminalStyle border = ControlAppearance.ResolveBorderStyle(this, GetVisualState());
        BackgroundMode background = opaque ? BackgroundMode.Opaque : BackgroundMode.Transparent;
        ControlChrome.DrawUniformBorder(canvas, Bounds, Glyphs, border, background);

        if (!string.IsNullOrEmpty(Title) && Bounds.Width > 3)
        {
            string text = $" {Title} ";
            int available = Bounds.Width - 2;
            int cells = Terminal.Unicode.Width.Measure(text).Cells;
            int offset = TitlePlacement switch
            {
                WindowTitlePlacement.Left => 0,
                WindowTitlePlacement.Center => Math.Max(0, (available - cells) / 2),
                WindowTitlePlacement.Right => Math.Max(0, available - cells),
                _ => throw new InvalidOperationException("The validated title placement is unknown."),
            };
            TerminalCanvas title = canvas.Clip(new Rect(Bounds.X + 1, Bounds.Y, available, 1));
            _ = title.Draw(
                text.AsSpan(),
                new Point(Bounds.X + 1 + offset, Bounds.Y),
                border,
                background: background);
        }

        if (HasShadow)
        {
            ControlChrome.DrawShadow(canvas, this, Bounds, Bounds, background, ResolvedStyle);
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

        Button? button = key.Stroke.Code == Code.Enter
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

    private static int Add(int left, int right)
    {
        long result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private static int? Subtract(int? value, int extent) => value.HasValue
        ? Math.Max(0, value.Value - extent)
        : null;

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
