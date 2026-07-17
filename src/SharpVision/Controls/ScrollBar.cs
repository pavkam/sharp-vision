// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;


using SharpVision.Scrolling;
using SharpVision.Terminal.Input;

using ScrollRange = Scrolling.Range;
using UnicodeWidth = Width;

/// <summary>Defines a focusable integer range with buttons, track, and draggable thumb.</summary>
public sealed class ScrollBar: Control
{
    private int _value;
    private bool _dragging;
    private int _dragPointerStart;
    private int? _dragPixelStart;
    private int _dragThumbStart;
    private int _dragTrackLength;
    private ScrollRange _dragRange;
    private bool _hasDecrementGlyph;
    private bool _hasIncrementGlyph;
    private bool _hasTrackGlyph;
    private bool _hasThumbGlyph;
    private Rune DecrementGlyphOverride { get; set; }
    private Rune IncrementGlyphOverride { get; set; }
    private Rune TrackGlyphOverride { get; set; }
    private Rune ThumbGlyphOverride { get; set; }

    /// <summary>Initializes a vertical focusable range from zero through one hundred.</summary>
    public ScrollBar()
    {
        Focusable = true;
        TabStop = true;
        TabNavigation = TabNavigation.None;
    }

    /// <inheritdoc/>

    /// <summary>Raised after a changed value commits.</summary>
    public event EventHandler<ScrollEventArgs>? ValueChanged;

    /// <summary>Gets or sets the non-negative inclusive lower endpoint.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="ArgumentException">The value exceeds Maximum or the current Value.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int Minimum
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            if (value > Maximum || value > Value)
            {
                throw new ArgumentException(
                    "Minimum cannot exceed Maximum or the current Value.",
                    nameof(value));
            }

            _ = SetProperty(ref field, value, ChangeImpact.Render);
        }
    }

    /// <summary>Gets or sets the inclusive upper endpoint.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="ArgumentException">The value is below Minimum or the current Value.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int Maximum
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            if (value < Minimum || value < Value)
            {
                throw new ArgumentException(
                    "Maximum cannot be below Minimum or the current Value.",
                    nameof(value));
            }

            _ = SetProperty(ref field, value, ChangeImpact.Render);
        }
    } = 100;

    /// <summary>Gets or sets the non-negative visible extent represented by the thumb.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int ViewportSize
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, ChangeImpact.Render);
        }
    }

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

            _ = Commit(value, Cause.Programmatic);
        }
    }

    /// <summary>Gets or sets the non-negative line/button change.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int SmallChange
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, ChangeImpact.None);
        }
    } = 1;

    /// <summary>Gets or sets the non-negative page/track change.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int LargeChange
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, ChangeImpact.None);
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
            Validate(value);
            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    }

    /// <summary>Gets or sets compact or full scrollbar chrome.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ScrollBarChrome Chrome
    {
        get;
        set
        {
            Validate(value);
            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    } = ScrollBarChrome.Full;

    /// <summary>Gets or sets the generated line or block glyph treatment.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ScrollBarFill Fill
    {
        get;
        set
        {
            Validate(value);
            _ = SetProperty(ref field, value, ChangeImpact.Render);
        }
    } = ScrollBarFill.Block;

    /// <summary>Gets or sets the printable narrow decrement-button glyph.</summary>
    /// <exception cref="ArgumentException">The value is a control or not one cell wide.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Rune DecrementGlyph
    {
        get => _hasDecrementGlyph ? DecrementGlyphOverride : DecrementThemeGlyph().Value;
        set
        {
            var glyph = Validate(value, nameof(value));
            VerifyMutable();
            var wasCustom = _hasDecrementGlyph;
            _hasDecrementGlyph = true;

            if (DecrementGlyphOverride == glyph)
            {
                if (!wasCustom)
                {
                    NotifyPropertyChanged(nameof(DecrementGlyph), ChangeImpact.Render);
                }

                return;
            }

            DecrementGlyphOverride = glyph;
            NotifyPropertyChanged(nameof(DecrementGlyph), ChangeImpact.Render);
        }
    }

    /// <summary>Gets or sets the printable narrow increment-button glyph.</summary>
    /// <exception cref="ArgumentException">The value is a control or not one cell wide.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Rune IncrementGlyph
    {
        get => _hasIncrementGlyph ? IncrementGlyphOverride : IncrementThemeGlyph().Value;
        set
        {
            var glyph = Validate(value, nameof(value));
            VerifyMutable();
            var wasCustom = _hasIncrementGlyph;
            _hasIncrementGlyph = true;

            if (IncrementGlyphOverride == glyph)
            {
                if (!wasCustom)
                {
                    NotifyPropertyChanged(nameof(IncrementGlyph), ChangeImpact.Render);
                }

                return;
            }

            IncrementGlyphOverride = glyph;
            NotifyPropertyChanged(nameof(IncrementGlyph), ChangeImpact.Render);
        }
    }

    /// <summary>Gets or sets the printable narrow unoccupied-track glyph.</summary>
    /// <exception cref="ArgumentException">The value is a control or not one cell wide.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Rune TrackGlyph
    {
        get => _hasTrackGlyph ? TrackGlyphOverride : TrackThemeGlyph().Value;
        set
        {
            var glyph = Validate(value, nameof(value));
            VerifyMutable();
            var wasCustom = _hasTrackGlyph;
            _hasTrackGlyph = true;

            if (TrackGlyphOverride == glyph)
            {
                if (!wasCustom)
                {
                    NotifyPropertyChanged(nameof(TrackGlyph), ChangeImpact.Render);
                }

                return;
            }

            TrackGlyphOverride = glyph;
            NotifyPropertyChanged(nameof(TrackGlyph), ChangeImpact.Render);
        }
    }

    /// <summary>Gets or sets the printable narrow thumb glyph.</summary>
    /// <exception cref="ArgumentException">The value is a control or not one cell wide.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Rune ThumbGlyph
    {
        get => _hasThumbGlyph ? ThumbGlyphOverride : ThumbThemeGlyph().Value;
        set
        {
            var glyph = Validate(value, nameof(value));
            VerifyMutable();
            var wasCustom = _hasThumbGlyph;
            _hasThumbGlyph = true;

            if (ThumbGlyphOverride == glyph)
            {
                if (!wasCustom)
                {
                    NotifyPropertyChanged(nameof(ThumbGlyph), ChangeImpact.Render);
                }

                return;
            }

            ThumbGlyphOverride = glyph;
            NotifyPropertyChanged(nameof(ThumbGlyph), ChangeImpact.Render);
        }
    }

    /// <summary>Clears all local scrollbar glyphs so the active theme supplies them.</summary>
    public void ResetGlyphs()
    {
        VerifyMutable();

        if (!_hasDecrementGlyph && !_hasIncrementGlyph && !_hasTrackGlyph && !_hasThumbGlyph)
        {
            return;
        }

        _hasDecrementGlyph = false;
        _hasIncrementGlyph = false;
        _hasTrackGlyph = false;
        _hasThumbGlyph = false;
        NotifyPropertyChanged(nameof(DecrementGlyph), ChangeImpact.Render);
        NotifyPropertyChanged(nameof(IncrementGlyph), ChangeImpact.Render);
        NotifyPropertyChanged(nameof(TrackGlyph), ChangeImpact.Render);
        NotifyPropertyChanged(nameof(ThumbGlyph), ChangeImpact.Render);
    }

    /// <summary>Adds a signed command delta with saturation and endpoint clamping.</summary>
    /// <param name="delta">The signed requested change.</param>
    /// <param name="cause">The defined input path.</param>
    /// <returns>True when a changed value committed; otherwise false.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool ScrollBy(int delta, Cause cause = Cause.Programmatic)
    {
        Validate(cause);
        VerifyMutable();
        var range = CurrentRange();
        return Commit(range.Move(delta), cause);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint.Width;
        Debug.Assert(Enum.IsDefined(Orientation), "Orientation is validated before assignment.");
        var extent = Chrome == ScrollBarChrome.Thin ? 1 : 3;
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
    }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);

        if (!focused)
        {
            CancelDrag(releaseCapture: true);
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        CancelDrag(releaseCapture: false);

        if (reason == ReleaseReason.Disposed)
        {
            ValueChanged = null;
        }
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason)
    {
        base.OnLostPointerCapture(reason);
        CancelDrag(releaseCapture: false);
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
        var thumb = Thumb.Resolve(CurrentRange(), trackLength);
        var style = ResolvedStyle;

        if (ControlAppearance.HasOpaqueFill(this, GetAppearanceState()))
        {
            canvas.Clear(bounds, style);
        }

        for (var position = 0; position < length; position++)
        {
            var glyph = ResolveGlyph(position, length, buttons, thumb);
            Draw(canvas, PointAt(bounds, position), glyph, style);
        }
    }

    private bool Commit(int value, Cause cause)
    {
        value = Math.Clamp(value, Minimum, Maximum);
        var previous = _value;

        if (!SetProperty(ref _value, value, ChangeImpact.Render, nameof(Value)))
        {
            return false;
        }

        Debug.Assert(value >= Minimum && value <= Maximum, "Committed value remains in range.");
        ValueChanged?.Invoke(this, new ScrollEventArgs(previous, value, cause));
        return true;
    }

    private void Handle(KeyEventArgs eventArgs)
    {
        if (eventArgs.Stroke.Action is not (KeyAction.Press or KeyAction.Repeat))
        {
            return;
        }

        var code = eventArgs.Stroke.Code;
        var decrement = Orientation == Orientation.Vertical ? Code.Up : Code.Left;
        var increment = Orientation == Orientation.Vertical ? Code.Down : Code.Right;

        if (code == decrement)
        {
            _ = ScrollBy(Negate(SmallChange), Cause.Keyboard);
        }
        else if (code == increment)
        {
            _ = ScrollBy(SmallChange, Cause.Keyboard);
        }
        else if (code == Code.PageUp)
        {
            _ = ScrollBy(Negate(LargeChange), Cause.Keyboard);
        }
        else if (code == Code.PageDown)
        {
            _ = ScrollBy(LargeChange, Cause.Keyboard);
        }
        else if (code == Code.Home)
        {
            _ = Commit(Minimum, Cause.Keyboard);
        }
        else if (code == Code.End)
        {
            _ = Commit(Maximum, Cause.Keyboard);
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
            HandleWheel(eventArgs);
            return;
        }

        if (_dragging)
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
        eventArgs.Handled = true;

        var buttons = ButtonCount(length);

        if (buttons != 0 && position == 0)
        {
            _ = ScrollBy(Negate(SmallChange), Cause.Pointer);
            return;
        }

        if (buttons != 0 && position == length - 1)
        {
            _ = ScrollBy(SmallChange, Cause.Pointer);
            return;
        }

        var trackLength = Math.Max(0, length - (buttons * 2));
        var trackPosition = position - buttons;
        var range = CurrentRange();
        var thumb = Thumb.Resolve(range, trackLength);

        if (trackPosition < thumb.Start)
        {
            _ = ScrollBy(Negate(LargeChange), Cause.Pointer);
        }
        else if (trackPosition >= thumb.Start + thumb.Length)
        {
            _ = ScrollBy(LargeChange, Cause.Pointer);
        }
        else
        {
            BeginDrag(pointer, trackPosition, trackLength, thumb, range);
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

        var requested = -(long) wheel * SmallChange;
        var delta = (int) Math.Clamp(requested, int.MinValue, int.MaxValue);

        // A pinned rail must not swallow the next viewport's wheel gesture.
        // The unchanged routed event can continue to an enclosing scroll host.
        eventArgs.Handled = ScrollBy(delta, Cause.Wheel);
    }

    private void BeginDrag(
        Pointer pointer,
        int trackPosition,
        int trackLength,
        Thumb thumb,
        ScrollRange range)
    {
        var capture = CaptureOwner;

        if (capture is null || !capture.Capture(this))
        {
            return;
        }

        _dragging = true;
        _dragPointerStart = trackPosition;
        _dragPixelStart = pointer.Pixels is { } pixels ? Axis(pixels) : null;
        _dragThumbStart = thumb.Start;
        _dragTrackLength = trackLength;
        _dragRange = range;
        SetPressed(true);
    }

    private void Drag(PointerEventArgs eventArgs)
    {
        var pointer = eventArgs.Pointer;

        if (pointer.Cells is not { } cells)
        {
            eventArgs.Handled = true;

            if (pointer.Action is PointerAction.Release or PointerAction.Leave)
            {
                CancelDrag(releaseCapture: true);
            }

            return;
        }

        var bounds = ContentBounds;
        var buttons = ButtonCount(AxisLength(bounds));
        var position = Axis(cells) - AxisOrigin(bounds) - buttons;
        var delta = Difference(position, _dragPointerStart);

        if (_dragPixelStart.HasValue && pointer.Pixels is { } pixels)
        {
            var pixelDelta = Difference(Axis(pixels), _dragPixelStart.Value);
            Debug.Assert(
                delta == 0 || pixelDelta == 0 || Math.Sign(delta) == Math.Sign(pixelDelta),
                "Inferred cell and pixel drag directions must agree.");
        }

        var start = SaturatingAdd(_dragThumbStart, delta);
        var value = Thumb.ValueAt(_dragRange, _dragTrackLength, start);
        _ = Commit(value, Cause.Pointer);
        eventArgs.Handled = true;

        if (pointer.Action is PointerAction.Release or PointerAction.Leave)
        {
            CancelDrag(releaseCapture: true);
        }
    }

    private void CancelDrag(bool releaseCapture)
    {
        _dragging = false;
        _dragPixelStart = null;
        SetPressed(false);

        if (releaseCapture && CaptureOwner?.Captured is { } captured && ReferenceEquals(captured, this))
        {
            CaptureOwner.Release();
        }
    }

    private ScrollRange CurrentRange() => new(Minimum, Maximum, Value, ViewportSize);

    private Rune ResolveGlyph(int position, int length, int buttons, Thumb thumb)
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

    private int ButtonCount(int length) => Chrome == ScrollBarChrome.Full && length >= 2 ? 1 : 0;

    private Rune DecrementRune()
    {
        var themed = DecrementThemeGlyph();
        return CellGlyph.Resolve(DecrementGlyph, themed.Fallback, CellPolicy.AmbiguousWidth);
    }

    private Rune IncrementRune()
    {
        var themed = IncrementThemeGlyph();
        return CellGlyph.Resolve(IncrementGlyph, themed.Fallback, CellPolicy.AmbiguousWidth);
    }

    private Rune TrackRune()
    {
        var themed = TrackThemeGlyph();
        return CellGlyph.Resolve(TrackGlyph, themed.Fallback, CellPolicy.AmbiguousWidth);
    }

    private Rune ThumbRune()
    {
        var themed = ThumbThemeGlyph();
        return CellGlyph.Resolve(ThumbGlyph, themed.Fallback, CellPolicy.AmbiguousWidth);
    }

    private ThemedGlyph DecrementThemeGlyph() => Orientation == Orientation.Vertical
        ? ResolveThemeGlyphs().ScrollBars.VerticalDecrement
        : ResolveThemeGlyphs().ScrollBars.HorizontalDecrement;

    private ThemedGlyph IncrementThemeGlyph() => Orientation == Orientation.Vertical
        ? ResolveThemeGlyphs().ScrollBars.VerticalIncrement
        : ResolveThemeGlyphs().ScrollBars.HorizontalIncrement;

    private ThemedGlyph TrackThemeGlyph() => Fill == ScrollBarFill.Block
        ? ResolveThemeGlyphs().ScrollBars.BlockTrack
        : Orientation == Orientation.Vertical
            ? ResolveThemeGlyphs().ScrollBars.VerticalLineTrack
            : ResolveThemeGlyphs().ScrollBars.HorizontalLineTrack;

    private ThemedGlyph ThumbThemeGlyph() => Fill == ScrollBarFill.Block
        ? ResolveThemeGlyphs().ScrollBars.BlockThumb
        : Orientation == Orientation.Vertical
            ? ResolveThemeGlyphs().ScrollBars.VerticalLineThumb
            : ResolveThemeGlyphs().ScrollBars.HorizontalLineThumb;

    private static void Draw(TerminalCanvas canvas, Point point, Rune glyph, TerminalStyle style) =>
        canvas.DrawRune(glyph, point, style, BackgroundMode.Transparent);

    private int Axis(Point point) => Orientation == Orientation.Vertical ? point.Y : point.X;

    private int AxisLength(Rect bounds) =>
        Orientation == Orientation.Vertical ? bounds.Height : bounds.Width;

    private int AxisOrigin(Rect bounds) => Orientation == Orientation.Vertical ? bounds.Y : bounds.X;

    private Point PointAt(Rect bounds, int position) => Orientation == Orientation.Vertical
        ? new Point(bounds.X, SaturatingAdd(bounds.Y, position))
        : new Point(SaturatingAdd(bounds.X, position), bounds.Y);

    private static int Difference(int left, int right) =>
        (int) Math.Clamp((long) left - right, int.MinValue, int.MaxValue);

    private static int SaturatingAdd(int left, int right) =>
        (int) Math.Clamp((long) left + right, int.MinValue, int.MaxValue);

    private static int Negate(int value) => value == int.MinValue ? int.MaxValue : -value;

    private static void Validate<T>(T value) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The enum value is unknown.");
        }
    }

    private static Rune Validate(Rune value, string name)
    {
        Span<char> buffer = stackalloc char[2];
        var length = value.EncodeToUtf16(buffer);
        var measurement = UnicodeWidth.Measure(buffer[..length]);

        return measurement.Cells == 1 && measurement.Controls == 0
            ? value
            : throw new ArgumentException("A scrollbar glyph must be printable and one cell wide.", name);
    }
}
