// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using SharpVision.Terminal.Input;

/// <summary>Defines a bordered field control for editing <see cref="TimeOnly"/> values using inline segment editing.</summary>
/// <remarks>
/// Each time segment (hour, minute, second, AM/PM) is independently editable.
/// Up/Down arrows increment or decrement the focused segment. Left/Right arrows
/// navigate between segments. Typing digits replaces the segment value.
/// Delete or Backspace clears the value to null when <see cref="AllowNull"/> is set.
/// </remarks>
[PublicAPI]
public sealed class TimeInput: Control
{
    /// <inheritdoc/>
    protected override ThemeRole ThemeRole => ThemeRole.Input;

    // Time faces use two-cell numeric segments, one-cell separators, and one content row.
    private const int _segmentWidth = 2;
    private const int _separatorWidth = 1;
    private const int _contentHeight = 1;
    private const int _segmentHour = 0;
    private const int _segmentMinute = 1;
    private const int _segmentSecond = 2;
    private const int _segmentAmPm = 3;
    private const int _baseFormatWidth = (_segmentWidth * 2) + _separatorWidth;
    private const int _optionalSegmentWidth = _segmentWidth + _separatorWidth;

    private TimeOnly? _value;
    private int _activeSegment;
    private int? _digitBuffer;

    #region Construction and properties

    /// <summary>Initializes a focusable time input at the current local time with a light field border.</summary>
    public TimeInput()
    {
        _value = TimeOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime);
        Focusable = true;
        TabStop = true;
        TabNavigation = TabNavigation.None;
    }

    /// <summary>Raised after a committed value transition.</summary>
    public event EventHandler<TimeInputValueChangedEventArgs>? ValueChanged;

    /// <summary>Gets or sets the current time value, or null when cleared.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public TimeOnly? Value
    {
        get => _value;
        set => Commit(value);
    }

    /// <summary>Gets or sets whether the value may be cleared to null. Default is true.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool AllowNull
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Render);
    } = true;

    /// <summary>Gets or sets whether a 24-hour clock is displayed. Default is true.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool Use24HourFormat
    {
        get;
        set
        {
            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                _activeSegment = Math.Min(_activeSegment, LastSegment());
                _digitBuffer = null;
            }
        }
    } = true;

    /// <summary>Gets or sets whether the seconds segment is displayed. Default is false.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool ShowSeconds
    {
        get;
        set
        {
            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                _activeSegment = Math.Min(_activeSegment, LastSegment());
                _digitBuffer = null;
            }
        }
    }

    /// <summary>Gets or sets the increment used when the minute segment is adjusted.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero, negative, or not a whole minute.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public TimeSpan TimeStep
    {
        get;
        set
        {
            if (value <= TimeSpan.Zero || value.TotalMinutes != Math.Truncate(value.TotalMinutes))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "TimeStep must be a positive whole number of minutes.");
            }

            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets the inclusive lower bound for the value. Default is <see cref="TimeOnly.MinValue"/>.</summary>
    /// <exception cref="ArgumentException">The minimum exceeds <see cref="MaximumTime"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public TimeOnly MinimumTime
    {
        get;
        set
        {
            if (value > MaximumTime)
            {
                throw new ArgumentException("Minimum time cannot exceed maximum time.", nameof(value));
            }

            if (SetProperty(ref field, value, InvalidationImpact.Render))
            {
                ClampCurrentValue();
            }
        }
    } = TimeOnly.MinValue;

    /// <summary>Gets or sets the inclusive upper bound for the value. Default is <see cref="TimeOnly.MaxValue"/>.</summary>
    /// <exception cref="ArgumentException">The maximum is below <see cref="MinimumTime"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public TimeOnly MaximumTime
    {
        get;
        set
        {
            if (value < MinimumTime)
            {
                throw new ArgumentException("Maximum time cannot be less than minimum time.", nameof(value));
            }

            if (SetProperty(ref field, value, InvalidationImpact.Render))
            {
                ClampCurrentValue();
            }
        }
    } = TimeOnly.MaxValue;

    #endregion

    #region Layout

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(FormatWidth(), _contentHeight);
    }

    #endregion

    #region Input

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (!EffectiveIsEnabled || !EffectiveIsVisible)
        {
            base.OnEvent(eventArgs);
            return;
        }

        if (eventArgs is KeyEventArgs key)
        {
            HandleKey(key);
        }
        else if (eventArgs is PointerEventArgs pointer)
        {
            HandlePointer(pointer);
        }

        if (!eventArgs.Handled)
        {
            base.OnEvent(eventArgs);
        }
    }

    private void HandleKey(KeyEventArgs eventArgs)
    {
        var stroke = eventArgs.Stroke;

        if (stroke.Action is not (KeyAction.Press or KeyAction.Repeat))
        {
            return;
        }

#pragma warning disable IDE0072 // Unknown or unsupported keys intentionally remain unhandled.
        var handled = stroke.Code switch
        {
            Code.Left => MoveSegment(-1),
            Code.Right => MoveSegment(1),
            Code.Up => IncrementSegment(1),
            Code.Down => IncrementSegment(-1),
            Code.Home => MoveToEdge(first: true),
            Code.End => MoveToEdge(first: false),
            Code.Delete => ClearValue(),
            Code.Backspace => ClearSegment(),
            Code.Character when stroke.Character is { } ch && IsDigit(ch) => TypeDigit(ch.Value - '0'),
            Code.Character when stroke.Character is { } ch && IsAmPmToggle(ch) => ToggleAmPm(),
            _ => false
        };
#pragma warning restore IDE0072

        if (handled)
        {
            eventArgs.Handled = true;
        }
    }

    private void HandlePointer(PointerEventArgs eventArgs)
    {
        var pointer = eventArgs.Pointer;

        if (pointer.Action != PointerAction.Press ||
            (pointer.Buttons & Buttons.Primary) == 0)
        {
            return;
        }

        if (pointer.Cells is not { } cells)
        {
            return;
        }

        var content = ContentBounds;

        if (!content.Contains(cells))
        {
            return;
        }

        var localX = cells.X - content.X;
        var segment = SegmentAtColumn(localX);

        if (segment >= 0)
        {
            _activeSegment = segment;
            _digitBuffer = null;
            Invalidate(InvalidationImpact.Render);
        }

        if (!IsFocused)
        {
            _ = RequestFocus();
        }

        eventArgs.Handled = true;
    }

    private bool MoveSegment(int direction)
    {
        var target = _activeSegment + direction;

        if (target < 0 || target > LastSegment())
        {
            return false;
        }

        _activeSegment = target;
        _digitBuffer = null;
        Invalidate(InvalidationImpact.Render);
        return true;
    }

    private bool MoveToEdge(bool first)
    {
        var target = first ? 0 : LastSegment();

        if (_activeSegment == target)
        {
            return false;
        }

        _activeSegment = target;
        _digitBuffer = null;
        Invalidate(InvalidationImpact.Render);
        return true;
    }

    private bool IncrementSegment(int delta)
    {
        if (!_value.HasValue)
        {
            return Commit(ClampToRange(TimeOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime)));
        }

        var time = _value.Value;
        var amPmIndex = AmPmSegmentIndex();
        var result = _activeSegment switch
        {
            _segmentHour => AddWithoutWrap(time, TimeSpan.TicksPerHour * delta),
            _segmentMinute => AddWithoutWrap(time, TimeStep.Ticks * delta),
            _ when _activeSegment == amPmIndex && !Use24HourFormat =>
                time.AddHours(time.Hour < 12 ? 12 : -12),
            _ when ShowSeconds && _activeSegment == _segmentSecond =>
                AddWithoutWrap(time, TimeSpan.TicksPerSecond * delta),
            _ => time
        };

        _digitBuffer = null;
        return Commit(result);
    }

    private static TimeOnly AddWithoutWrap(TimeOnly value, long ticks)
        => ticks switch
        {
            > 0 when ticks > TimeOnly.MaxValue.Ticks - value.Ticks => TimeOnly.MaxValue,
            < 0 when ticks < TimeOnly.MinValue.Ticks - value.Ticks => TimeOnly.MinValue,
            _ => new TimeOnly(value.Ticks + ticks)
        };

    private bool TypeDigit(int digit)
    {
        if (_activeSegment == AmPmSegmentIndex())
        {
            return false;
        }

        if (!_value.HasValue)
        {
            _ = Commit(ClampToRange(TimeOnly.MinValue));

            if (!_value.HasValue)
            {
                return false;
            }
        }

        var time = _value.Value;

        if (_digitBuffer.HasValue)
        {
            var newValue = (_digitBuffer.Value * 10) + digit;
            _digitBuffer = null;

            var committed = ApplySegmentValue(time, _activeSegment, newValue);

            if (_activeSegment < LastSegment() && _activeSegment != AmPmSegmentIndex())
            {
                _activeSegment++;
                Invalidate(InvalidationImpact.Render);
            }

            return committed;
        }

        var maxFirst = _activeSegment switch
        {
            _segmentHour when Use24HourFormat => 2,
            _segmentHour => 1,
            _segmentMinute => 5,
            _segmentSecond => 5,
            _ => 0
        };

        if (digit > maxFirst)
        {
            var committed = ApplySegmentValue(time, _activeSegment, digit);

            if (_activeSegment < LastSegment() && _activeSegment != AmPmSegmentIndex())
            {
                _activeSegment++;
                Invalidate(InvalidationImpact.Render);
            }

            return committed;
        }

        _digitBuffer = digit;
        return ApplySegmentValue(time, _activeSegment, digit);
    }

    private bool ApplySegmentValue(TimeOnly time, int segment, int value)
    {
        var clamped = segment switch
        {
            _segmentHour when Use24HourFormat => Math.Clamp(value, 0, 23),
            _segmentHour => Math.Clamp(value, 1, 12),
            _segmentMinute => Math.Clamp(value, 0, 59),
            _segmentSecond => Math.Clamp(value, 0, 59),
            _ => value
        };

        var result = segment switch
        {
            _segmentHour when !Use24HourFormat =>
                new TimeOnly(To24Hour(clamped, time.Hour >= 12), time.Minute, time.Second),
            _segmentHour =>
                new TimeOnly(clamped, time.Minute, time.Second),
            _segmentMinute =>
                new TimeOnly(time.Hour, clamped, time.Second),
            _segmentSecond =>
                new TimeOnly(time.Hour, time.Minute, clamped),
            _ => time
        };

        return Commit(result);
    }

    private bool ToggleAmPm()
    {
        if (Use24HourFormat || !_value.HasValue)
        {
            return false;
        }

        var time = _value.Value;
        _activeSegment = AmPmSegmentIndex();
        _digitBuffer = null;
        return Commit(time.AddHours(time.Hour < 12 ? 12 : -12));
    }

    private bool ClearSegment()
    {
        if (!_value.HasValue)
        {
            return false;
        }

        _digitBuffer = null;
        var time = _value.Value;
        var result = _activeSegment switch
        {
            _segmentHour => new TimeOnly(0, time.Minute, time.Second),
            _segmentMinute => new TimeOnly(time.Hour, 0, time.Second),
            _segmentSecond when ShowSeconds => new TimeOnly(time.Hour, time.Minute, 0),
            _ => time
        };

        return Commit(result);
    }

    private bool ClearValue() =>
        AllowNull && _value.HasValue && Commit(null);

    private static bool IsDigit(Rune character) =>
        character.Value is >= '0' and <= '9';

    private static bool IsAmPmToggle(Rune character) =>
        character.Value is 'a' or 'A' or 'p' or 'P';

    #endregion

    #region Commit and validation

    private bool Commit(TimeOnly? requested)
    {
        var previous = _value;
        var clamped = requested.HasValue
            ? ClampToRange(requested.Value)
            : AllowNull ? null : _value;

        if (!SetProperty(ref _value, clamped, InvalidationImpact.Render, nameof(Value)))
        {
            return false;
        }

        ValueChanged?.Invoke(this, new TimeInputValueChangedEventArgs(previous, clamped));
        return true;
    }

    private TimeOnly ClampToRange(TimeOnly time) =>
        time < MinimumTime ? MinimumTime
        : time > MaximumTime ? MaximumTime
        : time;

    private void ClampCurrentValue()
    {
        if (_value.HasValue)
        {
            _ = Commit(ClampToRange(_value.Value));
        }
    }

    #endregion

    #region Rendering

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var content = ContentBounds;

        if (content.Width == 0 || content.Height == 0)
        {
            return;
        }

        var style = ResolvedStyle;
        var highlight = new TerminalStyle(
            style.Foreground,
            style.Background,
            style.Attributes | TerminalAttributes.Reverse);

        if (!_value.HasValue)
        {
            DrawPlaceholder(canvas, content, style);
            return;
        }

        var time = _value.Value;
        var x = content.X;
        var y = content.Y;
        var focused = IsFocused;

        // Hour segment.
        var hourText = FormatHour(time);
        var hourStyle = focused && _activeSegment == _segmentHour ? highlight : style;
        _ = canvas.Draw(hourText.AsSpan(), new Point(x, y), hourStyle, background: BackgroundMode.Transparent);
        x += _segmentWidth;

        // First separator.
        _ = canvas.Draw(":".AsSpan(), new Point(x, y), style, background: BackgroundMode.Transparent);
        x += _separatorWidth;

        // Minute segment.
        var minuteText = time.Minute.ToString("D2", CultureInfo.InvariantCulture);
        var minuteStyle = focused && _activeSegment == _segmentMinute ? highlight : style;
        _ = canvas.Draw(minuteText.AsSpan(), new Point(x, y), minuteStyle, background: BackgroundMode.Transparent);
        x += _segmentWidth;

        // Second segment.
        if (ShowSeconds)
        {
            _ = canvas.Draw(":".AsSpan(), new Point(x, y), style, background: BackgroundMode.Transparent);
            x += _separatorWidth;

            var secondText = time.Second.ToString("D2", CultureInfo.InvariantCulture);
            var secondStyle = focused && _activeSegment == _segmentSecond ? highlight : style;
            _ = canvas.Draw(secondText.AsSpan(), new Point(x, y), secondStyle, background: BackgroundMode.Transparent);
            x += _segmentWidth;
        }

        // AM/PM designator.
        if (!Use24HourFormat)
        {
            _ = canvas.Draw(" ".AsSpan(), new Point(x, y), style, background: BackgroundMode.Transparent);
            x += _separatorWidth;

            var amPmText = time.Hour < 12 ? "AM" : "PM";
            var amPmStyle = focused && _activeSegment == AmPmSegmentIndex() ? highlight : style;
            _ = canvas.Draw(amPmText.AsSpan(), new Point(x, y), amPmStyle, background: BackgroundMode.Transparent);
        }
    }

    private void DrawPlaceholder(TerminalCanvas canvas, Rect content, TerminalStyle style)
    {
        var placeholder = Use24HourFormat
            ? ShowSeconds ? "--:--:--" : "--:--"
            : ShowSeconds ? "--:--:-- --" : "--:-- --";
        _ = canvas.Draw(
            placeholder.AsSpan(),
            new Point(content.X, content.Y),
            style,
            background: BackgroundMode.Transparent);
    }

    private string FormatHour(TimeOnly time)
    {
        if (Use24HourFormat)
        {
            return time.Hour.ToString("D2", CultureInfo.InvariantCulture);
        }

        var hour12 = time.Hour % 12;

        if (hour12 == 0)
        {
            hour12 = 12;
        }

        return hour12.ToString("D2", CultureInfo.InvariantCulture);
    }

    #endregion

    #region Segment helpers

    private int LastSegment() =>
        !Use24HourFormat ? AmPmSegmentIndex()
        : ShowSeconds ? _segmentSecond
        : _segmentMinute;

    private int AmPmSegmentIndex() =>
        ShowSeconds ? _segmentAmPm : _segmentSecond;

    private int SegmentAtColumn(int column) =>
        // Layout: "HH:mm" or "HH:mm:ss" or "HH:mm AM" or "HH:mm:ss AM"
        // Columns: 01 2 34 5 67 8 9A
        column < _segmentWidth ? _segmentHour
        : column < _baseFormatWidth ? _segmentMinute
        : ShowSeconds && column < _baseFormatWidth + _optionalSegmentWidth ? _segmentSecond
        : !Use24HourFormat ? AmPmSegmentIndex()
        : ShowSeconds ? _segmentSecond
        : _segmentMinute;

    private int FormatWidth()
    {
        // "HH:mm" = 5, "HH:mm:ss" = 8, "HH:mm AM" = 8, "HH:mm:ss AM" = 11
        var width = _baseFormatWidth;

        if (ShowSeconds)
        {
            width += _optionalSegmentWidth;
        }

        if (!Use24HourFormat)
        {
            width += _optionalSegmentWidth;
        }

        return width;
    }

    private static int To24Hour(int hour12, bool isPm) =>
        hour12 == 12 ? isPm ? 12 : 0
        : isPm ? hour12 + 12
        : hour12;

    #endregion

    #region Lifecycle

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);

        if (!focused)
        {
            _digitBuffer = null;
        }

        Invalidate(InvalidationImpact.Render);
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            ValueChanged = null;
        }
    }

    #endregion
}
