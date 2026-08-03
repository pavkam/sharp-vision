// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using SharpVision.Terminal.Input;

/// <summary>Edits one signed integer value along a focusable horizontal or vertical rail.</summary>
[PublicAPI]
public sealed class Slider: Control<SliderStyle>
{
    private int _value;
    private readonly DragBehavior _drag;
    private Rect _dragBounds;
    private int _dragLength;

    /// <summary>Initializes a horizontal focusable range from zero through one hundred.</summary>
    public Slider() : base(SliderStyle.Definition)
    {
        _drag = new DragBehavior(
            () => ContentBounds,
            () => EffectiveIsEnabled && EffectiveIsVisible,
            RequestFocus,
            CapturePointer,
            () => HasPointerCapture,
            ReleasePointerCapture,
            SetPressed);
        Focusable = true;
        TabStop = true;
        TabNavigation = TabNavigation.None;
    }

    /// <summary>Raised after a changed value commits.</summary>
    public event EventHandler<SliderValueChangedEventArgs>? ValueChanged;

    /// <summary>Gets or sets the inclusive signed lower endpoint; auto-clamps Value when needed.</summary>
    /// <exception cref="ArgumentException">The value exceeds Maximum.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int Minimum
    {
        get;
        set
        {
            if (value > Maximum)
            {
                throw new ArgumentException(
                    "Minimum cannot exceed Maximum.",
                    nameof(value));
            }

            if (SetProperty(ref field, value, InvalidationImpact.Render))
            {
                if (_value < value)
                {
                    _ = Commit(value);
                }
            }
        }
    }

    /// <summary>Gets or sets the inclusive signed upper endpoint; auto-clamps Value when needed.</summary>
    /// <exception cref="ArgumentException">The value is below Minimum.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int Maximum
    {
        get;
        set
        {
            if (value < Minimum)
            {
                throw new ArgumentException(
                    "Maximum cannot be below Minimum.",
                    nameof(value));
            }

            if (SetProperty(ref field, value, InvalidationImpact.Render))
            {
                if (_value > value)
                {
                    _ = Commit(value);
                }
            }
        }
    } = 100;

    /// <summary>Gets or sets the current value inside the inclusive endpoints.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the range.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int Value
    {
        get => _value;
        set
        {
            if (value < Minimum || value > Maximum)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Value must be inside the inclusive range.");
            }

            _ = Commit(value);
        }
    }

    /// <summary>Gets or sets the non-negative arrow and wheel change.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int SmallChange
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = 1;

    /// <summary>Gets or sets the non-negative Page Up and Page Down change.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int LargeChange
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = 10;

    /// <summary>Gets or sets whether the range runs horizontally or bottom-to-top vertically.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Orientation Orientation
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The orientation is unknown.");
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = Orientation.Horizontal;

    /// <summary>Adds one signed command delta with saturation and endpoint clamping.</summary>
    /// <param name="delta">The signed requested change.</param>
    /// <returns>True when a changed value committed; otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool ChangeBy(int delta)
    {
        VerifyMutable();
        var requested = (long) Value + delta;
        var next = (int) Math.Clamp(requested, Minimum, Maximum);
        return Commit(next);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint.Width;
        return Orientation == Orientation.Horizontal ? new Size(5, 1) : new Size(1, 5);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (!EffectiveIsEnabled || !EffectiveIsVisible)
        {
            return;
        }

        if (eventArgs is KeyEventArgs key)
        {
            Handle(key);
        }
        else if (eventArgs is PointerEventArgs pointer)
        {
            Handle(pointer);
        }

        if (!eventArgs.Handled)
        {
            base.OnEvent(eventArgs);
        }
    }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);
        _drag.FocusChanged(focused);
        ResetDragState();
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        _drag.Unavailable();
        ResetDragState();

        if (reason == ReleaseReason.Disposed)
        {
            ValueChanged = null;
        }
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason)
    {
        base.OnLostPointerCapture(reason);
        _drag.CaptureLost();
        ResetDragState();
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var bounds = ContentBounds;
        var length = AxisLength(bounds);

        if (length == 0)
        {
            return;
        }

        var thumb = PositionFor(Value, length);
        var inherited = ResolvedStyle;
        var actualStyle = ActualStyle;
        var fillStyle = inherited.WithForeground(ResolveColor(actualStyle.FillColor));
        var trackStyle = inherited.WithForeground(ResolveColor(actualStyle.TrackColor));
        var thumbStyle = inherited.WithForeground(ResolveColor(actualStyle.ThumbColor));

        if (this.HasOpaqueFill(GetAppearanceState()))
        {
            canvas.Clear(bounds, inherited);
        }

        for (var position = 0; position < length; position++)
        {
            var isThumb = position == thumb;
            var isFilled = IsFilled(position, thumb);
            var glyph = isThumb ? ThumbRune() : isFilled ? FillRune() : TrackRune();
            var style = isThumb ? thumbStyle : isFilled ? fillStyle : trackStyle;
            canvas.DrawRune(glyph, PointAt(bounds, position), style, BackgroundMode.Transparent);
        }
    }

    private bool Commit(int value)
    {
        value = Math.Clamp(value, Minimum, Maximum);
        var previous = _value;

        if (!SetProperty(ref _value, value, InvalidationImpact.Render, nameof(Value)))
        {
            return false;
        }

        ValueChanged?.Invoke(this, new SliderValueChangedEventArgs(previous, value));
        return true;
    }

    private void Handle(KeyEventArgs eventArgs)
    {
        if (eventArgs.Stroke.Action is not (KeyAction.Press or KeyAction.Repeat))
        {
            return;
        }

        var code = eventArgs.Stroke.Code;

        if ((Orientation == Orientation.Horizontal && code == Code.Left) ||
            (Orientation == Orientation.Vertical && code == Code.Down))
        {
            _ = ChangeBy(SmallChange.Negate());
        }
        else if ((Orientation == Orientation.Horizontal && code == Code.Right) ||
                 (Orientation == Orientation.Vertical && code == Code.Up))
        {
            _ = ChangeBy(SmallChange);
        }
        else if (code == Code.PageUp)
        {
            _ = ChangeBy(LargeChange);
        }
        else if (code == Code.PageDown)
        {
            _ = ChangeBy(LargeChange.Negate());
        }
        else if (code == Code.Home)
        {
            _ = Commit(Minimum);
        }
        else if (code == Code.End)
        {
            _ = Commit(Maximum);
        }
        else
        {
            return;
        }

        eventArgs.Handled = true;
    }

    private void Handle(PointerEventArgs eventArgs)
    {
        var pointer = eventArgs.Pointer;

        if (pointer.Action == PointerAction.Wheel)
        {
            var wheel = Orientation == Orientation.Horizontal && pointer.WheelX != 0
                ? pointer.WheelX
                : pointer.WheelY;

            if (wheel != 0)
            {
                var requested = (long) wheel * SmallChange;
                var delta = (int) Math.Clamp(requested, int.MinValue, int.MaxValue);
                eventArgs.Handled = ChangeBy(delta);
            }

            return;
        }

        if (_drag.IsDragging)
        {
            eventArgs.Handled = true;

            if (pointer.Action is PointerAction.Release or PointerAction.Leave)
            {
                _drag.Cancel(releaseCapture: true);
                ResetDragState();
            }
            else if (pointer.Cells is { } dragCells)
            {
                _ = Commit(ValueAt(dragCells, _dragBounds, _dragLength));
            }

            return;
        }

        if (pointer.Action != PointerAction.Press ||
            (pointer.Buttons & Buttons.Primary) == 0 ||
            pointer.Cells is not { } cells ||
            !ContentBounds.Contains(cells))
        {
            return;
        }

        var bounds = ContentBounds;
        var length = AxisLength(bounds);

        if (length == 0)
        {
            return;
        }

        _ = RequestFocus();
        _ = Commit(ValueAt(cells, bounds, length));
        eventArgs.Handled = true;

        if (_drag.TryStart(cells))
        {
            _dragBounds = bounds;
            _dragLength = length;
        }
    }

    private int ValueAt(Point point, Rect bounds, int length)
    {
        if (length <= 1 || Minimum == Maximum)
        {
            return Minimum;
        }

        var physical = Orientation == Orientation.Horizontal
            ? point.X - bounds.X
            : point.Y - bounds.Y;
        physical = Math.Clamp(physical, 0, length - 1);
        var logical = Orientation == Orientation.Vertical ? length - 1 - physical : physical;
        var span = (long) Maximum - Minimum;
        var positions = length - 1L;
        var offset = ((logical * span) + (positions / 2)) / positions;
        return Math.Clamp((int) (Minimum + offset), Minimum, Maximum);
    }

    private void ResetDragState()
    {
        _dragBounds = default;
        _dragLength = 0;
    }

    private int PositionFor(int value, int length)
    {
        if (length <= 1 || Minimum == Maximum)
        {
            return 0;
        }

        var span = (long) Maximum - Minimum;
        var offset = (long) value - Minimum;
        var positions = length - 1L;
        var logical = (int) (((offset * positions) + (span / 2)) / span);
        return Orientation == Orientation.Vertical ? length - 1 - logical : logical;
    }

    private bool IsFilled(int position, int thumb) => Orientation == Orientation.Horizontal
        ? position < thumb
        : position > thumb;

    private int AxisLength(Rect bounds) => Orientation == Orientation.Horizontal
        ? bounds.Width
        : bounds.Height;

    private Point PointAt(Rect bounds, int position) => Orientation == Orientation.Horizontal
        ? new Point(bounds.X.SaturatingAdd(position), bounds.Y)
        : new Point(bounds.X, bounds.Y.SaturatingAdd(position));

    private Color ResolveColor(ColorValue value) => ResolveColor(value, Theme);

    private Rune TrackRune()
    {
        var glyphs = ActualStyle.Glyphs;
        var themed = Orientation == Orientation.Horizontal ? glyphs.HorizontalTrackGlyph : glyphs.VerticalTrackGlyph;
        return themed.Value.Resolve(themed.Fallback, CellPolicy.AmbiguousWidth);
    }

    private Rune FillRune()
    {
        var glyphs = ActualStyle.Glyphs;
        var themed = Orientation == Orientation.Horizontal ? glyphs.HorizontalFillGlyph : glyphs.VerticalFillGlyph;
        return themed.Value.Resolve(themed.Fallback, CellPolicy.AmbiguousWidth);
    }

    private Rune ThumbRune()
    {
        var themed = ActualStyle.Glyphs.ThumbGlyph;
        return themed.Value.Resolve(themed.Fallback, CellPolicy.AmbiguousWidth);
    }

}
