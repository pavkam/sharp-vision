// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using Popups;

/// <summary>Combines date and time editing in one bordered field control with an optional calendar popup.</summary>
/// <remarks>
/// The control displays a formatted <see cref="DateTime"/> value with date segments (month, day, year)
/// followed by time segments (hour, minute, optionally second, fractional second, and AM/PM).
/// Every segment edits inline; the disclosure indicator opens the <see cref="Calendar"/> popup.
/// Up/Down arrows increment or decrement the focused segment. Left/Right arrows navigate between segments.
/// Typing digits replaces the segment value. Delete clears the value to null when <see cref="TemporalInputBase{TValue}.AllowNull"/> is set.
/// Custom <c>f</c> and <c>F</c> runs expose one to seven fractional digits; uppercase runs reserve
/// blank editing cells when formatted trailing zeroes are omitted. Alt+Down or F4 opens the calendar
/// popup from any segment.
/// </remarks>
[PublicAPI]
public sealed class DateTimeInput: TemporalInputBase<DateTime>
{
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
            ['f'] = TemporalSegmentKind.FractionalSecond,
            ['F'] = TemporalSegmentKind.FractionalSecond,
            ['t'] = TemporalSegmentKind.AmPmDesignator
        };

    private readonly CalendarDropDownCoordinator<DateTime> _calendarDropDown;
    private readonly RetainedPartProperty<CalendarStyle?> _calendarStyle;
    private readonly RetainedPartProperty<Length> _dropDownHeight;
    private readonly Popup _popup;
    private readonly RetainedPartProperty<PopupChrome> _popupChrome;

    #region Construction and properties

    /// <summary>Initializes a focusable date-time input at the current local date and time with a light field border and a connected calendar popup.</summary>
    public DateTimeInput()
        : base(DateTime.MinValue, DateTime.MaxValue, CultureInfo.InvariantCulture)
    {
        // Value resolves the current local date and time lazily, on first read, rather than
        // here: a control constructed off-dispatcher and then mounted under a dispatcher with its
        // own TimeProvider must observe that dispatcher's clock instead of latching the clock
        // that happened to be current at construction. The owned Calendar starts with no
        // selection to match; EnsureSeeded pushes the resolved value into it once seeding
        // actually happens.
        _calendarDropDown = new CalendarDropDownCoordinator<DateTime>(
            Culture,
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
        _ = RegisterRetainedPartProperty(
            _calendarDropDown.Calendar,
            nameof(Calendar.ActualStyle),
            nameof(ActualCalendarStyle),
            () => _calendarDropDown.Calendar.ActualStyle);
        _dropDownHeight = ForwardPartProperty(
            _popup,
            nameof(Popup.ContentHeightLimit),
            nameof(DropDownHeight),
            () => _popup.ContentHeightLimit,
            value => _popup.ContentHeightLimit = value,
            InvalidationImpact.Measure);
        _popupChrome = ForwardPartProperty(
            _popup,
            nameof(Popup.Style),
            nameof(PopupChrome),
            () => _popup.Style,
            value => _popup.Style = value);
        _calendarStyle = ForwardPartProperty(
            _calendarDropDown.Calendar,
            nameof(Calendar.Style),
            nameof(CalendarStyle),
            () => _calendarDropDown.Calendar.Style,
            value => _calendarDropDown.Calendar.Style = value);
        EnablePressActivation();
        TabNavigation = TabNavigation.None;
    }

    /// <summary>Raised after the Calendar popup opens.</summary>
    public event EventHandler? DropDownOpened;

    /// <summary>Raised after the Calendar popup closes.</summary>
    public event EventHandler? DropDownClosed;

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
                InvalidateSegmentLayout();
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
                InvalidateSegmentLayout();
            }
        }
    }

    /// <summary>Gets or sets a custom combined date-time format pattern, or null to derive the
    /// pattern from <see cref="TemporalInputBase{TValue}.Culture"/>'s <see cref="DateTimeFormatInfo.ShortDatePattern"/> plus
    /// <see cref="Use24HourFormat"/> and <see cref="ShowSeconds"/>. Default is null.</summary>
    /// <remarks>
    /// When set, the pattern's own token runs - not <see cref="TemporalInputBase{TValue}.Culture"/>'s date pattern or
    /// <see cref="Use24HourFormat"/>/<see cref="ShowSeconds"/> - determine the segment order and
    /// count; pair a 12-hour <c>h</c>/<c>hh</c> hour token with a <c>t</c>/<c>tt</c> AM/PM
    /// designator token for correct 12-hour clamping and rendering, since a lowercase hour token
    /// without a designator token is treated as a 24-hour segment for both editing and display.
    /// </remarks>
    /// <exception cref="ArgumentException">The value is empty, cannot be rendered by a <see cref="DateTime"/> under <see cref="TemporalInputBase{TValue}.Culture"/>, or declares an editable fractional-second run wider than seven digits (including via a percent-escaped <c>%f</c> run), which the segmented layout cannot represent even if <see cref="DateTime"/> itself would render it.</exception>
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
                    value, Culture, nameof(value), "DateTime", static (f, c) => DateTime.MinValue.ToString(f, c), _tokenKinds);
            }

            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                InvalidateSegmentLayout();
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

    /// <summary>Gets or sets the intrinsic, fixed, or placement-side-relative maximum visible calendar height.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A fixed or percentage value is zero.</exception>
    /// <exception cref="ArgumentException">The value uses proportional sizing.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Length DropDownHeight
    {
        get => _dropDownHeight.Value;
        set => _dropDownHeight.Value = value;
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
        get => _popupChrome.Value;
        set => _popupChrome.Value = value;
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
        get => _calendarStyle.Value;
        set => _calendarStyle.Value = value;
    }

    /// <summary>Gets the resolved presentation of the owned Calendar.</summary>
    public CalendarStyle ActualCalendarStyle => _calendarDropDown.Calendar.ActualStyle;

    /// <summary>Gets the retained calendar for proving bound synchronization invariants.</summary>
    internal Calendar OwnedCalendar => _calendarDropDown.Calendar;

    #endregion

    #region Input, layout, and rendering

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        EnsureSeeded();
        return MeasureSegmentedField();
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
        RenderSegmentedField(canvas, isPlaceholder: _state.Value is null);
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            _calendarDropDown.Dispose();
            DropDownOpened = null;
            DropDownClosed = null;
        }
    }

    #endregion

    #region Keyboard input

    // "a" selects AM and "p" selects PM rather than toggling: a user who presses the letter of
    // the half of the day they want must never be flipped to the other half because the value
    // already happened to be there.
    private bool HandleCharacterCommand(Rune character) =>
        TemporalSegmentClassification.TryGetAmPmSelection(character, out var selectPm) && SelectAmPm(selectPm);

    private bool SelectAmPm(bool selectPm) =>
        TemporalSegmentClassification.SelectAmPm(
            BuildSegments,
            () => _state.Value.HasValue,
            () => _state.Value is { Hour: >= 12 },
            _segments,
            selectPm);

    /// <summary>Gets whether the current layout - whether derived from <see cref="Use24HourFormat"/>
    /// or overridden by <see cref="Format"/> - includes an AM/PM designator segment, used as the
    /// effective 12-versus-24-hour policy for editing the hour segment.</summary>
    private bool HasAmPmDesignator => TemporalSegmentClassification.HasAmPmDesignator(BuildSegments);

    #endregion

    #region Segment value callbacks

    /// <inheritdoc/>
    protected override DateTime? ApplyDigit(DateTime value, TemporalSegmentKind kind, int digitValue, int digitCapacity)
    {
        var dt = value;
        var hasAmPm = HasAmPmDesignator;

        try
        {
#pragma warning disable IDE0072 // AmPmDesignator never reaches this callback: its digit capacity is zero.
            return kind switch
            {
                TemporalSegmentKind.Month => ReplaceMonth(dt, Math.Clamp(digitValue, 1, 12)),
                TemporalSegmentKind.Day => ReplaceDay(dt, Math.Clamp(digitValue, 1,
                    DateTime.DaysInMonth(dt.Year, dt.Month))),
                TemporalSegmentKind.Year => ReplaceYear(dt, Math.Clamp(digitValue, 1, 9999)),
                TemporalSegmentKind.Hour when hasAmPm =>
                    WithSubSecondTicksOf(dt.Date.Add(new TimeSpan(
                        TemporalSegmentClassification.To24Hour(
                            TemporalClockArithmetic.ClampHour(digitValue, hasAmPmDesignator: true),
                            dt.Hour >= 12),
                        dt.Minute, dt.Second)), dt),
                TemporalSegmentKind.Hour =>
                    WithSubSecondTicksOf(dt.Date.Add(new TimeSpan(
                        TemporalClockArithmetic.ClampHour(digitValue, hasAmPmDesignator: false), dt.Minute, dt.Second)), dt),
                TemporalSegmentKind.Minute =>
                    WithSubSecondTicksOf(dt.Date.Add(new TimeSpan(
                        dt.Hour, TemporalClockArithmetic.ClampMinuteOrSecond(digitValue), dt.Second)), dt),
                TemporalSegmentKind.Second =>
                    WithSubSecondTicksOf(dt.Date.Add(new TimeSpan(
                        dt.Hour, dt.Minute, TemporalClockArithmetic.ClampMinuteOrSecond(digitValue))), dt),
                TemporalSegmentKind.FractionalSecond => new DateTime(
                    dt.Ticks - (dt.Ticks % TimeSpan.TicksPerSecond) +
                    TemporalClockArithmetic.FractionalSecondTicks(digitValue, digitCapacity),
                    dt.Kind),
                _ => dt
            };
#pragma warning restore IDE0072
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    protected override DateTime? Increment(DateTime value, TemporalSegmentKind kind, int delta, int digitCapacity)
    {
        var dt = value;

        // Every case below is only reached for a kind the current layout actually contains
        // (the engine dispatches by the active segment's own kind), so no additional
        // Use24HourFormat/ShowSeconds guard is needed here.
#pragma warning disable IDE0072 // Every calendar/clock kind is individually handled or intentionally falls through.
        return kind switch
        {
            TemporalSegmentKind.Month => SafeAddMonths(dt, delta),
            TemporalSegmentKind.Day => SafeAddDays(dt, delta),
            TemporalSegmentKind.Year => SafeAddYears(dt, delta),
            TemporalSegmentKind.Hour => SafeAddTicks(dt, TimeSpan.TicksPerHour * delta),
            TemporalSegmentKind.Minute => SafeAddTicks(dt, TimeStep.Ticks * delta),
            TemporalSegmentKind.Second => SafeAddTicks(dt, TimeSpan.TicksPerSecond * delta),
            TemporalSegmentKind.FractionalSecond => SafeAddTicks(
                dt,
                TemporalClockArithmetic.FractionalSecondUnitTicks(digitCapacity) * delta),
            TemporalSegmentKind.AmPmDesignator => dt.AddHours(dt.Hour < 12 ? 12 : -12),
            _ => dt
        };
#pragma warning restore IDE0072
    }

    /// <inheritdoc/>
    protected override DateTime? ClearSegment(DateTime value, TemporalSegmentKind kind)
    {
        var dt = value;

        try
        {
#pragma warning disable IDE0072 // AmPmDesignator is intentionally a no-op here.
            return kind switch
            {
                TemporalSegmentKind.Month => ReplaceMonth(dt, 1),
                TemporalSegmentKind.Day => ReplaceDay(dt, 1),
                TemporalSegmentKind.Year => ReplaceYear(dt, 1),
                TemporalSegmentKind.Hour => WithSubSecondTicksOf(dt.Date.Add(new TimeSpan(0, dt.Minute, dt.Second)), dt),
                TemporalSegmentKind.Minute => WithSubSecondTicksOf(dt.Date.Add(new TimeSpan(dt.Hour, 0, dt.Second)), dt),
                TemporalSegmentKind.Second => WithSubSecondTicksOf(dt.Date.Add(new TimeSpan(dt.Hour, dt.Minute, 0)), dt),
                TemporalSegmentKind.FractionalSecond => new DateTime(
                    dt.Ticks - (dt.Ticks % TimeSpan.TicksPerSecond),
                    dt.Kind),
                _ => dt
            };
#pragma warning restore IDE0072
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    #endregion

    #region Commit and validation

    [Pure]
    private DateOnly ResolveCalendarMinimum() => Minimum > DateTime.MinValue
        ? DateOnly.FromDateTime(Minimum)
        : DateOnly.MinValue;

    [Pure]
    private DateOnly ResolveCalendarMaximum() => Maximum < DateTime.MaxValue
        ? DateOnly.FromDateTime(Maximum)
        : DateOnly.MaxValue;

    [Pure]
    private DateTime CombineCalendarDate(DateOnly date, DateTime? current)
    {
        var timePart = current?.TimeOfDay ?? TimeSpan.Zero;
        var kind = current?.Kind ?? DateTimeKind.Unspecified;
        var naive = date.ToDateTime(TimeOnly.FromTimeSpan(timePart), kind);

        // The day-granularity Calendar projects Minimum/Maximum onto whole days
        // (ResolveCalendarMinimum/ResolveCalendarMaximum), so a boundary day renders as a fully
        // selectable cell even when only part of it actually falls in range - the rest of the
        // day sits below Minimum's time-of-day or above Maximum's. Combining that day with the
        // preserved, untouched current time-of-day can therefore land outside [Minimum, Maximum]
        // even though the user only manipulated the date through the popup click. Rather than
        // let that fall through to Clamp - which repairs by substituting the whole boundary
        // instant, silently rewriting the time the user never touched - prefer nudging the date
        // itself to the nearest adjacent day that admits the original time-of-day, keeping the
        // axis the user actually chose authoritative. This can only be reached at the boundary
        // day itself: every other selectable day lies strictly between Minimum's and Maximum's
        // dates, so any time-of-day on it is already in range. Clamp remains the fallback for a
        // window so narrow that even the adjacent day does not help.
        if (naive < Minimum && date == DateOnly.FromDateTime(Minimum) && date < DateOnly.MaxValue)
        {
            var advanced = date.AddDays(1).ToDateTime(TimeOnly.FromTimeSpan(timePart), kind);

            if (advanced >= Minimum && advanced <= Maximum)
            {
                return advanced;
            }
        }
        else if (naive > Maximum && date == DateOnly.FromDateTime(Maximum) && date > DateOnly.MinValue)
        {
            var retreated = date.AddDays(-1).ToDateTime(TimeOnly.FromTimeSpan(timePart), kind);

            if (retreated >= Minimum && retreated <= Maximum)
            {
                return retreated;
            }
        }

        return naive;
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

    #region Temporal seams

    /// <inheritdoc/>
    protected override DateTime ResolveClockSeed() => TimeProvider.GetLocalNow().DateTime;

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<char, TemporalSegmentKind> TokenKinds => _tokenKinds;

    /// <inheritdoc/>
    protected override string ResolvePattern() => Format ?? BuildDefaultDateTimePattern();

    private string BuildDefaultDateTimePattern() =>
        $"{Culture.DateTimeFormat.ShortDatePattern} {TemporalClockSegments.BuildDefaultTimePattern(Use24HourFormat, ShowSeconds)}";

    /// <inheritdoc/>
    protected override string AdjustRenderingPattern(string pattern, bool hasAmPmDesignator) =>
        hasAmPmDesignator ? pattern : NormalizeDesignatorlessHourPattern(pattern);

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

    /// <inheritdoc/>
    protected override string FormatValue(DateTime value, string format, CultureInfo culture) =>
        value.ToString(format, culture);

    /// <inheritdoc/>
    protected override bool IsPm(DateTime value) => value.Hour >= 12;

    /// <inheritdoc/>
#pragma warning disable IDE0072 // Every segment kind is individually handled.
    protected override int MaxValueFor(TemporalSegmentKind kind, bool hasAmPmDesignator, int runLength) =>
        kind switch
        {
            TemporalSegmentKind.Month => 12,
            TemporalSegmentKind.Day => 31,
            TemporalSegmentKind.Year => 9999,
            _ => TemporalClockSegments.MaxValueFor(kind, hasAmPmDesignator, runLength)
        };
#pragma warning restore IDE0072

    /// <inheritdoc/>
    protected override void ValidateCulture(CultureInfo culture)
    {
        if (culture.DateTimeFormat.Calendar is not GregorianCalendar)
        {
            throw new ArgumentException(
                "DateTimeInput requires a Gregorian display culture.", nameof(culture));
        }

        if (Format is { } format)
        {
            TemporalFormatValidation.Validate(
                format, culture, "value", "DateTime", static (f, c) => DateTime.MinValue.ToString(f, c), _tokenKinds);
        }
    }

    /// <inheritdoc/>
    protected override void SynchronizeValue(DateTime? value) => _calendarDropDown.SyncValue(value);

    /// <inheritdoc/>
    protected override void SynchronizeBounds() => _calendarDropDown.SyncBounds();

    /// <inheritdoc/>
    protected override void SynchronizeCulture(CultureInfo culture) => _calendarDropDown.SyncCulture(culture);

    /// <inheritdoc/>
    protected override Func<Rune, bool>? ResolveCharacterCommand() => HandleCharacterCommand;

    /// <inheritdoc/>
    protected override Func<KeyEventArgs, bool?>? ResolvePopupCommand() => HandleDropDownOpeningCommand;

    /// <inheritdoc/>
    protected override bool ReservesDropDownIndicator => true;

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
        result.AddTicks(TemporalClockSegments.SubSecondRemainderTicks(original.Ticks));

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
