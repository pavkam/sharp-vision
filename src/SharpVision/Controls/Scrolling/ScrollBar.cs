// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Scrolling;

using SharpVision.Terminal.Input;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Defines a focusable integer range with buttons, track, and draggable thumb.</summary>
[PublicAPI]
public sealed class ScrollBar: ControlBase, IStyled<ScrollBarStyle>
{
    private int _value;
    private readonly DragBehavior _drag;
    private int _dragPointerStart;
    private int? _dragPixelStart;
    private int _dragValueStart;
    private readonly StyleSlot<ScrollBarStyle> _style;

    /// <summary>Initializes a vertical focusable range from zero through one hundred.</summary>
    public ScrollBar()
    {
        _style = InitializeStyle(ScrollBarStyle.Definition);
        _drag = new DragBehavior(
            () => Bounds,
            () => EffectiveIsEnabled && EffectiveIsVisible,
            () =>
            {
                _ = FocusOwner?.Focus(this);
                return true;
            },
            () => CaptureOwner is { } c && c.Capture(this),
            () => CaptureOwner?.Captured is { } captured && ReferenceEquals(captured, this),
            () => CaptureOwner?.Release(),
            SetPressed);
        IsFocusable = true;
        IsTabStop = true;
        TabNavigation = TabNavigation.None;
    }

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached scroll bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The scroll bar is disposed.</exception>
    public ScrollBarStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public ScrollBarStyle ActualStyle => _style.Actual;

    /// <inheritdoc/>
    /// <summary>Raised after a changed value commits.</summary>
    public event EventHandler<ScrollEventArgs>? ValueChanged;

    /// <summary>Gets or sets the non-negative inclusive lower endpoint.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="ArgumentException">The value exceeds Maximum or the current Value.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
    public int Minimum
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentException.ThrowIfAboveMaximum(value, Math.Min(Maximum, Value), nameof(value), "Minimum cannot exceed Maximum or the current Value.");

            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    }

    /// <summary>Gets or sets the inclusive upper endpoint.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="ArgumentException">The value is below Minimum or the current Value.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
    public int Maximum
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentException.ThrowIfBelowMinimum(value, Math.Max(Minimum, Value), nameof(value), "Maximum cannot be below Minimum or the current Value.");

            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    } = 100;

    /// <summary>Gets or sets the non-negative visible extent represented by the thumb.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
    public int ViewportSize
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    }

    /// <summary>Gets or sets the current value inside the inclusive endpoints.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the range.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
    public int Value
    {
        get => _value;
        set
        {
            ArgumentOutOfRangeException.ThrowIfOutsideInclusiveRange(value, Minimum, Maximum, nameof(value), "Value must be inside the inclusive range.");

            _ = Commit(value, ScrollCause.Programmatic);
        }
    }

    /// <summary>Gets or sets the non-negative line/button change.</summary>
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

    /// <summary>Gets or sets the non-negative page/track change.</summary>
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

    /// <summary>Gets or sets whether the range runs top-to-bottom or left-to-right.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Orientation Orientation
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The enum value is unknown.");
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <summary>Adds a signed command delta with saturation and endpoint clamping.</summary>
    /// <param name="delta">The signed requested change.</param>
    /// <param name="cause">The defined input path.</param>
    /// <returns>True when a changed value committed; otherwise false.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool ScrollBy(int delta, ScrollCause cause = ScrollCause.Programmatic)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(cause, nameof(cause), "The enum value is unknown.");
        VerifyMutable();
        var range = CurrentRange();
        return Commit(range.Move(delta), cause);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint.Width;
        Debug.Assert(Enum.IsDefined(Orientation), "Orientation is validated before assignment.");
        var extent = ActualStyle.Chrome == ScrollBarChrome.Thin ? 1 : 3;
        return Orientation == Orientation.Vertical ? new Size(1, extent) : new Size(extent, 1);
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

        var buttons = ButtonCount(length);
        var trackLength = Math.Max(0, length - (buttons * 2));
        var thumb = ScrollThumb.Resolve(CurrentRange(), trackLength);
        var inherited = ResolvedStyle;
        var style = ActualStyle;
        var trackStyle = inherited.WithForeground(ResolveColor(style.TrackColor));
        var thumbStyle = inherited.WithForeground(ResolveColor(style.ThumbColor));
        var buttonStyle = inherited.WithForeground(ResolveColor(style.ButtonColor));

        if (this.HasOpaqueFill(GetAppearanceState()))
        {
            canvas.Clear(bounds, inherited);
        }

        for (var position = 0; position < length; position++)
        {
            var glyph = ResolveGlyph(position, length, buttons, thumb);
            var trackPosition = position - buttons;
            var isButton = buttons != 0 && (position is 0 || position == length - 1);
            var isThumb = !isButton && trackPosition >= thumb.Start && trackPosition < thumb.Start + thumb.Length;
            var partStyle = isButton ? buttonStyle : isThumb ? thumbStyle : trackStyle;
            Draw(canvas, PointAt(bounds, position), glyph, partStyle);
        }
    }

    private bool Commit(int value, ScrollCause cause)
    {
        value = Math.Clamp(value, Minimum, Maximum);
        var previous = _value;

        if (!SetProperty(ref _value, value, InvalidationImpact.Render, nameof(Value)))
        {
            return false;
        }

        Debug.Assert(value >= Minimum && value <= Maximum, "Committed value remains in range.");
        ValueChanged?.Invoke(this, new ScrollEventArgs(previous, value, cause));
        return true;
    }

    private void Handle(KeyEventArgs eventArgs)
    {
        if (!eventArgs.IsKeyDown)
        {
            return;
        }

        var code = eventArgs.Stroke.Code;
        var decrement = Orientation == Orientation.Vertical ? Code.Up : Code.Left;
        var increment = Orientation == Orientation.Vertical ? Code.Down : Code.Right;

        if (code == decrement)
        {
            _ = ScrollBy(SmallChange.Negate(), ScrollCause.Keyboard);
        }
        else if (code == increment)
        {
            _ = ScrollBy(SmallChange, ScrollCause.Keyboard);
        }
        else if (code == Code.PageUp)
        {
            _ = ScrollBy(LargeChange.Negate(), ScrollCause.Keyboard);
        }
        else if (code == Code.PageDown)
        {
            _ = ScrollBy(LargeChange, ScrollCause.Keyboard);
        }
        else if (code == Code.Home)
        {
            _ = Commit(Minimum, ScrollCause.Keyboard);
        }
        else if (code == Code.End)
        {
            _ = Commit(Maximum, ScrollCause.Keyboard);
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
            HandleWheel(eventArgs);
            return;
        }

        if (_drag.IsDragging)
        {
            Drag(eventArgs);
            return;
        }

        if (pointer.Action != PointerAction.Press ||
            (pointer.Buttons & Buttons.Primary) == 0 ||
            pointer.Cells is not { } cells ||
            !Bounds.Contains(cells))
        {
            return;
        }

        var bounds = ContentBounds;
        var length = AxisLength(bounds);
        var position = Axis(cells) - AxisOrigin(bounds);

        if (length == 0 || position < 0 || position >= length)
        {
            return;
        }

        _ = FocusOwner?.Focus(this);
        eventArgs.IsHandled = true;

        var buttons = ButtonCount(length);

        if (buttons != 0 && position == 0)
        {
            _ = ScrollBy(SmallChange.Negate(), ScrollCause.Pointer);
            return;
        }

        if (buttons != 0 && position == length - 1)
        {
            _ = ScrollBy(SmallChange, ScrollCause.Pointer);
            return;
        }

        var trackLength = Math.Max(0, length - (buttons * 2));
        var trackPosition = position - buttons;
        var range = CurrentRange();
        var thumb = ScrollThumb.Resolve(range, trackLength);

        if (trackPosition < thumb.Start)
        {
            _ = ScrollBy(LargeChange.Negate(), ScrollCause.Pointer);
        }
        else if (trackPosition >= thumb.Start + thumb.Length)
        {
            _ = ScrollBy(LargeChange, ScrollCause.Pointer);
        }
        else
        {
            BeginDrag(pointer, cells, trackPosition);
        }
    }

    private void HandleWheel(PointerEventArgs eventArgs)
    {
        var wheel = Orientation == Orientation.Vertical
            ? eventArgs.Pointer.WheelY
            : eventArgs.Pointer.WheelX;

        if (wheel == 0)
        {
            return;
        }

        var signed = Orientation == Orientation.Vertical ? -(long) wheel : wheel;
        var requested = signed * SmallChange;
        var delta = (int) Math.Clamp(requested, int.MinValue, int.MaxValue);

        // A pinned rail must not swallow the next viewport's wheel gesture.
        // The unchanged routed event can continue to an enclosing scroll host.
        eventArgs.IsHandled = ScrollBy(delta, ScrollCause.Wheel);
    }

    private void BeginDrag(
        Pointer pointer,
        Point cells,
        int trackPosition)
    {
        if (!_drag.TryStart(cells))
        {
            return;
        }

        _dragPointerStart = trackPosition;
        _dragPixelStart = pointer.Pixels is { } pixels ? Axis(pixels) : null;

        // Anchors the drag on the *value* the bar held when the drag began, not the thumb's
        // absolute track cell position. Value has no dependency on track geometry, so it stays
        // meaningful across a resize; an absolute cell offset does not (see Drag()'s
        // recomputation of the anchor thumb on every move for why this matters).
        _dragValueStart = Value;
    }

    private void Drag(PointerEventArgs eventArgs)
    {
        var pointer = eventArgs.Pointer;

        if (pointer.Cells is not { } cells)
        {
            eventArgs.IsHandled = true;

            if (pointer.Action == PointerAction.Leave || PointerButtonTransition.IsPrimaryRelease(pointer))
            {
                _drag.Cancel(releaseCapture: true);
                ResetDragState();
            }

            return;
        }

        var bounds = ContentBounds;
        var length = AxisLength(bounds);
        var buttons = ButtonCount(length);
        var position = Axis(cells) - AxisOrigin(bounds) - buttons;
        var delta = Difference(position, _dragPointerStart);

        if (_dragPixelStart.HasValue && pointer.Pixels is { } pixels)
        {
            var pixelDelta = Difference(Axis(pixels), _dragPixelStart.Value);
            Debug.Assert(
                delta == 0 || pixelDelta == 0 || Math.Sign(delta) == Math.Sign(pixelDelta),
                "Inferred cell and pixel drag directions must agree.");
        }

        // The track length and range are re-read from live geometry on every move rather than
        // reused from BeginDrag's captured snapshot: a resize (or a Minimum/Maximum/ViewportSize
        // change from content re-layout) while a drag is in flight would otherwise convert the
        // pointer's position back to a value using a stale track length, producing a jump or
        // drift until the drag ends.
        var trackLength = Math.Max(0, length - (buttons * 2));
        var range = CurrentRange();

        // The thumb position corresponding to the value the drag started from must be recomputed
        // against the *current* trackLength on every move, not reused from BeginDrag's snapshot:
        // a cell offset that meant "50% of the way" in a 20-cell track means something entirely
        // different added to a raw pointer-movement delta once the track has shrunk to 5 cells.
        // Anchoring on the starting *value* (which has no dependency on track geometry) and only
        // ever re-deriving its thumb position fresh keeps a resize mid-drag proportional instead
        // of snapping toward an endpoint.
        // A handler reacting to the commit below (e.g. one that narrows Minimum/Maximum in
        // response to ValueChanged) can move the live range so the value captured at BeginDrag no
        // longer falls inside it; clamping into the *current* range before using it as the
        // anchor's value keeps ScrollRange's own endpoint validation from rejecting an otherwise
        // legitimate, merely stale, anchor.
        var anchorRange = new ScrollRange(range.Minimum, range.Maximum, range.Clamp(_dragValueStart), range.Viewport);
        var anchorThumb = ScrollThumb.Resolve(anchorRange, trackLength);
        var start = anchorThumb.Start + delta;
        var value = ScrollThumb.ValueAt(range, trackLength, start);
        _ = Commit(value, ScrollCause.Pointer);
        eventArgs.IsHandled = true;

        if (pointer.Action == PointerAction.Leave || PointerButtonTransition.IsPrimaryRelease(pointer))
        {
            _drag.Cancel(releaseCapture: true);
            ResetDragState();
        }
    }

    private void ResetDragState() => _dragPixelStart = null;

    private ScrollRange CurrentRange() => new(Minimum, Maximum, Value, ViewportSize);

    private Rune ResolveGlyph(int position, int length, int buttons, ScrollThumb thumb)
    {
        var trackPosition = position - buttons;

        return buttons == 0
            ? trackPosition >= thumb.Start && trackPosition < thumb.Start + thumb.Length
                ? ThumbRune()
                : TrackRune()
            : position == 0
                ? DecrementRune()
                : position == length - 1
                    ? IncrementRune()
                    : trackPosition >= thumb.Start && trackPosition < thumb.Start + thumb.Length
                        ? ThumbRune()
                        : TrackRune();
    }

    private int ButtonCount(int length) => ActualStyle.Chrome == ScrollBarChrome.Full && length >= 2 ? 1 : 0;

    private Rune DecrementRune()
    {
        var themed = DecrementDefaultGlyph();
        return themed.Value.Resolve(themed.Fallback, CellPolicy.AmbiguousWidth);
    }

    private Rune IncrementRune()
    {
        var themed = IncrementDefaultGlyph();
        return themed.Value.Resolve(themed.Fallback, CellPolicy.AmbiguousWidth);
    }

    private Rune TrackRune()
    {
        var themed = TrackDefaultGlyph();
        return themed.Value.Resolve(themed.Fallback, CellPolicy.AmbiguousWidth);
    }

    private Rune ThumbRune()
    {
        var themed = ThumbDefaultGlyph();
        return themed.Value.Resolve(themed.Fallback, CellPolicy.AmbiguousWidth);
    }

    private ControlGlyph DecrementDefaultGlyph() => Orientation == Orientation.Vertical
        ? ActualStyle.Glyphs.VerticalDecrementGlyph
        : ActualStyle.Glyphs.HorizontalDecrementGlyph;

    private ControlGlyph IncrementDefaultGlyph() => Orientation == Orientation.Vertical
        ? ActualStyle.Glyphs.VerticalIncrementGlyph
        : ActualStyle.Glyphs.HorizontalIncrementGlyph;

    private ControlGlyph TrackDefaultGlyph() => ActualStyle.Fill == ScrollBarFill.Block
        ? ActualStyle.Glyphs.BlockTrackGlyph
        : Orientation == Orientation.Vertical
            ? ActualStyle.Glyphs.VerticalLineTrackGlyph
            : ActualStyle.Glyphs.HorizontalLineTrackGlyph;

    private ControlGlyph ThumbDefaultGlyph() => ActualStyle.Fill == ScrollBarFill.Block
        ? ActualStyle.Glyphs.BlockThumbGlyph
        : Orientation == Orientation.Vertical
            ? ActualStyle.Glyphs.VerticalLineThumbGlyph
            : ActualStyle.Glyphs.HorizontalLineThumbGlyph;

    /// <summary>Merges a nullable local style with the active theme's ControlBase profile, exactly as
    /// <see cref="ScrollBar"/> itself resolves its own style — shared so every composite host
    /// reports an ActualScrollBarStyle that matches what its generated bar actually renders
    /// instead of falling back to the code-owned static default.</summary>
    /// <param name="localStyle">The optional local style override.</param>
    /// <param name="theme">The active theme, or null to fall back to the library default theme.</param>
    /// <returns>The complete style the generated bar would resolve and render.</returns>
    [Pure]
    internal static ScrollBarStyle ResolveStyle(ScrollBarStyle? localStyle, Theme? theme) =>
        ScrollBarStyle.Definition.Resolve(localStyle, theme);

    private static void Draw(TerminalCanvas canvas, Point point, Rune glyph, TerminalStyle style) =>
        canvas.DrawRune(glyph, point, style, BackgroundMode.Transparent);

    private int Axis(Point point) => Orientation == Orientation.Vertical ? point.Y : point.X;

    private int AxisLength(Rect bounds) =>
        Orientation == Orientation.Vertical ? bounds.Height : bounds.Width;

    private int AxisOrigin(Rect bounds) => Orientation == Orientation.Vertical ? bounds.Y : bounds.X;

    private Point PointAt(Rect bounds, int position) => Orientation == Orientation.Vertical
        ? new Point(bounds.X, bounds.Y.SaturatingAdd(position))
        : new Point(bounds.X.SaturatingAdd(position), bounds.Y);

    private static int Difference(int left, int right) =>
        (int) Math.Clamp((long) left - right, int.MinValue, int.MaxValue);

}
