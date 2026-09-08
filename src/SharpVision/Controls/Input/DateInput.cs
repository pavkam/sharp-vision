// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using Popups;

/// <summary>Displays a formatted date with inline segment editing and a Calendar popup for selection.</summary>
[PublicAPI]
public sealed class DateInput: TemporalInputBase<DateOnly>
{
    // A stable, representative date used to validate candidate formatting patterns.
    private static readonly DateOnly _probeDate = DateOnly.FromDateTime(DateTime.UnixEpoch);

    private static readonly IReadOnlyDictionary<char, TemporalSegmentKind> _tokenKinds =
        new Dictionary<char, TemporalSegmentKind>
        {
            ['M'] = TemporalSegmentKind.Month,
            ['d'] = TemporalSegmentKind.Day,
            ['y'] = TemporalSegmentKind.Year
        };

    private readonly CalendarDropDownCoordinator<DateOnly> _calendarDropDown;
    private readonly RetainedPartProperty<CalendarStyle?> _calendarStyle;
    private readonly RetainedPartProperty<Length> _dropDownHeight;
    private readonly Popup _popup;
    private readonly RetainedPartProperty<PopupChrome> _popupChrome;

    #region Construction and properties

    /// <summary>Initializes a bordered date field with a connected Calendar popup.</summary>
    public DateInput()
        : base(
            DateOnly.MinValue,
            DateOnly.MaxValue,
            CultureInfo.CurrentCulture.DateTimeFormat.Calendar is GregorianCalendar
                ? CultureInfo.CurrentCulture
                : CultureInfo.InvariantCulture)
    {
        // Value resolves the current local date lazily, on first read, rather than here: a
        // control constructed off-dispatcher and then mounted under a dispatcher with its own
        // TimeProvider must observe that dispatcher's clock instead of latching the clock that
        // happened to be current at construction. The owned Calendar starts with no selection to
        // match; EnsureSeeded pushes the resolved value into it once seeding actually happens.
        _calendarDropDown = new CalendarDropDownCoordinator<DateOnly>(
            Culture,
            EnsureSeeded,
            () => _state.Value,
            value => _ = _state.SetValue(value),
            static date => date,
            static (date, current) => date,
            () => Minimum,
            () => Maximum,
            () => _state.ValueVersion,
            () => _state.BoundsVersion,
            () => IsOpen,
            () => IsOpen = false,
            AcceptPopupAndClose);
        _popup = EnablePopupNavigationSession(
            _calendarDropDown.Calendar,
            placement: PopupPlacement.Below,
            focusOnOpen: true,
            popupTabNavigation: TabNavigation.Continue,
            beforeOpen: _calendarDropDown.BeforeOpen,
            beginSession: _calendarDropDown.BeginSession,
            handleNavigationKey: _calendarDropDown.HandleNavigationKey,
            cancelSession: _calendarDropDown.CancelSession,
            acceptSession: _calendarDropDown.AcceptSession);
        _popup.ContentHeightLimit = Length.Cells(10);

        // ActualCalendarStyle is a live projection of the owned Calendar's own ActualStyle rather
        // than a style slot DateInput owns directly, so nothing raises PropertyChanged for it on
        // either a local CalendarStyle assignment or a theme swap unless something forwards
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

    /// <summary>Gets or sets the date format string used for display.</summary>
    /// <remarks>The pattern must be renderable by <see cref="DateOnly"/> under <see cref="TemporalInputBase{TValue}.Culture"/>: a single
    /// standard specifier outside <see cref="DateOnly"/>'s own set, or any pattern containing a time specifier,
    /// is rejected.</remarks>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value is empty, or cannot be rendered by a <see cref="DateOnly"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string Format
    {
        get;
        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);
            TemporalFormatValidation.Validate(
                value, Culture, nameof(value), "DateOnly", static (f, c) => _probeDate.ToString(f, c), _tokenKinds);

            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                InvalidateSegmentLayout();
            }
        }
    } = "d";

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
        if (Bounds.Width == 0 || Bounds.Height == 0)
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
    protected override bool ClearValue() => AllowNull && _state.SetValue(null);

    /// <inheritdoc/>
    protected override DateOnly ResolveClockSeed() => DateOnly.FromDateTime(TimeProvider.GetLocalNow().DateTime);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<char, TemporalSegmentKind> TokenKinds => _tokenKinds;

    /// <inheritdoc/>
    protected override string ResolvePattern() =>
        Format.Length > 1
            ? Format
            : Format[0] switch
            {
                'd' => Culture.DateTimeFormat.ShortDatePattern,
                'D' => Culture.DateTimeFormat.LongDatePattern,
                'm' or 'M' => Culture.DateTimeFormat.MonthDayPattern,
                'y' or 'Y' => Culture.DateTimeFormat.YearMonthPattern,
                'o' or 'O' => "yyyy'-'MM'-'dd",
                'r' or 'R' => "ddd, dd MMM yyyy",
                _ => Culture.DateTimeFormat.ShortDatePattern
            };

    /// <inheritdoc/>
    protected override CultureInfo ResolveRenderingCulture() =>
        Format.Length == 1 && Format[0] is 'r' or 'R' ? CultureInfo.InvariantCulture : Culture;

    /// <inheritdoc/>
    protected override string FormatValue(DateOnly value, string format, CultureInfo culture) =>
        value.ToString(format, culture);

    /// <inheritdoc/>
    protected override int MaxValueFor(TemporalSegmentKind kind, bool hasAmPmDesignator, int runLength)
    {
        _ = hasAmPmDesignator;
        _ = runLength;
#pragma warning disable IDE0072 // Only date-kind segments are reachable from DateInput's layout.
        return kind switch
        {
            TemporalSegmentKind.Month => 12,
            TemporalSegmentKind.Day => 31,
            TemporalSegmentKind.Year => 9999,
            _ => 0
        };
#pragma warning restore IDE0072
    }

    /// <inheritdoc/>
    protected override DateOnly? Increment(DateOnly value, TemporalSegmentKind kind, int delta, int digitCapacity)
    {
        _ = digitCapacity;

        if (kind == TemporalSegmentKind.Year)
        {
            var newYear = value.Year + delta;

            if (newYear is < 1 or > 9999)
            {
                // Silently ignore an increment that pushes the year beyond DateOnly's own
                // [1, 9999] range. The shared ReplaceYear helper clamps into that range rather
                // than throwing, so - unlike Month/Day below - the out-of-range case has to be
                // caught here, before calling it, instead of recovered from underneath via a
                // caught ArgumentOutOfRangeException. Mirrors DateTimeInput.SafeAddYears's own
                // pre-call guard.
                return null;
            }

            var (replacedYear, replacedMonth, replacedDay) =
                TemporalCalendarArithmetic.ReplaceYear(value.Month, value.Day, newYear);
            return new DateOnly(replacedYear, replacedMonth, replacedDay);
        }

        try
        {
#pragma warning disable IDE0072 // Only date-kind segments are reachable from DateInput's layout.
            return kind switch
            {
                TemporalSegmentKind.Month => value.AddMonths(delta),
                TemporalSegmentKind.Day => value.AddDays(delta),
                _ => value
            };
#pragma warning restore IDE0072
        }
        catch (ArgumentOutOfRangeException)
        {
            // Silently ignore increments that push beyond DateOnly bounds.
            return null;
        }
    }

    /// <inheritdoc/>
    protected override DateOnly? ApplyDigit(DateOnly value, TemporalSegmentKind kind, int digitValue, int digitCapacity)
    {
        _ = digitCapacity;

        try
        {
#pragma warning disable IDE0072 // Only date-kind segments are reachable from DateInput's layout.
            var (year, month, day) = kind switch
            {
                TemporalSegmentKind.Month => TemporalCalendarArithmetic.ReplaceMonth(
                    value.Year, value.Day, Math.Clamp(digitValue, 1, 12)),
                TemporalSegmentKind.Day => TemporalCalendarArithmetic.ClampDayOfMonth(
                    value.Year, value.Month, Math.Clamp(digitValue, 1, DateTime.DaysInMonth(value.Year, value.Month))),
                TemporalSegmentKind.Year => TemporalCalendarArithmetic.ReplaceYear(
                    value.Month, value.Day, Math.Clamp(digitValue, 1, 9999)),
                _ => (value.Year, value.Month, value.Day)
            };
#pragma warning restore IDE0072

            return new DateOnly(year, month, day);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    protected override DateOnly? ClearSegment(DateOnly value, TemporalSegmentKind kind)
    {
        try
        {
#pragma warning disable IDE0072 // Only date-kind segments are reachable from DateInput's layout.
            var (year, month, day) = kind switch
            {
                TemporalSegmentKind.Month => TemporalCalendarArithmetic.ReplaceMonth(value.Year, value.Day, 1),
                TemporalSegmentKind.Day => TemporalCalendarArithmetic.ClampDayOfMonth(value.Year, value.Month, 1),
                TemporalSegmentKind.Year => TemporalCalendarArithmetic.ReplaceYear(value.Month, value.Day, 1),
                _ => (value.Year, value.Month, value.Day)
            };
#pragma warning restore IDE0072

            return new DateOnly(year, month, day);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    protected override void ValidateCulture(CultureInfo culture)
    {
        if (culture.DateTimeFormat.Calendar is not GregorianCalendar)
        {
            throw new ArgumentException(
                "DateInput requires a Gregorian display culture.", nameof(culture));
        }

        TemporalFormatValidation.Validate(
            Format, culture, "value", "DateOnly", static (f, c) => _probeDate.ToString(f, c), _tokenKinds);
    }

    /// <inheritdoc/>
    protected override void SynchronizeValue(DateOnly? value) => _calendarDropDown.SyncValue(value);

    /// <inheritdoc/>
    protected override void SynchronizeBounds() => _calendarDropDown.SyncBounds();

    /// <inheritdoc/>
    protected override void SynchronizeCulture(CultureInfo culture) => _calendarDropDown.SyncCulture(culture);

    /// <inheritdoc/>
    protected override Func<KeyEventArgs, bool?>? ResolvePopupCommand() => HandleDropDownOpeningCommand;

    /// <inheritdoc/>
    protected override bool ReservesDropDownIndicator => true;

    /// <inheritdoc/>
    protected override bool ActivateFirstSegmentOnFocus => true;

    #endregion

}
