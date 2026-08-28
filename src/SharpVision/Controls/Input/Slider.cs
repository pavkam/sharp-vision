// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using SharpVision.Terminal.Input;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Edits one signed integer value along a focusable horizontal or vertical rail.</summary>
[PublicAPI]
public sealed class Slider: ControlBase, IStyled<SliderStyle>
{
    private int _value;
    private long _valueVersion;
    private readonly DragBehavior _drag;
    private Rect _dragBounds;
    private int _dragLength;
    private readonly StyleSlot<SliderStyle> _style;

    /// <summary>Initializes a horizontal focusable range from zero through one hundred.</summary>
    public Slider()
    {
        _style = InitializeStyle(SliderStyle.Definition);
        _drag = new DragBehavior(
            () => ContentBounds,
            () => EffectiveIsEnabled && EffectiveIsVisible,
            () => !IsDisposed,
            RequestFocus,
            CapturePointer,
            () => HasPointerCapture,
            ReleasePointerCapture,
            SetPressed);
        RegisterLifecycleParticipant(_drag);
        IsFocusable = true;
        IsTabStop = true;
        TabNavigation = TabNavigation.None;
    }

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached slider is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The slider is disposed.</exception>
    public SliderStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public SliderStyle ActualStyle => _style.Actual;

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
            ArgumentException.ThrowIfAboveMaximum(value, Maximum, nameof(value), "Minimum cannot exceed Maximum.");

            _ = SetPropertyAndContinue(
                ref field,
                value,
                InvalidationImpact.Render,
                () => _ = _value < value && Commit(value));
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
            ArgumentException.ThrowIfBelowMinimum(value, Minimum, nameof(value), "Maximum cannot be below Minimum.");

            _ = SetPropertyAndContinue(
                ref field,
                value,
                InvalidationImpact.Render,
                () => _ = _value > value && Commit(value));
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
            ArgumentOutOfRangeException.ThrowIfOutsideInclusiveRange(value, Minimum, Maximum, nameof(value), "Value must be inside the inclusive range.");

            _ = Commit(value);
        }
    }

    /// <summary>Gets or sets the non-negative arrow and wheel change.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
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
    [NonNegativeValue]
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
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The orientation is unknown.");

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

        if (!eventArgs.IsHandled)
        {
            base.OnEvent(eventArgs);
        }
    }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);
        ResetDragState();
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
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

        if (!SetVersionedProperty(
                ref _value,
                value,
                InvalidationImpact.Render,
                ref _valueVersion,
                out var version,
                nameof(Value)))
        {
            return false;
        }

        if (IsVersionedPropertyCurrent(_value, value, _valueVersion, version))
        {
            ValueChanged?.Invoke(this, new SliderValueChangedEventArgs(previous, value));
        }

        return true;
    }

    private void Handle(KeyEventArgs eventArgs)
    {
        if (!eventArgs.IsKeyDown ||
            !KeyboardModifierPolicy.MatchesCommand(eventArgs.Stroke.Modifiers, Modifiers.None))
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

        eventArgs.IsHandled = true;
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
                eventArgs.IsHandled = ChangeBy(delta);
            }

            return;
        }

        if (_drag.IsDragging)
        {
            eventArgs.IsHandled = true;

            if (pointer.Action == PointerAction.Leave || PointerButtonTransition.IsPrimaryRelease(pointer))
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

        var dispatcher = Dispatcher;
        _ = RequestFocus();

        if (!CanContinueAfterFocus(dispatcher))
        {
            return;
        }

        _ = Commit(ValueAt(cells, bounds, length));
        eventArgs.IsHandled = true;

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
        var offset = RangeValidation.RoundHalfUp(logical * span, positions);
        return Math.Clamp(Minimum + offset, Minimum, Maximum);
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
        var logical = RangeValidation.RoundHalfUp(offset * positions, span);
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
