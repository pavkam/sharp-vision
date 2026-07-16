// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Terminal.Input;

/// <summary>Frames one owned content control as a titled terminal window with optional Turbo Vision-style shadowing.</summary>
public sealed partial class Window: ContentControl
{
    private bool _dragging;
    private Point _dragPrevious;

    #region Construction and properties

    static Window()
    {
        _ = HasShadowProperty.RegisterClassDefault<Window>(true);
        _ = ShadowOffsetProperty.RegisterClassDefault<Window>(new Point(2, 1));
        _ = ShadowAttributesProperty.RegisterClassDefault<Window>(TerminalAttributes.Dim);
    }

    /// <summary>Initializes an empty window with a rounded border and composite shadow.</summary>
    public Window() => PropertyChanged += OnWindowPropertyChanged;

    /// <summary>Raised when the close glyph is activated by a pointer press or programmatic invocation.</summary>
    public event EventHandler? Closing;

    /// <summary>Gets or sets whether the window can be dragged by its title bar.</summary>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
    public bool CanMove
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.None);
    } = true;

    /// <summary>Gets or sets whether the window renders a close glyph in the top-right corner of the border.</summary>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
    public bool CanClose
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Render);
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
            _ = SetProperty(ref field, value, ChangeImpact.Measure);
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

            _ = SetProperty(ref field, value, ChangeImpact.Render);
        }
    } = WindowTitlePlacement.Left;

    /// <summary>Gets or sets the terminal-safe physical glyph family used for the frame.</summary>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
    public Glyphs Glyphs
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Render);
    } = Glyphs.Rounded;

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    protected override Rect VisualBounds =>
        ControlChrome.ExpandVisualBounds(Bounds, HasShadow, ShadowOffset);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var child = Content;
        var titleWidth = Title.Length == 0 ? 0 : Add(2, Terminal.Unicode.Width.Measure(Title).Cells);

        if (child is null)
        {
            return new Size(Math.Max(2, titleWidth + 2), 2);
        }

        var desired = MeasureChild(
            child,
            new Constraint(Subtract(constraint.Width, 2), Subtract(constraint.Height, 2)));
        var contentWidth = child.Visibility == Visibility.Collapsed
            ? 2
            : Add(Add(desired.Width, child.Margin.Horizontal), 2);
        var contentHeight = child.Visibility == Visibility.Collapsed
            ? 2
            : Add(Add(desired.Height, child.Margin.Vertical), 2);
        return new Size(
            Math.Max(contentWidth, titleWidth + 2),
            contentHeight);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (Content is { } content)
        {
            ArrangeChild(content, new Thickness(1).Deflate(bounds), ResolvedAxes.Both);
        }
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
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

        var border = ControlAppearance.ResolveBorderStyle(this, GetVisualState());
        var background = opaque ? BackgroundMode.Opaque : BackgroundMode.Transparent;
        ControlChrome.DrawUniformBorder(canvas, Bounds, Glyphs, border, background);

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
                background: background);
        }

        if (CanClose && Bounds.Width > 3)
        {
            _ = canvas.Draw(
                "✕".AsSpan(),
                new Point(Bounds.Right - 2, Bounds.Y),
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

        if (eventArgs.Handled)
        {
            return;
        }

        if (eventArgs is KeyEventArgs { Stroke.Action: KeyAction.Press } key)
        {
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

            return;
        }

        if (eventArgs is PointerEventArgs pointer)
        {
            HandlePointerClose(pointer);

            if (!pointer.Handled)
            {
                HandlePointerDrag(pointer);
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureCancelled(ReleaseReason reason)
    {
        base.OnPointerCaptureCancelled(reason);
        _dragging = false;
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            PropertyChanged -= OnWindowPropertyChanged;
            Closing = null;
        }
    }

    #endregion

    #region Close and drag interaction

    private void HandlePointerClose(PointerEventArgs eventArgs)
    {
        Debug.Assert(eventArgs is not null, "Pointer handling receives a non-null event.");

        if (eventArgs.Pointer.Cells is not { } cells)
        {
            return;
        }

        if (eventArgs.Pointer.Action == PointerAction.Press &&
            eventArgs.Pointer.Buttons == Buttons.Primary &&
            IsCloseGlyph(cells))
        {
            Closing?.Invoke(this, EventArgs.Empty);
            eventArgs.Handled = true;
        }
    }

    private void HandlePointerDrag(PointerEventArgs eventArgs)
    {
        Debug.Assert(eventArgs is not null, "Pointer handling receives a non-null event.");

        if (!CanMove || eventArgs.Pointer.Cells is not { } cells)
        {
            return;
        }

        var action = eventArgs.Pointer.Action;

        if (action == PointerAction.Press &&
            eventArgs.Pointer.Buttons == Buttons.Primary &&
            IsTitleBar(cells) &&
            CapturePointer())
        {
            _dragging = true;
            _dragPrevious = cells;
            eventArgs.Handled = true;
        }
        else if (action == PointerAction.Move && _dragging && HasPointerCapture)
        {
            var deltaX = cells.X - _dragPrevious.X;
            var deltaY = cells.Y - _dragPrevious.Y;
            _dragPrevious = cells;
            Left = Length.Cells(Math.Max(0, LocalBounds.X + deltaX));
            Top = Length.Cells(Math.Max(0, LocalBounds.Y + deltaY));
            eventArgs.Handled = true;
        }
        else if (action == PointerAction.Release && _dragging)
        {
            _dragging = false;
            ReleasePointerCapture();
            eventArgs.Handled = true;
        }
    }

    private bool IsTitleBar(Point cells) =>
        cells.Y == Bounds.Y && cells.X >= Bounds.X && cells.X < Bounds.Right;

    private bool IsCloseGlyph(Point cells) =>
        CanClose && Bounds.Width > 3 && cells.Y == Bounds.Y && cells.X == Bounds.Right - 2;

    #endregion

    #region Implementation

    private void OnWindowPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName != nameof(Visibility) || Visibility != Visibility.Visible)
        {
            return;
        }

        var first = FindFirstFocusable(this);
        _ = first is not null ? FocusOwner?.Focus(first) : FocusOwner?.Focus(this);
    }

    private static Control? FindFirstFocusable(Control root)
    {
        var count = root.OwnedControlCount;

        for (var i = 0; i < count; i++)
        {
            var child = root.OwnedControlAt(i);

            if (child.CanFocus && child.EffectiveIsVisible && child.EffectiveIsEnabled)
            {
                return child;
            }

            if (FindFirstFocusable(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static int Add(int left, int right)
    {
        Debug.Assert(left >= 0, "Window accumulation uses non-negative extents.");
        Debug.Assert(right >= 0, "Window accumulation uses non-negative extents.");

        var result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private static int? Subtract(int? value, int extent)
    {
        Debug.Assert(extent >= 0, "Window subtraction extent is non-negative.");

        return value.HasValue
            ? Math.Max(0, value.Value - extent)
            : null;
    }

    private static Button? FindButton(Control control, Func<Button, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(predicate);

        if (control is Button button && button.EffectiveIsEnabled && button.EffectiveIsVisible && predicate(button))
        {
            return button;
        }

        var count = control.OwnedControlCount;

        for (var index = 0; index < count; index++)
        {
            if (FindButton(control.OwnedControlAt(index), predicate) is { } result)
            {
                return result;
            }
        }

        return null;
    }

    #endregion
}
