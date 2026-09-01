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
public sealed class DateTimeInput: InputBase
{
    // Terminal field geometry: one content row and two border columns. The indicator cell is
    // InputBase.DropDownIndicatorWidth.
    private const int _fieldContentHeight = 1;
    private const int _fieldBorderWidth = 2;

    private static readonly IReadOnlyDictionary<char, TemporalSegmentKind> _tokenKinds =
        new Dictionary<char, TemporalSegmentKind>
        {
            ['M'] = TemporalSegmentKind.Month,
            ['d'] = TemporalSegmentKind.Day,
            ['y'] = TemporalSegmentKind.Year,
            ['H'] = TemporalSegmentKind.Hour,
            ['h'] = TemporalSegmentKind.Hour,
            ['m'] = TemporalSegmentKind.Minute,
            ['s'] = TemporalSegmentKind.Second,
            ['t'] = TemporalSegmentKind.AmPmDesignator
        };

    private readonly CalendarDropDownCoordinator<DateTime> _calendarDropDown;
    private readonly Popup _popup;
    private readonly SegmentFieldBehavior _segments;
    private readonly SegmentFieldKeyOptions _segmentKeyOptions;
    private readonly TemporalValueState<DateTime> _state;

    private CultureInfo _culture;

    #region Construction and properties

    /// <summary>Initializes a focusable date-time input at the current local date and time with a light field border and a connected calendar popup.</summary>
    public DateTimeInput()
    {
        // Value resolves the current local date and time lazily, on first read, rather than
        // here: a control constructed off-dispatcher and then mounted under a dispatcher with its
        // own TimeProvider must observe that dispatcher's clock instead of latching the clock
        // that happened to be current at construction. The owned Calendar starts with no
        // selection to match; EnsureSeeded pushes the resolved value into it once seeding
        // actually happens.
        _culture = CultureInfo.InvariantCulture;
        _state = new TemporalValueState<DateTime>(
            DateTime.MinValue,
            DateTime.MaxValue,
            this,
            VerifyMutable,
            NotifyPropertyChanged,
            () => TimeProvider.GetLocalNow().DateTime,
            PublishValueChanged,
            SynchronizeCalendarValue,
            SyncCalendarBounds,
            resolveValueImpact: ResolveValueWidthImpact);
        _calendarDropDown = new CalendarDropDownCoordinator<DateTime>(
            _culture,
            EnsureSeeded,
            () => _state.Value,
            value => _ = _state.SetValue(value),
            static value => DateOnly.FromDateTime(value),
            CombineCalendarDate,
            ResolveCalendarMinimum,
            ResolveCalendarMaximum,
            () => _state.ValueVersion,
            () => _state.BoundsVersion,
            () => IsOpen,
            () => IsOpen = false,
            AcceptPopupAndClose);

        _popup = EnablePopupNavigationSession(
            _calendarDropDown.Calendar,
            focusOnOpen: true,
            beforeOpen: _calendarDropDown.BeforeOpen,
            beginSession: _calendarDropDown.BeginSession,
            handleNavigationKey: _calendarDropDown.HandleNavigationKey,
            cancelSession: _calendarDropDown.CancelSession,
            acceptSession: _calendarDropDown.AcceptSession);
        _popup.ContentHeightLimit = Length.Cells(10);

        // ActualCalendarStyle is a live projection of the owned Calendar's own ActualStyle rather
        // than a style slot DateTimeInput owns directly, so nothing raises PropertyChanged for it
        // on either a local CalendarStyle assignment or a theme swap unless something forwards
        // Calendar's own notification through. This bridge does exactly that.
        _ = RegisterRetainedPartProperty<CalendarStyle>(
            _calendarDropDown.Calendar,
            nameof(Calendar.ActualStyle),
            nameof(ActualCalendarStyle),
            () => _calendarDropDown.Calendar.ActualStyle);
        EnablePressActivation();

        _segments = EnableSegmentEditing(
            BuildSegments,
            ApplyDigitValue,
            IncrementSegmentValue,
            ClearSegmentValue);
        _segmentKeyOptions = new SegmentFieldKeyOptions(
            ResolveStepDelta,
            ClearValue,
            HandleCharacterCommand,
            HandlePopupCommand);
        TabNavigation = TabNavigation.None;
    }

    /// <summary>Raised after a committed value transition.</summary>
    public event EventHandler<DateTimeInputValueChangedEventArgs>? ValueChanged;

    private void PublishValueChanged(
        ref CallbackTransitionTransaction transition,
        DateTime? previous,
        DateTime? current) =>
        transition.PublishCurrent(
            ValueChanged,
            this,
            new DateTimeInputValueChangedEventArgs(previous, current));

    /// <summary>Raised after the Calendar popup opens.</summary>
    public event EventHandler? DropDownOpened;

    /// <summary>Raised after the Calendar popup closes.</summary>
    public event EventHandler? DropDownClosed;

    /// <summary>Gets or sets the current date-time value, or null when cleared.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public DateTime? Value
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return _state.EnsureSeeded();
        }
        set => _ = _state.SetValue(value);
    }

    /// <summary>Gets or sets whether the value may be cleared to null. Default is true.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool AllowNull
    {
        get => _state.AllowNull;
        set => _ = _state.SetAllowNull(value);
    }

    /// <summary>
    /// Gets or sets the Gregorian culture applied to both the popup <see cref="Calendar"/>'s month
    /// and day names and the typed field's own segment order, separators, and AM/PM designator
    /// text. Default is <see cref="CultureInfo.InvariantCulture"/>, so out-of-the-box rendering
    /// never depends on the host operating system's locale; set this explicitly to localize the
    /// field.
    /// </summary>
    /// <remarks>
    /// The date portion of the typed field derives its segment order, widths, and separators from
    /// <see cref="DateTimeFormatInfo.ShortDatePattern"/> the same way <see cref="DateInput.Culture"/>
    /// does - for example a German culture renders day before month with a period separator. The
    /// time portion keeps the fixed hour/minute/[second]/[AM-PM] structure <see cref="Use24HourFormat"/>
    /// and <see cref="ShowSeconds"/> already select, localizing only its separator, AM/PM designator
    /// text, and digit glyphs. Set <see cref="Format"/> to override the combined pattern entirely.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The culture's active calendar is not Gregorian, or a non-null <see cref="Format"/> cannot be rendered by a <see cref="DateTime"/> under this culture.</exception>
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

            if (Format is { } format)
            {
                TemporalFormatValidation.Validate(
                    format, value, nameof(value), "DateTime", static (f, c) => DateTime.MinValue.ToString(f, c));
            }

            _ = SetPropertyAndSynchronize(
                ref _culture,
                value,
                InvalidationImpact.Measure,
                () =>
                {
                    _calendarDropDown.SyncCulture(Culture);
                    _segments.ClampActiveSegment();
                    _segments.ResetDigitBuffer();
                },
                ReferenceEqualityComparer.Instance);
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
                _segments.ClampActiveSegment();
                _segments.ResetDigitBuffer();
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
                _segments.ClampActiveSegment();
                _segments.ResetDigitBuffer();
            }
        }
    }

    /// <summary>Gets or sets a custom combined date-time format pattern, or null to derive the
    /// pattern from <see cref="Culture"/>'s <see cref="DateTimeFormatInfo.ShortDatePattern"/> plus
    /// <see cref="Use24HourFormat"/> and <see cref="ShowSeconds"/>. Default is null.</summary>
    /// <remarks>
    /// When set, the pattern's own token runs - not <see cref="Culture"/>'s date pattern or
    /// <see cref="Use24HourFormat"/>/<see cref="ShowSeconds"/> - determine the segment order and
    /// count; pair a 12-hour <c>h</c>/<c>hh</c> hour token with a <c>t</c>/<c>tt</c> AM/PM
    /// designator token for correct 12-hour clamping and rendering, since a lowercase hour token
    /// without a designator token is treated as a 24-hour segment for both editing and display.
    /// </remarks>
    /// <exception cref="ArgumentException">The value is empty, or cannot be rendered by a <see cref="DateTime"/> under <see cref="Culture"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string? Format
    {
        get;
        set
        {
            if (value is not null)
            {
                ArgumentException.ThrowIfNullOrEmpty(value);
                TemporalFormatValidation.Validate(
                    value, _culture, nameof(value), "DateTime", static (f, c) => DateTime.MinValue.ToString(f, c));
            }

            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                _segments.ClampActiveSegment();
                _segments.ResetDigitBuffer();
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
            ArgumentOutOfRangeException.ThrowIfNotAPositiveWholeMinuteStep(value, nameof(value));

            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets the inclusive lower bound for the value. Default is <see cref="DateTime.MinValue"/>.</summary>
    /// <exception cref="ArgumentException">The minimum exceeds <see cref="Maximum"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public DateTime Minimum
    {
        get => _state.Minimum;
        set => _ = _state.SetMinimum(value);
    }

    /// <summary>Gets or sets the inclusive upper bound for the value. Default is <see cref="DateTime.MaxValue"/>.</summary>
    /// <exception cref="ArgumentException">The maximum is below <see cref="Minimum"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public DateTime Maximum
    {
        get => _state.Maximum;
        set => _ = _state.SetMaximum(value);
    }

    /// <summary>Gets or sets the intrinsic, fixed, or placement-side-relative maximum visible calendar height.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A fixed or percentage value is zero.</exception>
    /// <exception cref="ArgumentException">The value uses proportional sizing.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Length DropDownHeight
    {
        get => _popup.ContentHeightLimit;
        set
        {
            VerifyMutable();

            if (_popup.ContentHeightLimit == value)
            {
                return;
            }

            _popup.ContentHeightLimit = value;
            NotifyPropertyChanged(nameof(DropDownHeight), InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets or sets the owned Calendar popup's border and shadow together.</summary>
    /// <remarks>
    /// A component left null keeps the popup on its own <see cref="PopupChrome"/> role
    /// appearance for that part.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public PopupChrome PopupChrome
    {
        get => _popup.Style;
        set
        {
            VerifyMutable();

            if (_popup.Style == value)
            {
                return;
            }

            _popup.Style = value;
            NotifyPropertyChanged(nameof(PopupChrome), InvalidationImpact.None);
        }
    }

    /// <summary>Returns the Calendar popup's border and shadow to <see cref="PopupChrome"/> ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void ResetPopupChrome() => PopupChrome = default;

    /// <summary>Gets or sets the complete local style of the owned Calendar, or null to use its own
    /// role-normal presentation.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public CalendarStyle? CalendarStyle
    {
        get => _calendarDropDown.Calendar.Style;
        set
        {
            VerifyMutable();

            if (_calendarDropDown.Calendar.Style == value)
            {
                return;
            }

            _calendarDropDown.Calendar.Style = value;
            NotifyPropertyChanged(nameof(CalendarStyle), InvalidationImpact.None);
        }
    }

    /// <summary>Gets the resolved presentation of the owned Calendar.</summary>
    public CalendarStyle ActualCalendarStyle => _calendarDropDown.Calendar.ActualStyle;

    /// <summary>Gets the retained calendar for proving bound synchronization invariants.</summary>
    internal Calendar OwnedCalendar => _calendarDropDown.Calendar;

    /// <summary>Gets or sets the optional leading edge-pinned decoration, reserved inside the
    /// field box and strictly inboard of the drop-down indicator.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Affix? StartAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    /// <summary>Gets or sets the optional trailing edge-pinned decoration, reserved inside the
    /// field box and strictly inboard of the drop-down indicator.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Affix? EndAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    #endregion

    #region Input, layout, and rendering

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        EnsureSeeded();
        _ = MeasureChild(_popup, new Constraint(constraint.Width, height: null));
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());
        var width = _fieldBorderWidth + affixes.StartCells + affixes.EndCells;

        foreach (var segment in BuildSegments())
        {
            width += MeasureCells(segment.Text);
        }

        return new Size(width, _fieldContentHeight);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) =>
        ArrangeChild(_popup, RootBounds(bounds), ResolvedAxes.Both);

    /// <summary>Resolves the box editable segment text is drawn into: the content box with the
    /// drop-down indicator's own reserved columns (<see cref="_fieldBorderWidth"/>) subtracted
    /// first, then deflated for any active <see cref="StartAffix"/>/<see cref="EndAffix"/> -
    /// keeping both affixes strictly inboard of the indicator, and never overlapping it.</summary>
    private Rect ResolveTextBox()
    {
        var content = ContentBounds;
        var fieldBox = new Rect(
            content.X,
            content.Y,
            Math.Max(0, content.Width - _fieldBorderWidth),
            _fieldContentHeight);
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());
        return DeflateForAffixes(fieldBox, affixes);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var content = ContentBounds;

        if (content.Width == 0 || content.Height == 0)
        {
            return;
        }

        EnsureSeeded();
        var style = ResolvedStyle;
        var highlight = new TerminalStyle(
            style.Foreground,
            style.Background,
            style.Attributes | TerminalAttributes.Reverse);
        var canHighlight = IsFocused && !IsOpen;
        var fieldBox = new Rect(
            content.X,
            content.Y,
            Math.Max(0, content.Width - _fieldBorderWidth),
            _fieldContentHeight);
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());
        RenderAffixes(canvas, fieldBox, affixes, StartAffix, EndAffix, style);
        var textBox = DeflateForAffixes(fieldBox, affixes);
        var textCanvas = canvas.Clip(textBox);
        var x = textBox.X;
        var editableIndex = -1;

        foreach (var segment in BuildSegments())
        {
            if (segment.IsEditable)
            {
                editableIndex++;
            }

            var segmentStyle = canHighlight && segment.IsEditable && editableIndex == _segments.ActiveSegment
                ? highlight
                : style;
            _ = textCanvas.Draw(segment.Text.AsSpan(), new Point(x, textBox.Y), segmentStyle, background: BackgroundMode.Transparent);
            x += MeasureCells(segment.Text);
        }

        DrawDropDownIndicator(canvas, content, style);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        EnsureSeeded();

        if (!EffectiveIsEnabled || !EffectiveIsVisible)
        {
            base.OnEvent(eventArgs);
            return;
        }

        if (IsOpen)
        {
            return;
        }

        if (eventArgs is KeyEventArgs key && !IsOpen)
        {
            _segments.HandleKey(key, _segmentKeyOptions);

            if (key.IsHandled)
            {
                return;
            }
        }

        if (eventArgs is PointerEventArgs pointer && !IsOpen)
        {
            HandlePointer(pointer);

            if (pointer.IsHandled)
            {
                return;
            }
        }

        if (!eventArgs.IsHandled)
        {
            HandlePressActivation(eventArgs);
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

        if (!focused)
        {
            _segments.ResetDigitBuffer();
        }

        Invalidate(InvalidationImpact.Render);
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            _calendarDropDown.Dispose();
            ValueChanged = null;
            DropDownOpened = null;
            DropDownClosed = null;
        }
    }

    #endregion

    #region Keyboard input

    private void HandlePointer(PointerEventArgs eventArgs)
    {
        var dispatcher = Dispatcher;
        _segments.HandlePointer(
            eventArgs,
            ResolveTextBox(),
            CellPolicy.AmbiguousWidth,
            IsFocused,
            RequestFocus,
            () => CanContinueAfterFocus(dispatcher));
    }

    private int? ResolveStepDelta(KeyEventArgs eventArgs) =>
        TryGetStepDelta(eventArgs, out var delta) ? delta : null;

    private bool HandleCharacterCommand(Rune character) =>
        TemporalSegmentClassification.IsAmPmToggle(character) && ToggleAmPm();

    private bool? HandlePopupCommand(KeyEventArgs eventArgs)
    {
        var stroke = eventArgs.Stroke;

        if (!eventArgs.IsInitialKeyDown ||
            stroke.Code != Code.Down ||
            (stroke.Modifiers & Modifiers.Alt) == 0)
        {
            return null;
        }

        if (!KeyboardModifierPolicy.MatchesCommand(stroke.Modifiers, Modifiers.Alt))
        {
            return false;
        }

        IsOpen = true;
        return true;
    }

    private bool ToggleAmPm() =>
        TemporalSegmentClassification.ToggleAmPm(BuildSegments, () => _state.Value.HasValue, _segments);

    /// <summary>Gets whether the current layout - whether derived from <see cref="Use24HourFormat"/>
    /// or overridden by <see cref="Format"/> - includes an AM/PM designator segment, used as the
    /// effective 12-versus-24-hour policy for editing the hour segment.</summary>
    private bool HasAmPmDesignator => TemporalSegmentClassification.HasAmPmDesignator(BuildSegments);

    private bool ApplyDigitValue(TemporalSegmentKind kind, int value)
    {
        if (!_state.Value.HasValue)
        {
            _ = _state.SetValue(_state.Clamp(TimeProvider.GetLocalNow().DateTime));

            if (!_state.Value.HasValue)
            {
                return false;
            }
        }

        var dt = _state.Value.Value;
        var hasAmPm = HasAmPmDesignator;

        try
        {
#pragma warning disable IDE0072 // AmPmDesignator never reaches this callback: its digit capacity is zero.
            var result = kind switch
            {
                TemporalSegmentKind.Month => ReplaceMonth(dt, Math.Clamp(value, 1, 12)),
                TemporalSegmentKind.Day => ReplaceDay(dt, Math.Clamp(value, 1,
                    DateTime.DaysInMonth(dt.Year, dt.Month))),
                TemporalSegmentKind.Year => ReplaceYear(dt, Math.Clamp(value, 1, 9999)),
                TemporalSegmentKind.Hour when hasAmPm =>
                    WithSubSecondTicksOf(dt.Date.Add(new TimeSpan(
                        TemporalSegmentClassification.To24Hour(
                            TemporalClockArithmetic.ClampHour(value, hasAmPmDesignator: true),
                            dt.Hour >= 12),
                        dt.Minute, dt.Second)), dt),
                TemporalSegmentKind.Hour =>
                    WithSubSecondTicksOf(dt.Date.Add(new TimeSpan(
                        TemporalClockArithmetic.ClampHour(value, hasAmPmDesignator: false), dt.Minute, dt.Second)), dt),
                TemporalSegmentKind.Minute =>
                    WithSubSecondTicksOf(dt.Date.Add(new TimeSpan(
                        dt.Hour, TemporalClockArithmetic.ClampMinuteOrSecond(value), dt.Second)), dt),
                TemporalSegmentKind.Second =>
                    WithSubSecondTicksOf(dt.Date.Add(new TimeSpan(
                        dt.Hour, dt.Minute, TemporalClockArithmetic.ClampMinuteOrSecond(value))), dt),
                _ => dt
            };
#pragma warning restore IDE0072

            return _state.SetValue(result);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private bool IncrementSegmentValue(TemporalSegmentKind kind, int delta)
    {
        if (!_state.Value.HasValue)
        {
            return _state.SetValue(_state.Clamp(TimeProvider.GetLocalNow().DateTime));
        }

        var dt = _state.Value.Value;

        // Every case below is only reached for a kind the current layout actually contains
        // (the engine dispatches by the active segment's own kind), so no additional
        // Use24HourFormat/ShowSeconds guard is needed here.
#pragma warning disable IDE0072 // Every calendar/clock kind is individually handled or intentionally falls through.
        var result = kind switch
        {
            TemporalSegmentKind.Month => SafeAddMonths(dt, delta),
            TemporalSegmentKind.Day => SafeAddDays(dt, delta),
            TemporalSegmentKind.Year => SafeAddYears(dt, delta),
            TemporalSegmentKind.Hour => SafeAddTicks(dt, TimeSpan.TicksPerHour * delta),
            TemporalSegmentKind.Minute => SafeAddTicks(dt, TimeStep.Ticks * delta),
            TemporalSegmentKind.Second => SafeAddTicks(dt, TimeSpan.TicksPerSecond * delta),
            TemporalSegmentKind.AmPmDesignator => dt.AddHours(dt.Hour < 12 ? 12 : -12),
            _ => dt
        };
#pragma warning restore IDE0072

        return _state.SetValue(result);
    }

    private bool ClearSegmentValue(TemporalSegmentKind kind)
    {
        if (!_state.Value.HasValue)
        {
            return false;
        }

        var dt = _state.Value.Value;

        try
        {
#pragma warning disable IDE0072 // AmPmDesignator is intentionally a no-op here.
            var result = kind switch
            {
                TemporalSegmentKind.Month => ReplaceMonth(dt, 1),
                TemporalSegmentKind.Day => ReplaceDay(dt, 1),
                TemporalSegmentKind.Year => ReplaceYear(dt, 1),
                TemporalSegmentKind.Hour => WithSubSecondTicksOf(dt.Date.Add(new TimeSpan(0, dt.Minute, dt.Second)), dt),
                TemporalSegmentKind.Minute => WithSubSecondTicksOf(dt.Date.Add(new TimeSpan(dt.Hour, 0, dt.Second)), dt),
                TemporalSegmentKind.Second => WithSubSecondTicksOf(dt.Date.Add(new TimeSpan(dt.Hour, dt.Minute, 0)), dt),
                _ => dt
            };
#pragma warning restore IDE0072

            return _state.SetValue(result);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private bool ClearValue() =>
        AllowNull && _state.Value.HasValue && _state.SetValue(null);

    #endregion

    #region Commit and validation

    /// <summary>Latches Value to the current local date and time on first read, so a control
    /// mounted under a dispatcher observes that dispatcher's clock instead of the clock current
    /// at construction, and pushes the newly resolved value into the owned Calendar. A value
    /// already committed - including an explicit null under <see cref="AllowNull"/> - is left
    /// untouched.</summary>
    private void EnsureSeeded() => _ = _state.EnsureSeeded();

    private void SyncCalendarBounds() => _calendarDropDown.SyncBounds();

    private void SynchronizeCalendarValue(DateTime? value) =>
        _calendarDropDown.SyncValue(value);

    [Pure]
    private DateOnly ResolveCalendarMinimum() => Minimum > DateTime.MinValue
        ? DateOnly.FromDateTime(Minimum)
        : DateOnly.MinValue;

    [Pure]
    private DateOnly ResolveCalendarMaximum() => Maximum < DateTime.MaxValue
        ? DateOnly.FromDateTime(Maximum)
        : DateOnly.MaxValue;

    [Pure]
    private static DateTime CombineCalendarDate(DateOnly date, DateTime? current)
    {
        var timePart = current?.TimeOfDay ?? TimeSpan.Zero;
        var kind = current?.Kind ?? DateTimeKind.Unspecified;
        return date.ToDateTime(TimeOnly.FromTimeSpan(timePart), kind);
    }

    #endregion

    #region Drop-down coordination

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        if (cause == ActivationCause.Keyboard)
        {
            return;
        }

        IsOpen = !IsOpen;
    }

    /// <inheritdoc/>
    protected override void OnDropDownOpened() => DropDownOpened?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc/>
    protected override void OnDropDownClosed() => DropDownClosed?.Invoke(this, EventArgs.Empty);

    #endregion

    #region Rendering

    private string ResolveDateTimePattern() => Format ?? BuildDefaultDateTimePattern();

    private string BuildDefaultDateTimePattern()
    {
        var datePattern = _culture.DateTimeFormat.ShortDatePattern;
        var timePattern = new StringBuilder(Use24HourFormat ? "HH" : "hh").Append(':').Append("mm");

        if (ShowSeconds)
        {
            _ = timePattern.Append(':').Append("ss");
        }

        if (!Use24HourFormat)
        {
            _ = timePattern.Append(' ').Append("tt");
        }

        return $"{datePattern} {timePattern}";
    }

    private SegmentDescriptor[] BuildSegments() => BuildSegments(_state.Value);

    private SegmentDescriptor[] BuildSegments(DateTime? value)
    {
        var pattern = ResolveDateTimePattern();
        var tokens = TemporalPatternSegmenter.ParseTokens(pattern, _tokenKinds, _culture);
        var hasAmPm = false;

        foreach (var token in tokens)
        {
            if (token.Kind == TemporalSegmentKind.AmPmDesignator)
            {
                hasAmPm = true;
                break;
            }
        }

        IReadOnlyList<string> text;

        if (value is { } dt)
        {
            var renderingPattern = hasAmPm ? pattern : NormalizeDesignatorlessHourPattern(pattern);
            text = TemporalPatternSegmenter.FormatSegments(
                renderingPattern,
                tokens,
                _tokenKinds,
                format => dt.ToString(format, _culture));
        }
        else
        {
            var placeholder = new string[tokens.Count];

            for (var index = 0; index < tokens.Count; index++)
            {
                var token = tokens[index];
#pragma warning disable IDE0072 // Every non-literal kind is intentionally a two-character placeholder except Year.
                placeholder[index] = token.Kind switch
                {
                    null => token.LiteralText,
                    TemporalSegmentKind.Year when token.RunLength >= 4 => "----",
                    _ => "--"
                };
#pragma warning restore IDE0072
            }

            text = placeholder;
        }

        var descriptors = new SegmentDescriptor[tokens.Count];

        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];

            // A weekday (dddd) or month-name (MMMM) run of length >= 3 is a name, not a
            // zero-padded number: rendering it as an ordinary editable segment would let a typed
            // digit be misinterpreted as a day-of-month or month-number and corrupt the date.
            // Building it as a literal instead makes it inert for digit entry, tab/arrow
            // traversal, and Increment alike, since SegmentFieldBehavior gates all three purely
            // on SegmentDescriptor.IsEditable.
            descriptors[index] = token.Kind is not { } kind ||
                (kind is TemporalSegmentKind.Month or TemporalSegmentKind.Day && token.RunLength >= 3)
                ? new SegmentDescriptor(text[index])
                : new SegmentDescriptor(
                    text[index],
                    kind,
                    kind == TemporalSegmentKind.AmPmDesignator ? 0
                        : kind == TemporalSegmentKind.Year && token.RunLength >= 4 ? 4
                        : 2,
                    MaxValueFor(kind, hasAmPm));
        }

        return descriptors;
    }

    /// <summary>Rewrites unquoted lowercase hour tokens to their 24-hour equivalents when the
    /// pattern has no AM/PM designator, keeping rendering consistent with segment editing.</summary>
    [Pure]
    private static string NormalizeDesignatorlessHourPattern(string pattern)
    {
        var normalized = new StringBuilder(pattern.Length);
        var quote = '\0';

        for (var index = 0; index < pattern.Length; index++)
        {
            var character = pattern[index];

            if (character == '\\' && index + 1 < pattern.Length)
            {
                _ = normalized.Append(character).Append(pattern[++index]);
                continue;
            }

            if (character is '\'' or '"')
            {
                quote = quote == '\0' ? character : quote == character ? '\0' : quote;
                _ = normalized.Append(character);
                continue;
            }

            _ = normalized.Append(quote == '\0' && character == 'h' ? 'H' : character);
        }

        return normalized.ToString();
    }

    /// <summary>Sums the resolved cell width of every rendered segment for a candidate value,
    /// without committing it, so a value transition can be graded before it is applied.</summary>
    private int MeasureFormattedWidth(DateTime? value)
    {
        var width = 0;

        foreach (var segment in BuildSegments(value))
        {
            width += MeasureCells(segment.Text);
        }

        return width;
    }

    /// <summary>Grades a value transition by its resolved display-width delta, mirroring
    /// <see cref="ControlBase.GetAffixChangeImpact"/> for affixes: a same-width transition (for
    /// example incrementing a zero-padded minute segment) needs only
    /// <see cref="InvalidationImpact.Render"/>, while a transition that widens or narrows the
    /// formatted text (a single-digit month or day widening to two digits under a non-padded
    /// <see cref="Culture"/> pattern) needs <see cref="InvalidationImpact.Measure"/> so the field
    /// box is remeasured instead of leaving stale geometry behind.</summary>
    private InvalidationImpact ResolveValueWidthImpact(DateTime? previous, DateTime? candidate) =>
        MeasureFormattedWidth(previous) == MeasureFormattedWidth(candidate)
            ? InvalidationImpact.Render
            : InvalidationImpact.Measure;

#pragma warning disable IDE0072 // Every segment kind is individually handled.
    [Pure]
    private static int MaxValueFor(TemporalSegmentKind kind, bool hasAmPm) =>
        kind switch
        {
            TemporalSegmentKind.Month => 12,
            TemporalSegmentKind.Day => 31,
            TemporalSegmentKind.Year => 9999,
            TemporalSegmentKind.Hour => hasAmPm ? 12 : 23,
            TemporalSegmentKind.Minute or TemporalSegmentKind.Second => 59,
            _ => 0
        };
#pragma warning restore IDE0072

    #endregion

    #region Date arithmetic helpers

    [Pure]
    private static DateTime ReplaceMonth(DateTime dt, int month)
    {
        var (year, resolvedMonth, day) = TemporalCalendarArithmetic.ReplaceMonth(dt.Year, dt.Day, month);
        return WithSubSecondTicksOf(new DateTime(year, resolvedMonth, day, dt.Hour, dt.Minute, dt.Second, dt.Kind), dt);
    }

    // Every call site already pre-clamps day to DateTime.DaysInMonth(dt.Year, dt.Month) - the
    // same year and month this clamp itself recomputes - so the clamp below never actually
    // fires. Kept as a thin adapter, rather than inlined at each call site, purely for symmetry
    // with ReplaceMonth/ReplaceYear above.
    [Pure]
    private static DateTime ReplaceDay(DateTime dt, int day)
    {
        var (year, month, clampedDay) = TemporalCalendarArithmetic.ClampDayOfMonth(dt.Year, dt.Month, day);
        return WithSubSecondTicksOf(new DateTime(year, month, clampedDay, dt.Hour, dt.Minute, dt.Second, dt.Kind), dt);
    }

    [Pure]
    private static DateTime ReplaceYear(DateTime dt, int year)
    {
        var (clampedYear, month, day) = TemporalCalendarArithmetic.ReplaceYear(dt.Month, dt.Day, year);
        return WithSubSecondTicksOf(new DateTime(clampedYear, month, day, dt.Hour, dt.Minute, dt.Second, dt.Kind), dt);
    }

    /// <summary>Rebuilds <paramref name="result"/> with the sub-second (tick-resolution) remainder
    /// carried over from <paramref name="original"/>, since <paramref name="result"/> is always
    /// reconstructed from whole hour/minute/second (or whole-day, for the calendar-segment
    /// replacements) components and would otherwise silently drop any fractional-second precision
    /// <paramref name="original"/> already had.</summary>
    [Pure]
    private static DateTime WithSubSecondTicksOf(DateTime result, DateTime original) =>
        result.AddTicks(original.Ticks % TimeSpan.TicksPerSecond);

    [Pure]
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

    [Pure]
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

    [Pure]
    private static DateTime SafeAddYears(DateTime dt, int delta)
    {
        var year = dt.Year + delta;
        return year is < 1 or > 9999 ? dt : ReplaceYear(dt, year);
    }

    [Pure]
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

    #endregion

}
