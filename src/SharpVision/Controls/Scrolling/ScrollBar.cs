// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Scrolling;

using SharpVision.Terminal.Input;

/// <summary>Defines a focusable integer range with buttons, track, and draggable thumb.</summary>
[PublicAPI]
public sealed class ScrollBar: Control
{
    private static readonly StyleContract<ScrollBarStyle> _styleContract = new(
        ThemeRole.Control,
        static profile => new ScrollBarStyle(
            ScrollBarStyle.Default.Chrome,
            ScrollBarStyle.Default.Fill,
            ScrollBarStyle.Default.Glyphs,
            ScrollBarStyle.Default.TrackColor,
            ScrollBarStyle.Default.ThumbColor,
            ScrollBarStyle.Default.ButtonColor,
            profile),
        static (previous, previousTheme, current, currentTheme) =>
            previous.Chrome != current.Chrome
                ? InvalidationImpact.Measure
                : previous != current ||
                  ResolveColor(previous.TrackColor, previousTheme) != ResolveColor(current.TrackColor, currentTheme) ||
                  ResolveColor(previous.ThumbColor, previousTheme) != ResolveColor(current.ThumbColor, currentTheme) ||
                  ResolveColor(previous.ButtonColor, previousTheme) != ResolveColor(current.ButtonColor, currentTheme)
                    ? InvalidationImpact.Render
                    : InvalidationImpact.None,
        static style => style.Appearance);
    private int _value;
    private readonly DragBehavior _drag;
    private int _dragPointerStart;
    private int? _dragPixelStart;
    private int _dragThumbStart;
    private int _dragTrackLength;
    private ScrollRange _dragRange;
    private ScrollBarStyle? _actualStyleCache;
    private ScrollBarStyle? _actualStyleCacheKey;
    private Theme? _actualStyleCacheTheme;

    /// <summary>Initializes a vertical focusable range from zero through one hundred.</summary>
    public ScrollBar()
    {
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

            _ = SetProperty(ref field, value, InvalidationImpact.Render);
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

            _ = SetProperty(ref field, value, InvalidationImpact.Render);
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
            _ = SetProperty(ref field, value, InvalidationImpact.Render);
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

            _ = Commit(value, ScrollCause.Programmatic);
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
            _ = SetProperty(ref field, value, InvalidationImpact.None);
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
            value.ValidateDefined();
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets or sets the complete local presentation, or null to use the semantic control profile.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ScrollBarStyle? Style
    {
        get;
        set => _ = SetControlStyle(
            ref field,
            value,
            _styleContract.Resolve,
            _styleContract.CompareStructure,
            _styleContract.Appearance,
            nameof(Style),
            nameof(ActualStyle));
    }

    /// <summary>Gets the complete local presentation or library scrollbar mechanics completed with the semantic control profile.</summary>
    public ScrollBarStyle ActualStyle =>
        ResolveContractStyle(
            _styleContract,
            ref _actualStyleCache,
            ref _actualStyleCacheKey,
            ref _actualStyleCacheTheme,
            Style,
            Theme);

    /// <inheritdoc/>
    protected override ThemeRole ThemeRole => _styleContract.Role;

    /// <inheritdoc/>
    protected override ThemeProfile AppearanceProfile => ActualStyle.Appearance;

    /// <inheritdoc/>
    protected override ThemeProfile GetAppearanceProfile(Theme? theme) =>
        GetContractAppearanceProfile(_styleContract, Style, theme);

    /// <inheritdoc/>
    protected override InvalidationImpact GetThemeChangeImpact(
        Theme? previous,
        Theme? current,
        Face? previousParentAmbientFace,
        Face? currentParentAmbientFace) =>
        GetContractThemeChangeImpact(
            _styleContract,
            Style,
            previous,
            current,
            previousParentAmbientFace,
            currentParentAmbientFace);

    /// <inheritdoc/>
    protected override string? GetThemeResolvedStylePropertyName(Theme? previous, Theme? current) =>
        GetContractResolvedStylePropertyName(_styleContract, Style, previous, current, nameof(ActualStyle));

    /// <summary>Adds a signed command delta with saturation and endpoint clamping.</summary>
    /// <param name="delta">The signed requested change.</param>
    /// <param name="cause">The defined input path.</param>
    /// <returns>True when a changed value committed; otherwise false.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool ScrollBy(int delta, ScrollCause cause = ScrollCause.Programmatic)
    {
        cause.ValidateDefined();
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
        if (eventArgs.Stroke.Action is not (KeyAction.Press or KeyAction.Repeat))
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
        eventArgs.Handled = true;

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
            BeginDrag(pointer, cells, trackPosition, trackLength, thumb, range);
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
        eventArgs.Handled = ScrollBy(delta, ScrollCause.Wheel);
    }

    private void BeginDrag(
        Pointer pointer,
        Point cells,
        int trackPosition,
        int trackLength,
        ScrollThumb thumb,
        ScrollRange range)
    {
        if (!_drag.TryStart(cells))
        {
            return;
        }

        _dragPointerStart = trackPosition;
        _dragPixelStart = pointer.Pixels is { } pixels ? Axis(pixels) : null;
        _dragThumbStart = thumb.Start;
        _dragTrackLength = trackLength;
        _dragRange = range;
    }

    private void Drag(PointerEventArgs eventArgs)
    {
        var pointer = eventArgs.Pointer;

        if (pointer.Cells is not { } cells)
        {
            eventArgs.Handled = true;

            if (pointer.Action is PointerAction.Release or PointerAction.Leave)
            {
                _drag.Cancel(releaseCapture: true);
                ResetDragState();
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

        var start = _dragThumbStart.SaturatingAdd(delta);
        var value = ScrollThumb.ValueAt(_dragRange, _dragTrackLength, start);
        _ = Commit(value, ScrollCause.Pointer);
        eventArgs.Handled = true;

        if (pointer.Action is PointerAction.Release or PointerAction.Leave)
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

    private Color ResolveColor(ColorValue value) => ResolveColor(value, Theme);

    /// <summary>Merges a nullable local style with the active theme's Control profile, exactly as
    /// <see cref="ScrollBar"/> itself resolves its own style — shared so every composite host
    /// reports an ActualScrollBarStyle that matches what its generated bar actually renders
    /// instead of falling back to the code-owned static default (see #159).</summary>
    /// <param name="localStyle">The optional local style override.</param>
    /// <param name="theme">The active theme, or null to fall back to the library default theme.</param>
    /// <returns>The complete style the generated bar would resolve and render.</returns>
    internal static ScrollBarStyle ResolveStyle(ScrollBarStyle? localStyle, Theme? theme) =>
        _styleContract.Resolve(localStyle, theme);

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
