// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using Popups;

using SharpVision.Terminal.Input;

/// <summary>Combines date and time editing in one bordered field control with an optional calendar popup.</summary>
/// <remarks>
/// The control displays a formatted <see cref="DateTime"/> value with date segments (month, day, year)
/// followed by time segments (hour, minute, optionally second, optionally AM/PM).
/// Date segments open a <see cref="Calendar"/> popup on activation; time segments edit inline.
/// Up/Down arrows increment or decrement the focused segment. Left/Right arrows navigate between segments.
/// Typing digits replaces the segment value. Delete clears the value to null when <see cref="AllowNull"/> is set.
/// Alt+Down opens the calendar popup from any segment.
/// </remarks>
[PublicAPI]
public sealed class DateTimeInput: ControlBase
{
    /// <inheritdoc/>
    protected override ThemeRole ThemeRole => ThemeRole.Input;

    // Terminal field geometry: one content row, two border columns, and one indicator cell.
    private const int _fieldContentHeight = 1;
    private const int _fieldBorderWidth = 2;
    private const int _indicatorWidth = 1;
    private const int _calendarPopupHeight = 10;
    // Date/time faces use fixed-width terminal cells for numeric segments and separators.
    private const int _dateSegmentWidth = 2;
    private const int _yearSegmentWidth = 4;
    private const int _dateSeparatorWidth = 1;
    private const int _dateTimeSeparatorWidth = 1;
    private const int _timeSegmentWidth = 2;
    private const int _timeSeparatorWidth = 1;
    private const int _singleCellWidth = 1;
    private const int _segmentMonth = 0;
    private const int _segmentDay = 1;
    private const int _segmentYear = 2;
    private const int _segmentHour = 3;
    private const int _segmentMinute = 4;
    private const int _segmentSecond = 5;
    private const int _segmentAmPm = 6;

    private readonly Calendar _calendar;
    private readonly Popup _popup;
    private readonly OwnedControlSlot _popupSlot;
    private readonly PressBehavior _interaction;
    private readonly PopupModalTracker _modalTracker;

    private DateTime? _value;
    private CultureInfo _culture;

    private int _activeSegment;
    private int? _digitBuffer;
    private int _yearDigitCount;
    private Rune? _dropDownGlyph;

    #region Construction and properties

    /// <summary>Initializes a focusable date-time input at the current local date and time with a light field border and a connected calendar popup.</summary>
    public DateTimeInput()
    {
        _value = TimeProvider.System.GetLocalNow().DateTime;
        _culture = CultureInfo.CurrentCulture.DateTimeFormat.Calendar is GregorianCalendar
            ? CultureInfo.CurrentCulture
            : CultureInfo.InvariantCulture;
        _calendar = new Calendar
        {
            SelectionMode = CalendarSelectionMode.Select,
            TabStop = false
        };

        _popup = new Popup
        {
            Anchor = this,
            Content = _calendar,
            FocusOnOpen = false,
            ModalBehavior = PopupModalBehavior.None,
            TabNavigation = TabNavigation.None,
            ConnectsToAnchor = true
        };
        _popup.Opened += OnPopupOpened;
        _popup.Closing += OnPopupClosing;
        _popup.Closed += OnPopupClosed;
        _modalTracker = new PopupModalTracker(_popup, () => IsOpen = false);

        // Register event handler after _popup is created to avoid NullReferenceException
        // when setting _calendar.Selection fires SelectionChanged → IsOpen accessor.
        _calendar.SelectionChanged += OnCalendarSelectionChanged;

        if (_value.HasValue)
        {
            _calendar.Selection = new DateInterval(
                DateOnly.FromDateTime(_value.Value),
                DateOnly.FromDateTime(_value.Value));
        }

        _popupSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Popup,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: "drop-down",
                InvalidationImpact.Measure),
            capacity: 1);
        _popupSlot.Add(_popup);
        _interaction = new PressBehavior(
            () => Bounds,
            () => EffectiveIsEnabled && EffectiveIsVisible,
            () => FocusOwner is null || IsFocused,
            RequestFocus,
            CapturePointer,
            () => HasPointerCapture,
            ReleasePointerCapture,
            SetPressed,
            Activate);
        Focusable = true;
        TabStop = true;
        TabNavigation = TabNavigation.None;
    }

    /// <summary>Raised after a committed value transition.</summary>
    public event EventHandler<DateTimeInputValueChangedEventArgs>? ValueChanged;

    /// <summary>Gets or sets the current date-time value, or null when cleared.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public DateTime? Value
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
        set
        {
            if (SetProperty(ref field, value, InvalidationImpact.None) && !value && _value is null)
            {
                _ = Commit(ClampToRange(TimeProvider.System.GetLocalNow().DateTime));
            }
        }
    } = true;

    /// <summary>
    /// Gets or sets the Gregorian culture applied to the popup <see cref="Calendar"/>'s month and day
    /// names and navigation. Default is <see cref="CultureInfo.CurrentCulture"/>.
    /// </summary>
    /// <remarks>
    /// This affects only the popup calendar. The typed field itself always renders a fixed
    /// month/day/year segment order with invariant digits and separators, regardless of this
    /// value; it does not currently derive its layout from a culture's date pattern the way
    /// <see cref="DateInput.Culture"/> does.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The culture's active calendar is not Gregorian.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public CultureInfo Culture
    {
        get => _culture;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (value.DateTimeFormat.Calendar is not GregorianCalendar)
            {
                throw new ArgumentException(
                    "DateTimeInput requires a Gregorian display culture.", nameof(value));
            }

            if (SetProperty(ref _culture, value, InvalidationImpact.Measure))
            {
                _calendar.Culture = value;
            }
        }
    }

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
                _yearDigitCount = 0;
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
                _yearDigitCount = 0;
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

    /// <summary>Gets or sets the inclusive lower bound for the value. Default is <see cref="DateTime.MinValue"/>.</summary>
    /// <exception cref="ArgumentException">The minimum exceeds <see cref="MaximumValue"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public DateTime MinimumValue
    {
        get;
        set
        {
            VerifyMutable();

            if (value > MaximumValue)
            {
                throw new ArgumentException(
                    "MinimumValue cannot exceed MaximumValue.", nameof(value));
            }

            if (SetProperty(ref field, value, InvalidationImpact.Render))
            {
                SyncCalendarBounds();
                ClampCurrentValue();
            }
        }
    } = DateTime.MinValue;

    /// <summary>Gets or sets the inclusive upper bound for the value. Default is <see cref="DateTime.MaxValue"/>.</summary>
    /// <exception cref="ArgumentException">The maximum is below <see cref="MinimumValue"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public DateTime MaximumValue
    {
        get;
        set
        {
            VerifyMutable();

            if (value < MinimumValue)
            {
                throw new ArgumentException(
                    "MaximumValue cannot be less than MinimumValue.", nameof(value));
            }

            if (SetProperty(ref field, value, InvalidationImpact.Render))
            {
                SyncCalendarBounds();
                ClampCurrentValue();
            }
        }
    } = DateTime.MaxValue;

    /// <summary>Gets or sets whether the private calendar popup owns a dismissing modal plane rooted at this field.</summary>
    /// <exception cref="ArgumentException">The attached control is not an eligible modal root.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    /// <exception cref="Exception">A focus, scope, pointer-cleanup, or user callback fails after committed cleanup.</exception>
    public bool IsOpen
    {
        get => _popup.IsOpen;
        set
        {
            VerifyMutable();

            if (_popup.IsOpen != value)
            {
                if (value)
                {
                    OpenDropDown();
                }
                else
                {
                    CloseDropDown();
                }
            }
        }
    }

    /// <summary>Gets or sets the local one-cell drop-down indicator.</summary>
    /// <exception cref="ArgumentException">The value is a control or does not occupy exactly one cell.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Rune DropDownGlyph
    {
        get => _dropDownGlyph ?? ControlGlyphs.Disclosure.DropDown.Value;
        set
        {
            _ = new ControlGlyph(value, value);
            VerifyMutable();

            if (_dropDownGlyph == value)
            {
                return;
            }

            _dropDownGlyph = value;
            NotifyPropertyChanged(nameof(DropDownGlyph), InvalidationImpact.Render);
        }
    }

    /// <summary>Clears the local drop-down indicator to the code-owned default.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void ResetDropDownGlyph()
    {
        VerifyMutable();

        if (_dropDownGlyph.HasValue)
        {
            _dropDownGlyph = null;
            NotifyPropertyChanged(nameof(DropDownGlyph), InvalidationImpact.Render);
        }
    }

    #endregion

    #region Input, layout, and rendering

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = MeasureChild(_popup, new Constraint(constraint.Width, _calendarPopupHeight));
        var width = FormatWidth() + _fieldBorderWidth;
        return new Size(width, _fieldContentHeight);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) =>
        ArrangeChild(_popup, RootBounds(bounds), ResolvedAxes.Both);

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
        }
        else
        {
            DrawSegments(canvas, content, style, highlight);
        }

        var themed = ControlGlyphs.Disclosure.DropDown;
        var glyph = DropDownGlyph.Resolve(themed.Fallback, CellPolicy.AmbiguousWidth);
        canvas.DrawRune(
            glyph,
            new Point(Math.Max(content.X, content.Right - _indicatorWidth), content.Y),
            style,
            BackgroundMode.Transparent);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        if (!EffectiveIsEnabled || !EffectiveIsVisible)
        {
            base.OnEvent(eventArgs);
            return;
        }

        if (IsOpen && eventArgs is KeyEventArgs { Stroke: { Action: KeyAction.Press } stroke })
        {
            if (stroke.Code == Code.Escape)
            {
                IsOpen = false;
                eventArgs.Handled = true;
                return;
            }

            if (stroke.Code == Code.Tab)
            {
                IsOpen = false;
                return;
            }

            if (stroke.Code is Code.Up or Code.Down or Code.Left or Code.Right
                or Code.PageUp or Code.PageDown or Code.Home or Code.End)
            {
                _calendar.InvokeDefault(eventArgs);
                return;
            }

            if (stroke.Code == Code.Enter ||
                (stroke.Code == Code.Character && stroke.Character == new Rune(' ')))
            {
                _calendar.InvokeDefault(eventArgs);
                return;
            }

            return;
        }

        if (eventArgs is KeyEventArgs key && !IsOpen)
        {
            HandleKey(key);

            if (key.Handled)
            {
                return;
            }
        }

        if (eventArgs is PointerEventArgs pointer && !IsOpen)
        {
            HandlePointer(pointer);

            if (pointer.Handled)
            {
                return;
            }
        }

        if (!eventArgs.Handled)
        {
            _interaction.Handle(eventArgs);
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
        _interaction.FocusChanged(focused);

        if (!focused)
        {
            _digitBuffer = null;
            _yearDigitCount = 0;
        }

        Invalidate(InvalidationImpact.Render);
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason)
    {
        base.OnLostPointerCapture(reason);
        _interaction.CaptureLost();
    }

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();

        if (_popup.IsOpen)
        {
            _modalTracker.Enter(this);
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        _interaction.Unavailable();

        if (reason == ReleaseReason.Disposed)
        {
            _calendar.SelectionChanged -= OnCalendarSelectionChanged;
            _popup.Opened -= OnPopupOpened;
            _popup.Closing -= OnPopupClosing;
            _popup.Closed -= OnPopupClosed;
            ValueChanged = null;
        }
    }

    #endregion

    #region Keyboard input

    private void HandleKey(KeyEventArgs eventArgs)
    {
        var stroke = eventArgs.Stroke;

        if (stroke.Action is not (KeyAction.Press or KeyAction.Repeat))
        {
            return;
        }

        if (stroke.Code == Code.Down && (stroke.Modifiers & Modifiers.Alt) != 0)
        {
            IsOpen = true;
            eventArgs.Handled = true;
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

        if (pointer.Action != PointerAction.Press || (pointer.Buttons & Buttons.Primary) == 0)
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
            _yearDigitCount = 0;
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

        if (target < 0)
        {
            target = LastSegment();
        }
        else if (target > LastSegment())
        {
            target = 0;
        }

        if (_activeSegment == target)
        {
            return false;
        }

        _activeSegment = target;
        _digitBuffer = null;
        _yearDigitCount = 0;
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
        _yearDigitCount = 0;
        Invalidate(InvalidationImpact.Render);
        return true;
    }

    private bool IncrementSegment(int delta)
    {
        if (!_value.HasValue)
        {
            return Commit(ClampToRange(TimeProvider.System.GetLocalNow().DateTime));
        }

        var dt = _value.Value;
        var result = _activeSegment switch
        {
            _segmentMonth => SafeAddMonths(dt, delta),
            _segmentDay => SafeAddDays(dt, delta),
            _segmentYear => SafeAddYears(dt, delta),
            _segmentHour => SafeAddTicks(dt, TimeSpan.TicksPerHour * delta),
            _segmentMinute => SafeAddTicks(dt, TimeStep.Ticks * delta),
            _segmentSecond when ShowSeconds => SafeAddTicks(dt, TimeSpan.TicksPerSecond * delta),
            _ when _activeSegment == AmPmSegmentIndex() && !Use24HourFormat =>
                dt.AddHours(dt.Hour < 12 ? 12 : -12),
            _ => dt
        };

        _digitBuffer = null;
        _yearDigitCount = 0;
        return Commit(result);
    }

    private bool TypeDigit(int digit)
    {
        if (_activeSegment == AmPmSegmentIndex() && !Use24HourFormat)
        {
            return false;
        }

        if (!_value.HasValue)
        {
            _ = Commit(ClampToRange(TimeProvider.System.GetLocalNow().DateTime));

            if (!_value.HasValue)
            {
                return false;
            }
        }

        var dt = _value.Value;

        if (_digitBuffer.HasValue)
        {
            var newValue = (_digitBuffer.Value * 10) + digit;

            // Year needs four digits, not two: keep buffering and stay on the segment
            // until the count reaches four instead of committing after the second.
            if (_activeSegment == _segmentYear && ++_yearDigitCount < 4)
            {
                _digitBuffer = newValue;
                return ApplySegmentValue(dt, _activeSegment, newValue);
            }

            _digitBuffer = null;
            _yearDigitCount = 0;

            var committed = ApplySegmentValue(dt, _activeSegment, newValue);

            if (committed)
            {
                var next = _activeSegment + 1;

                if (next <= LastSegment() && next != AmPmSegmentIndex())
                {
                    _activeSegment = next;
                }
                else if (_activeSegment == _segmentYear && next <= LastSegment())
                {
                    _activeSegment = next;
                }
            }

            return committed;
        }

        var maxFirst = _activeSegment switch
        {
            _segmentMonth => 1,
            _segmentDay => 3,
            _segmentYear => 9,
            _segmentHour when Use24HourFormat => 2,
            _segmentHour => 1,
            _segmentMinute => 5,
            _segmentSecond => 5,
            _ => 0
        };

        if (digit > maxFirst && _activeSegment != _segmentYear)
        {
            return ApplySegmentValue(dt, _activeSegment, digit);
        }

        _digitBuffer = digit;
        _yearDigitCount = _activeSegment == _segmentYear ? 1 : 0;
        return ApplySegmentValue(dt, _activeSegment, digit);
    }

    private bool ApplySegmentValue(DateTime dt, int segment, int value)
    {
        try
        {
            var result = segment switch
            {
                _segmentMonth => ReplaceMonth(dt, Math.Clamp(value, 1, 12)),
                _segmentDay => ReplaceDay(dt, Math.Clamp(value, 1,
                    DateTime.DaysInMonth(dt.Year, dt.Month))),
                _segmentYear => ReplaceYear(dt, Math.Clamp(value, 1, 9999)),
                _segmentHour when !Use24HourFormat =>
                    dt.Date.Add(new TimeSpan(
                        To24Hour(Math.Clamp(value, 1, 12), dt.Hour >= 12),
                        dt.Minute, dt.Second)),
                _segmentHour =>
                    dt.Date.Add(new TimeSpan(
                        Math.Clamp(value, 0, 23), dt.Minute, dt.Second)),
                _segmentMinute =>
                    dt.Date.Add(new TimeSpan(
                        dt.Hour, Math.Clamp(value, 0, 59), dt.Second)),
                _segmentSecond =>
                    dt.Date.Add(new TimeSpan(
                        dt.Hour, dt.Minute, Math.Clamp(value, 0, 59))),
                _ => dt
            };

            return Commit(result);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private bool ToggleAmPm()
    {
        if (Use24HourFormat || !_value.HasValue)
        {
            return false;
        }

        _activeSegment = AmPmSegmentIndex();
        return IncrementSegment(1);
    }

    private bool ClearSegment()
    {
        if (!_value.HasValue)
        {
            return false;
        }

        _digitBuffer = null;
        _yearDigitCount = 0;
        var dt = _value.Value;

        try
        {
            var result = _activeSegment switch
            {
                _segmentMonth => ReplaceMonth(dt, 1),
                _segmentDay => ReplaceDay(dt, 1),
                _segmentYear => ReplaceYear(dt, 1),
                _segmentHour => dt.Date.Add(new TimeSpan(0, dt.Minute, dt.Second)),
                _segmentMinute => dt.Date.Add(new TimeSpan(dt.Hour, 0, dt.Second)),
                _segmentSecond when ShowSeconds => dt.Date.Add(new TimeSpan(dt.Hour, dt.Minute, 0)),
                _ => dt
            };

            return Commit(result);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private bool ClearValue() =>
        AllowNull && _value.HasValue && Commit(null);

    private static bool IsDigit(Rune character) =>
        character.Value is >= '0' and <= '9';

    private static bool IsAmPmToggle(Rune character) =>
        character.Value is 'a' or 'A' or 'p' or 'P';

    #endregion

    #region Commit and validation

    private bool Commit(DateTime? requested)
    {
        var previous = _value;
        var clamped = requested.HasValue
            ? ClampToRange(requested.Value)
            : AllowNull ? null : _value;

        if (!SetProperty(ref _value, clamped, InvalidationImpact.Render, nameof(Value)))
        {
            return false;
        }

        if (clamped.HasValue)
        {
            var date = DateOnly.FromDateTime(clamped.Value);
            _calendar.Selection = new DateInterval(date, date);
        }

        ValueChanged?.Invoke(this, new DateTimeInputValueChangedEventArgs(previous, clamped));
        return true;
    }

    private DateTime ClampToRange(DateTime dateTime) =>
        dateTime < MinimumValue ? MinimumValue
        : dateTime > MaximumValue ? MaximumValue
        : dateTime;

    private void ClampCurrentValue()
    {
        if (_value.HasValue)
        {
            _ = Commit(ClampToRange(_value.Value));
        }
    }

    private void SyncCalendarBounds()
    {
        _calendar.MinimumDate = MinimumValue > DateTime.MinValue
            ? DateOnly.FromDateTime(MinimumValue)
            : DateOnly.MinValue;

        _calendar.MaximumDate = MaximumValue < DateTime.MaxValue
            ? DateOnly.FromDateTime(MaximumValue)
            : DateOnly.MaxValue;
    }

    #endregion

    #region Drop-down coordination

    private void Activate(ActivationCause cause)
    {
        if (cause == ActivationCause.Keyboard)
        {
            return;
        }

        IsOpen = !IsOpen;
    }

    private void OpenDropDown()
    {
        if (_value.HasValue)
        {
            var date = DateOnly.FromDateTime(_value.Value);
            _calendar.DisplayMonth = new DateOnly(date.Year, date.Month, 1);
            _calendar.Selection = new DateInterval(date, date);
        }

        _popup.IsOpen = true;
        _modalTracker.Enter(this);
    }

    private void CloseDropDown()
    {
        _modalTracker.Exit();
        _popup.IsOpen = false;
    }

    private void OnCalendarSelectionChanged(object? sender, CalendarSelectionChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Selection is not { } interval)
        {
            return;
        }

        var selectedDate = interval.Start;
        var timePart = _value?.TimeOfDay ?? TimeSpan.Zero;
        var kind = _value?.Kind ?? DateTimeKind.Unspecified;
        var combined = selectedDate.ToDateTime(TimeOnly.FromTimeSpan(timePart), kind);
        _ = Commit(combined);
        IsOpen = false;
    }

    private void OnPopupOpened(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        NotifyPropertyChanged(nameof(IsOpen), InvalidationImpact.None);
    }

    private void OnPopupClosing(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (ContainsFocused(_calendar))
        {
            _ = RequestFocus();
        }
    }

    private void OnPopupClosed(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        NotifyPropertyChanged(nameof(IsOpen), InvalidationImpact.None);
    }

    #endregion

    #region Rendering

    private void DrawSegments(TerminalCanvas canvas, Rect content, TerminalStyle style, TerminalStyle highlight)
    {
        var dt = _value!.Value;
        var x = content.X;
        var y = content.Y;
        var focused = IsFocused;

        var monthText = dt.Month.ToString("D2", CultureInfo.InvariantCulture);
        var monthStyle = focused && _activeSegment == _segmentMonth ? highlight : style;
        _ = canvas.Draw(monthText.AsSpan(), new Point(x, y), monthStyle, background: BackgroundMode.Transparent);
        x += _dateSegmentWidth;

        _ = canvas.Draw("/".AsSpan(), new Point(x, y), style, background: BackgroundMode.Transparent);
        x += _dateSeparatorWidth;

        var dayText = dt.Day.ToString("D2", CultureInfo.InvariantCulture);
        var dayStyle = focused && _activeSegment == _segmentDay ? highlight : style;
        _ = canvas.Draw(dayText.AsSpan(), new Point(x, y), dayStyle, background: BackgroundMode.Transparent);
        x += _dateSegmentWidth;

        _ = canvas.Draw("/".AsSpan(), new Point(x, y), style, background: BackgroundMode.Transparent);
        x += _dateSeparatorWidth;

        var yearText = dt.Year.ToString("D4", CultureInfo.InvariantCulture);
        var yearStyle = focused && _activeSegment == _segmentYear ? highlight : style;
        _ = canvas.Draw(yearText.AsSpan(), new Point(x, y), yearStyle, background: BackgroundMode.Transparent);
        x += _yearSegmentWidth;

        _ = canvas.Draw(" ".AsSpan(), new Point(x, y), style, background: BackgroundMode.Transparent);
        x += _dateTimeSeparatorWidth;

        var hourText = FormatHour(dt);
        var hourStyle = focused && _activeSegment == _segmentHour ? highlight : style;
        _ = canvas.Draw(hourText.AsSpan(), new Point(x, y), hourStyle, background: BackgroundMode.Transparent);
        x += _timeSegmentWidth;

        _ = canvas.Draw(":".AsSpan(), new Point(x, y), style, background: BackgroundMode.Transparent);
        x += _timeSeparatorWidth;

        var minuteText = dt.Minute.ToString("D2", CultureInfo.InvariantCulture);
        var minuteStyle = focused && _activeSegment == _segmentMinute ? highlight : style;
        _ = canvas.Draw(minuteText.AsSpan(), new Point(x, y), minuteStyle, background: BackgroundMode.Transparent);
        x += _timeSegmentWidth;

        if (ShowSeconds)
        {
            _ = canvas.Draw(":".AsSpan(), new Point(x, y), style, background: BackgroundMode.Transparent);
            x += _timeSeparatorWidth;

            var secondText = dt.Second.ToString("D2", CultureInfo.InvariantCulture);
            var secondStyle = focused && _activeSegment == _segmentSecond ? highlight : style;
            _ = canvas.Draw(secondText.AsSpan(), new Point(x, y), secondStyle, background: BackgroundMode.Transparent);
            x += _timeSegmentWidth;
        }

        if (!Use24HourFormat)
        {
            _ = canvas.Draw(" ".AsSpan(), new Point(x, y), style, background: BackgroundMode.Transparent);
            x += _dateTimeSeparatorWidth;

            var amPmText = dt.Hour < 12 ? "AM" : "PM";
            var amPmStyle = focused && _activeSegment == AmPmSegmentIndex() ? highlight : style;
            _ = canvas.Draw(amPmText.AsSpan(), new Point(x, y), amPmStyle, background: BackgroundMode.Transparent);
        }
    }

    private void DrawPlaceholder(TerminalCanvas canvas, Rect content, TerminalStyle style)
    {
        var datePart = "--/--/----";
        var timePart = Use24HourFormat
            ? ShowSeconds ? "--:--:--" : "--:--"
            : ShowSeconds ? "--:--:-- --" : "--:-- --";
        var placeholder = $"{datePart} {timePart}";

        var focused = IsFocused;
        var highlight = new TerminalStyle(
            style.Foreground,
            style.Background,
            style.Attributes | TerminalAttributes.Reverse);

        if (!focused)
        {
            _ = canvas.Draw(
                placeholder.AsSpan(),
                new Point(content.X, content.Y),
                style,
                background: BackgroundMode.Transparent);
            return;
        }

        var x = content.X;
        var y = content.Y;

        for (var i = 0; i < placeholder.Length && x < content.Right - _singleCellWidth; i++)
        {
            var segmentIndex = SegmentAtColumn(i);
            var charStyle = segmentIndex == _activeSegment ? highlight : style;
            _ = canvas.Draw(
                placeholder.AsSpan(i, _singleCellWidth),
                new Point(x, y),
                charStyle,
                background: BackgroundMode.Transparent);
            x += _singleCellWidth;
        }
    }

    private string FormatHour(DateTime dt)
    {
        if (Use24HourFormat)
        {
            return dt.Hour.ToString("D2", CultureInfo.InvariantCulture);
        }

        var hour12 = dt.Hour % 12;

        if (hour12 == 0)
        {
            hour12 = 12;
        }

        return hour12.ToString("D2", CultureInfo.InvariantCulture);
    }

    #endregion

    #region Segment helpers

    private int LastSegment() =>
        !Use24HourFormat ? AmPmSegmentIndex() : ShowSeconds ? _segmentSecond : _segmentMinute;

    private int AmPmSegmentIndex() =>
        ShowSeconds ? _segmentAmPm : _segmentSecond;

    private int SegmentAtColumn(int column) =>
        column < _dateSegmentWidth ? _segmentMonth
        : column < _dateSegmentWidth + _dateSeparatorWidth + _dateSegmentWidth ? _segmentDay
        : column < (_dateSegmentWidth * 2) + (_dateSeparatorWidth * 2) + _yearSegmentWidth ? _segmentYear
        : column - (_dateSegmentWidth * 2) - (_dateSeparatorWidth * 2) - _yearSegmentWidth - _dateTimeSeparatorWidth < _timeSegmentWidth ? _segmentHour
        : column - (_dateSegmentWidth * 2) - (_dateSeparatorWidth * 2) - _yearSegmentWidth - _dateTimeSeparatorWidth < (_timeSegmentWidth * 2) + _timeSeparatorWidth ? _segmentMinute
        : ShowSeconds && column - (_dateSegmentWidth * 2) - (_dateSeparatorWidth * 2) - _yearSegmentWidth - _dateTimeSeparatorWidth < (_timeSegmentWidth * 3) + (_timeSeparatorWidth * 2) ? _segmentSecond
        : !Use24HourFormat ? AmPmSegmentIndex()
        : ShowSeconds ? _segmentSecond : _segmentMinute;

    private int FormatWidth()
    {
        var width = (_dateSegmentWidth * 2) + (_dateSeparatorWidth * 2) + _yearSegmentWidth
            + _dateTimeSeparatorWidth + (_timeSegmentWidth * 2) + _timeSeparatorWidth;

        if (ShowSeconds)
        {
            width += _timeSeparatorWidth + _timeSegmentWidth;
        }

        if (!Use24HourFormat)
        {
            width += _dateTimeSeparatorWidth + _timeSegmentWidth;
        }

        return width;
    }

    #endregion

    #region Date arithmetic helpers

    private static DateTime ReplaceMonth(DateTime dt, int month)
    {
        var day = Math.Min(dt.Day, DateTime.DaysInMonth(dt.Year, month));
        return new DateTime(dt.Year, month, day, dt.Hour, dt.Minute, dt.Second, dt.Kind);
    }

    private static DateTime ReplaceDay(DateTime dt, int day)
    {
        var clampedDay = Math.Min(day, DateTime.DaysInMonth(dt.Year, dt.Month));
        return new DateTime(dt.Year, dt.Month, clampedDay, dt.Hour, dt.Minute, dt.Second, dt.Kind);
    }

    private static DateTime ReplaceYear(DateTime dt, int year)
    {
        var clampedYear = Math.Clamp(year, 1, 9999);
        var day = Math.Min(dt.Day, DateTime.DaysInMonth(clampedYear, dt.Month));
        return new DateTime(clampedYear, dt.Month, day, dt.Hour, dt.Minute, dt.Second, dt.Kind);
    }

    private static DateTime SafeAddMonths(DateTime dt, int delta)
    {
        try
        {
            return dt.AddMonths(delta);
        }
        catch (ArgumentOutOfRangeException)
        {
            return dt;
        }
    }

    private static DateTime SafeAddDays(DateTime dt, int delta)
    {
        try
        {
            return dt.AddDays(delta);
        }
        catch (ArgumentOutOfRangeException)
        {
            return dt;
        }
    }

    private static DateTime SafeAddYears(DateTime dt, int delta)
    {
        var year = dt.Year + delta;
        return year is < 1 or > 9999 ? dt : ReplaceYear(dt, year);
    }

    private static DateTime SafeAddTicks(DateTime dateTime, long ticks)
    {
        try
        {
            return dateTime.AddTicks(ticks);
        }
        catch (ArgumentOutOfRangeException)
        {
            return dateTime;
        }
    }

    private static int To24Hour(int hour12, bool isPm) =>
        hour12 == 12 ? isPm ? 12 : 0 : isPm ? hour12 + 12 : hour12;

    #endregion
}
