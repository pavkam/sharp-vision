// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using Popups;

/// <summary>Displays a formatted date with inline segment editing and a Calendar popup for selection.</summary>
[PublicAPI]
public sealed class DateInput: InputBase
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
    private readonly TemporalValueState<DateOnly> _state;
    private CultureInfo _culture;

    #region Construction and properties

    /// <summary>Initializes a bordered date field with a connected Calendar popup.</summary>
    public DateInput()
    {
        _culture = CultureInfo.CurrentCulture.DateTimeFormat.Calendar is GregorianCalendar
            ? CultureInfo.CurrentCulture
            : CultureInfo.InvariantCulture;

        // Value resolves the current local date lazily, on first read, rather than here: a
        // control constructed off-dispatcher and then mounted under a dispatcher with its own
        // TimeProvider must observe that dispatcher's clock instead of latching the clock that
        // happened to be current at construction. The owned Calendar starts with no selection to
        // match; EnsureSeeded pushes the resolved value into it once seeding actually happens.
        _state = new TemporalValueState<DateOnly>(
            DateOnly.MinValue,
            DateOnly.MaxValue,
            this,
            VerifyMutable,
            NotifyPropertyChanged,
            () => DateOnly.FromDateTime(TimeProvider.GetLocalNow().DateTime),
            RaiseValueChanged,
            SynchronizeCalendarValue,
            SyncCalendarBounds,
            resolveValueImpact: ResolveValueWidthImpact);
        _calendarDropDown = new CalendarDropDownCoordinator<DateOnly>(
            _culture,
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
        _ = EnableSegmentEditing(
            BuildSegments,
            ApplySegmentDigit,
            ApplySegmentIncrement,
            ClearSegmentValue,
            new SegmentFieldKeyOptions(
                ResolveSegmentStepDelta,
                ClearValueCommand,
                handlePopupCommand: HandleDropDownOpeningCommand,
                handleRecognizedWithoutChange: true),
            reservesDropDownIndicator: true,
            activateFirstSegmentOnFocus: true,
            beforeInput: EnsureSeeded);
        TabNavigation = TabNavigation.None;
    }

    /// <summary>Raised after a committed value transition.</summary>
    public event EventHandler<DateInputValueChangedEventArgs>? ValueChanged;

    /// <summary>Raised after the Calendar popup opens.</summary>
    public event EventHandler? DropDownOpened;

    /// <summary>Raised after the Calendar popup closes.</summary>
    public event EventHandler? DropDownClosed;

    /// <summary>Gets or sets the committed date, or null when cleared.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public DateOnly? Value
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return _state.EnsureSeeded();
        }
        set => _ = _state.SetValue(value);
    }

    /// <summary>Gets or sets whether a null value is permitted.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool AllowNull
    {
        get => _state.AllowNull;
        set => _ = _state.SetAllowNull(value);
    }

    /// <summary>Gets or sets the Gregorian culture used for date formatting and segment order.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The culture's active calendar is not Gregorian, or the current <see cref="Format"/> cannot be rendered by a <see cref="DateOnly"/> under this culture.</exception>
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
                    "DateInput requires a Gregorian display culture.", nameof(value));
            }

            VerifyMutable();

            if (ReferenceEquals(_culture, value))
            {
                return;
            }

            TemporalFormatValidation.Validate(
                Format, value, nameof(value), "DateOnly", static (f, c) => _probeDate.ToString(f, c), _tokenKinds);

            _ = SetPropertyAndSynchronize(
                ref _culture,
                value,
                InvalidationImpact.Measure,
                () =>
                {
                    _calendarDropDown.SyncCulture(Culture);
                    InvalidateSegmentLayout();
                },
                ReferenceEqualityComparer.Instance);
        }
    }

    /// <summary>Gets or sets the date format string used for display.</summary>
    /// <remarks>The pattern must be renderable by <see cref="DateOnly"/> under <see cref="Culture"/>: a single
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
                value, _culture, nameof(value), "DateOnly", static (f, c) => _probeDate.ToString(f, c), _tokenKinds);

            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                InvalidateSegmentLayout();
            }
        }
    } = "d";

    /// <summary>Gets or sets the earliest selectable date.</summary>
    /// <exception cref="ArgumentException">The value exceeds <see cref="Maximum"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public DateOnly Minimum
    {
        get => _state.Minimum;
        set => _ = _state.SetMinimum(value);
    }

    /// <summary>Gets or sets the latest selectable date.</summary>
    /// <exception cref="ArgumentException">The value precedes <see cref="Minimum"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public DateOnly Maximum
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
            ValueChanged = null;
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

    #region Segment editing

    // Reports whether the clear actually changed the value, like the sibling fields do: Delete on
    // an already-empty field is then consumed only through the recognized-without-change policy
    // (which needs an editable segment to recognize it), never by claiming a transition that did
    // not happen.
    private bool ClearValueCommand() => AllowNull && _state.SetValue(null);

    private bool ApplySegmentIncrement(SegmentDescriptor segment, int delta)
    {
        var kind = segment.Kind!.Value;

        if (_state.Value is not { } date)
        {
            // AllowNull defaults to true, so a prior Delete (or an explicit Value = null) can
            // leave the value unset. Rather than refusing the increment outright, seed today's
            // date - the same seed DateInput resolves lazily at construction - so Up/Down starts
            // producing a value instead of silently doing nothing forever.
            return CommitSegmentValue(_state.Clamp(DateOnly.FromDateTime(TimeProvider.GetLocalNow().DateTime)));
        }

        if (kind == TemporalSegmentKind.Year)
        {
            var newYear = date.Year + delta;

            if (newYear is < 1 or > 9999)
            {
                // Silently ignore an increment that pushes the year beyond DateOnly's own
                // [1, 9999] range. The shared ReplaceYear helper clamps into that range rather
                // than throwing, so - unlike Month/Day below - the out-of-range case has to be
                // caught here, before calling it, instead of recovered from underneath via a
                // caught ArgumentOutOfRangeException. Mirrors DateTimeInput.SafeAddYears's own
                // pre-call guard.
                return false;
            }

            var (replacedYear, replacedMonth, replacedDay) =
                TemporalCalendarArithmetic.ReplaceYear(date.Month, date.Day, newYear);
            return CommitSegmentValue(_state.Clamp(new DateOnly(replacedYear, replacedMonth, replacedDay)));
        }

        try
        {
#pragma warning disable IDE0072 // Only date-kind segments are reachable from DateInput's layout.
            var adjusted = kind switch
            {
                TemporalSegmentKind.Month => date.AddMonths(delta),
                TemporalSegmentKind.Day => date.AddDays(delta),
                _ => date
            };
#pragma warning restore IDE0072

            return CommitSegmentValue(_state.Clamp(adjusted));
        }
        catch (ArgumentOutOfRangeException)
        {
            // Silently ignore increments that push beyond DateOnly bounds.
            return false;
        }
    }

    private bool ApplySegmentDigit(SegmentDescriptor segment, int value)
    {
        var kind = segment.Kind!.Value;

        if (_state.Value is not { } date)
        {
            // Same rationale as ApplySegmentIncrement: seed today's date instead of refusing,
            // so a digit typed after Delete lands on a real value rather than being dropped.
            _ = _state.SetValue(_state.Clamp(DateOnly.FromDateTime(TimeProvider.GetLocalNow().DateTime)));

            if (_state.Value is not { } seeded)
            {
                return false;
            }

            date = seeded;
        }

        try
        {
#pragma warning disable IDE0072 // Only date-kind segments are reachable from DateInput's layout.
            var (year, month, day) = kind switch
            {
                TemporalSegmentKind.Month => TemporalCalendarArithmetic.ReplaceMonth(
                    date.Year, date.Day, Math.Clamp(value, 1, 12)),
                TemporalSegmentKind.Day => TemporalCalendarArithmetic.ClampDayOfMonth(
                    date.Year, date.Month, Math.Clamp(value, 1, DateTime.DaysInMonth(date.Year, date.Month))),
                TemporalSegmentKind.Year => TemporalCalendarArithmetic.ReplaceYear(
                    date.Month, date.Day, Math.Clamp(value, 1, 9999)),
                _ => (date.Year, date.Month, date.Day)
            };
#pragma warning restore IDE0072

            return CommitSegmentValue(_state.Clamp(new DateOnly(year, month, day)));
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private bool ClearSegmentValue(SegmentDescriptor segment)
    {
        var kind = segment.Kind!.Value;

        if (_state.Value is not { } date)
        {
            return false;
        }

        try
        {
#pragma warning disable IDE0072 // Only date-kind segments are reachable from DateInput's layout.
            var (year, month, day) = kind switch
            {
                TemporalSegmentKind.Month => TemporalCalendarArithmetic.ReplaceMonth(date.Year, date.Day, 1),
                TemporalSegmentKind.Day => TemporalCalendarArithmetic.ClampDayOfMonth(date.Year, date.Month, 1),
                TemporalSegmentKind.Year => TemporalCalendarArithmetic.ReplaceYear(date.Month, date.Day, 1),
                _ => (date.Year, date.Month, date.Day)
            };
#pragma warning restore IDE0072

            return CommitSegmentValue(_state.Clamp(new DateOnly(year, month, day)));
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private bool CommitSegmentValue(DateOnly value)
    {
        var previous = _state.Value;
        Value = value;
        return _state.Value != previous;
    }

    private string ResolveDatePattern() =>
        Format.Length > 1
            ? Format
            : Format[0] switch
            {
                'd' => _culture.DateTimeFormat.ShortDatePattern,
                'D' => _culture.DateTimeFormat.LongDatePattern,
                'm' or 'M' => _culture.DateTimeFormat.MonthDayPattern,
                'y' or 'Y' => _culture.DateTimeFormat.YearMonthPattern,
                'o' or 'O' => "yyyy'-'MM'-'dd",
                'r' or 'R' => "ddd, dd MMM yyyy",
                _ => _culture.DateTimeFormat.ShortDatePattern
            };

    #endregion

    #region Rendering helpers

    private SegmentDescriptor[] BuildSegments() => BuildSegments(_state.Value);

    private SegmentDescriptor[] BuildSegments(DateOnly? value)
    {
        var pattern = ResolveDatePattern();
        var tokens = TemporalPatternSegmenter.ParseTokens(pattern, _tokenKinds, _culture);

        IReadOnlyList<string> text;

        if (value is { } date)
        {
            var renderingCulture = Format.Length == 1 && Format[0] is 'r' or 'R'
                ? CultureInfo.InvariantCulture
                : _culture;
            text = TemporalPatternSegmenter.FormatSegments(
                pattern,
                tokens,
                _tokenKinds,
                format => date.ToString(format, renderingCulture));
        }
        else
        {
            var placeholder = new string[tokens.Count];

            for (var index = 0; index < tokens.Count; index++)
            {
                var token = tokens[index];
#pragma warning disable IDE0072 // Only date-kind segments are reachable from DateInput's layout.
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
                    kind == TemporalSegmentKind.Year && token.RunLength >= 4 ? 4 : 2,
#pragma warning disable IDE0072 // Only date-kind segments are reachable from DateInput's layout.
                    kind switch
                    {
                        TemporalSegmentKind.Month => 12,
                        TemporalSegmentKind.Day => 31,
                        TemporalSegmentKind.Year => 9999,
                        _ => 0
                    });
#pragma warning restore IDE0072
        }

        return descriptors;
    }

    /// <summary>Grades a value transition by its resolved display-width delta, mirroring
    /// <see cref="ControlBase.GetAffixChangeImpact"/> for affixes: a same-width transition (for
    /// example incrementing a zero-padded day segment) needs only
    /// <see cref="InvalidationImpact.Render"/>, while a transition that widens or narrows the
    /// formatted text (a single-digit month or day widening to two digits under a non-padded
    /// <see cref="Format"/>) needs <see cref="InvalidationImpact.Measure"/> so the field box is
    /// remeasured instead of leaving stale geometry behind.</summary>
    private InvalidationImpact ResolveValueWidthImpact(DateOnly? previous, DateOnly? candidate) =>
        ResolveSegmentWidthImpact(BuildSegments(previous), BuildSegments(candidate));

    #endregion

    #region Value management

    /// <summary>Latches Value to the current local date on first read, so a control mounted under
    /// a dispatcher observes that dispatcher's clock instead of the clock current at
    /// construction, and pushes the newly resolved value into the owned Calendar. A value already
    /// committed - including an explicit null under <see cref="AllowNull"/> - is left
    /// untouched.</summary>
    private void EnsureSeeded() => _ = _state.EnsureSeeded();

    private void SyncCalendarBounds() => _calendarDropDown.SyncBounds();

    private void SynchronizeCalendarValue(DateOnly? value) =>
        _calendarDropDown.SyncValue(value);

    private void RaiseValueChanged(
        ref CallbackTransitionTransaction transition,
        DateOnly? previous,
        DateOnly? current)
    {
        transition.PublishCurrent(
            ValueChanged,
            this,
            new DateInputValueChangedEventArgs(previous, current));
    }

    #endregion

}
