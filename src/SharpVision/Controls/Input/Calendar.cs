// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using SharpVision.Terminal.Input;

/// <summary>Displays one Gregorian month for single-date or inclusive-interval selection.</summary>
[PublicAPI]
public sealed class Calendar: Control
{
    /// <summary>
    /// The day-grid background halves for the hovered/preview, selected, and disabled states
    /// (<see cref="ThemeColor.ActiveControl"/>, <see cref="ThemeColor.SelectedControl"/>,
    /// <see cref="ThemeColor.DisabledControl"/>) and the header's <see cref="ThemeColor.Accent"/>
    /// arrows still resolve directly against the Theme rather than through
    /// <see cref="CalendarStyle"/> (see #244) — only the five day-grid foregrounds and the content
    /// inset are exposed on the style surface in this pass. The style contract's structural
    /// comparison still compares all of them so a theme swap confined to any one role continues to
    /// repaint, exactly as the pre-#244 manual diff block did (see #161).
    /// </summary>
    private static readonly StyleContract<CalendarStyle> _styleContract = new(
        ThemeRole.Input,
        static profile => new CalendarStyle(
            CalendarStyle.Default.SelectedDayColor,
            CalendarStyle.Default.TodayMarkerColor,
            CalendarStyle.Default.OutOfMonthDayColor,
            CalendarStyle.Default.WeekdayHeaderColor,
            CalendarStyle.Default.DisabledDayColor,
            CalendarStyle.Default.ContentInset,
            profile),
        static (previous, previousTheme, current, currentTheme) =>
            previous.ContentInset != current.ContentInset
                ? InvalidationImpact.Measure
                : previous != current ||
                  ResolveColor(previous.SelectedDayColor, previousTheme) != ResolveColor(current.SelectedDayColor, currentTheme) ||
                  ResolveColor(previous.TodayMarkerColor, previousTheme) != ResolveColor(current.TodayMarkerColor, currentTheme) ||
                  ResolveColor(previous.OutOfMonthDayColor, previousTheme) != ResolveColor(current.OutOfMonthDayColor, currentTheme) ||
                  ResolveColor(previous.WeekdayHeaderColor, previousTheme) != ResolveColor(current.WeekdayHeaderColor, currentTheme) ||
                  ResolveColor(previous.DisabledDayColor, previousTheme) != ResolveColor(current.DisabledDayColor, currentTheme) ||
                  ResolveDirectColor(previousTheme, ThemeColor.Accent) != ResolveDirectColor(currentTheme, ThemeColor.Accent) ||
                  ResolveDirectColor(previousTheme, ThemeColor.ActiveControl) != ResolveDirectColor(currentTheme, ThemeColor.ActiveControl) ||
                  ResolveDirectColor(previousTheme, ThemeColor.SelectedControl) != ResolveDirectColor(currentTheme, ThemeColor.SelectedControl) ||
                  ResolveDirectColor(previousTheme, ThemeColor.DisabledControl) != ResolveDirectColor(currentTheme, ThemeColor.DisabledControl)
                    ? InvalidationImpact.Render
                    : InvalidationImpact.None,
        static style => style.Appearance);
    private CalendarStyle? _actualStyleCache;
    private CalendarStyle? _actualStyleCacheKey;
    private Theme? _actualStyleCacheTheme;

    /// <summary>Gets or sets the complete local presentation, or null to use the semantic input profile.</summary>
    /// <exception cref="InvalidOperationException">The attached Calendar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Calendar is disposed.</exception>
    public CalendarStyle? Style
    {
        get;
        set
        {
            if (SetControlStyle(
                ref field,
                value,
                _styleContract.Resolve,
                _styleContract.CompareStructure,
                _styleContract.Appearance,
                nameof(Style),
                nameof(ActualStyle)))
            {
                Padding = ActualStyle.ContentInset;
            }
        }
    }

    /// <summary>Gets the complete local presentation or the library day-grid mechanics completed with the semantic input profile.</summary>
    public CalendarStyle ActualStyle =>
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

    private static Color ResolveDirectColor(Theme? theme, ThemeColor color) =>
        theme?.ResolveColor(color) ?? Color.Default;

    private Color ResolveColor(ColorValue value) => ResolveColor(value, Theme);
    private const TerminalAttributes _blinkAttributes =
        TerminalAttributes.Blink | TerminalAttributes.RapidBlink;
    private const int _cellWidth = 4;
    private const int _columnCount = 7;
    // The header and weekday rows occupy one terminal cell row each; dates begin after both.
    private const int _headerHeight = 1;
    private const int _weekdayRowOffset = 1;
    private const int _dateGridOffset = 2;
    private const int _minimumHeaderWidth = 2;
    private const int _contentHeight = 8;
    private const int _contentWidth = _cellWidth * _columnCount;
    private const int _weekCount = 6;

    private DateOnly _activeDate;
    private CultureInfo _culture;
    private DateOnly _displayMonth;
    private DateOnly? _hoveredDate;
    private DateOnly? _intervalAnchor;
    private readonly Dictionary<DateOnly, CalendarDateMarkup> _markup = [];
    private DateInterval? _selection;
    private DayOfWeek _firstDayOfWeek;

    #region Construction and public state

    /// <summary>Initializes a focusable calendar at the current local date.</summary>
    public Calendar()
    {
        var today = DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime);
        _activeDate = today;
        _culture = CultureInfo.CurrentCulture.DateTimeFormat.Calendar is GregorianCalendar
            ? CultureInfo.CurrentCulture
            : CultureInfo.InvariantCulture;
        _firstDayOfWeek = _culture.DateTimeFormat.FirstDayOfWeek;
        _displayMonth = StartOfMonth(today);
        BlockedDates = new CalendarBlockedDateCollection(this);
        Focusable = true;
        TabStop = true;
        TabNavigation = TabNavigation.None;
        Padding = ActualStyle.ContentInset;
    }

    /// <summary>Gets the normalized collection of dates unavailable for selection.</summary>
    public CalendarBlockedDateCollection BlockedDates { get; }

    /// <summary>Sets the complete trusted markup face for one date.</summary>
    /// <param name="date">The exact date to decorate.</param>
    /// <param name="markup">The non-null markup that must produce visible text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="markup"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="markup"/> produces no visible text.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void SetMarkup(DateOnly date, string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        CalendarDateMarkup parsed;

        try
        {
            parsed = new CalendarDateMarkup(markup);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(exception.Message, nameof(markup), exception);
        }

        VerifyMutable();

        if (_markup.TryGetValue(date, out var existing) && existing.Source == markup)
        {
            return;
        }

        _markup[date] = parsed;
        Invalidate(InvalidationImpact.Render);
    }

    /// <summary>Removes the authored markup face for one date.</summary>
    /// <param name="date">The exact date whose default numeric face should return.</param>
    /// <returns>True when an authored face was removed.</returns>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool RemoveMarkup(DateOnly date)
    {
        VerifyMutable();

        if (!_markup.Remove(date))
        {
            return false;
        }

        Invalidate(InvalidationImpact.Render);
        return true;
    }

    /// <summary>Removes every authored date face.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void ClearMarkup()
    {
        VerifyMutable();

        if (_markup.Count == 0)
        {
            return;
        }

        _markup.Clear();
        Invalidate(InvalidationImpact.Render);
    }

    /// <summary>Gets the exact authored markup for a date, or null.</summary>
    /// <param name="date">The exact date to inspect.</param>
    /// <returns>The authored markup, or null when the numeric face is active.</returns>
    internal string? GetMarkup(DateOnly date) =>
        _markup.TryGetValue(date, out var markup) ? markup.Source : null;

    /// <summary>Raised after a changed selection commits.</summary>
    public event EventHandler<CalendarSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Gets or sets whether activation selects one date or an inclusive interval.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public CalendarSelectionMode SelectionMode
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The selection mode is unknown.");
            }

            if (SetProperty(ref field, value, InvalidationImpact.Render))
            {
                SetIntervalAnchor(null);
                _ = CommitSelection(null);
            }
        }
    }

    /// <summary>Gets or sets the committed selection, represented as one inclusive interval.</summary>
    /// <exception cref="ArgumentException">Select mode receives a multi-day interval or a blocked date.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public DateInterval? Selection
    {
        get => _selection;
        set => _ = CommitSelection(value);
    }

    /// <summary>Commits one date using the current selection contract.</summary>
    /// <param name="date">The date to select.</param>
    /// <returns>True when the committed selection changed.</returns>
    /// <exception cref="ArgumentException">The date is blocked.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The date is outside the calendar bounds.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool Select(DateOnly date) => CommitSelection(new DateInterval(date, date));

    /// <summary>Commits one ordered inclusive interval using the current selection contract.</summary>
    /// <param name="start">The inclusive first date.</param>
    /// <param name="end">The inclusive last date.</param>
    /// <returns>True when the committed selection changed.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="end"/> precedes <paramref name="start"/>, select mode receives multiple
    /// dates, or the interval contains a blocked date.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The interval is outside the calendar bounds.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool Select(DateOnly start, DateOnly end) => CommitSelection(new DateInterval(start, end));

    /// <summary>Clears the committed selection without changing the active date.</summary>
    /// <returns>True when a committed selection was removed.</returns>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool ClearSelection() => CommitSelection(null);

    /// <summary>Gets the current keyboard-navigation date.</summary>
    public DateOnly ActiveDate => _activeDate;

    /// <summary>Gets the pending first interval endpoint, or null.</summary>
    internal DateOnly? IntervalAnchor => _intervalAnchor;

    /// <summary>Gets the directly hovered selectable date, or null.</summary>
    internal DateOnly? HoveredDate => _hoveredDate;

    /// <summary>Gets or sets the earliest selectable date.</summary>
    /// <exception cref="ArgumentException">The value exceeds <see cref="MaximumDate"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public DateOnly MinimumDate
    {
        get;
        set
        {
            if (value > MaximumDate)
            {
                throw new ArgumentException("MinimumDate cannot exceed MaximumDate.", nameof(value));
            }

            if (SetProperty(ref field, value, InvalidationImpact.Render))
            {
                RepairState();
            }
        }
    } = DateOnly.MinValue;

    /// <summary>Gets or sets the latest selectable date.</summary>
    /// <exception cref="ArgumentException">The value precedes <see cref="MinimumDate"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public DateOnly MaximumDate
    {
        get;
        set
        {
            if (value < MinimumDate)
            {
                throw new ArgumentException("MaximumDate cannot precede MinimumDate.", nameof(value));
            }

            if (SetProperty(ref field, value, InvalidationImpact.Render))
            {
                RepairState();
            }
        }
    } = DateOnly.MaxValue;

    /// <summary>Gets or sets the Gregorian culture used for month and weekday names.</summary>
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
                throw new ArgumentException("Calendar requires a Gregorian display culture.", nameof(value));
            }

            if (SetProperty(ref _culture, value, InvalidationImpact.Render))
            {
                FirstDayOfWeek = value.DateTimeFormat.FirstDayOfWeek;
            }
        }
    }

    /// <summary>Gets or sets the weekday shown in the first calendar column.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined weekday.</exception>
    /// <exception cref="InvalidOperationException">The attached calendar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The calendar is disposed.</exception>
    public DayOfWeek FirstDayOfWeek
    {
        get => _firstDayOfWeek;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The weekday is unknown.");
            }

            _ = SetProperty(ref _firstDayOfWeek, value, InvalidationImpact.Render);
        }
    }

    /// <summary>Moves the active date and displayed month to today when today is selectable.</summary>
    /// <returns><see langword="true"/> when the calendar moved to today.</returns>
    /// <exception cref="InvalidOperationException">The attached calendar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The calendar is disposed.</exception>
    public bool GoToToday()
    {
        VerifyMutable();
        var today = DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime);
        if (today < MinimumDate || today > MaximumDate || BlockedDates.Contains(today))
        {
            return false;
        }

        SetActiveDate(today);
        DisplayMonth = StartOfMonth(today);
        return true;
    }

    /// <summary>Gets or sets the displayed Gregorian month; supplied dates normalize to day one.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public DateOnly DisplayMonth
    {
        get => _displayMonth;
        set => _ = SetProperty(ref _displayMonth, StartOfMonth(value), InvalidationImpact.Render);
    }

    #endregion

    #region Selection and owned collections

    /// <summary>Verifies owner access before an owned calendar collection mutates.</summary>
    internal void VerifyCalendarMutable() => VerifyMutable();

    /// <summary>Invalidates rendered date state after an owned blocked-range mutation.</summary>
    internal void OnBlockedDatesChanged()
    {
        if (_selection is { } selection && BlockedDates.Intersects(selection))
        {
            _ = CommitSelection(null);
        }

        if (_intervalAnchor is { } anchor && BlockedDates.Contains(anchor))
        {
            SetIntervalAnchor(null);
        }

        RepairActiveDate();

        Invalidate(InvalidationImpact.Render);
    }

    /// <summary>Activates one selectable date through the current selection mode.</summary>
    /// <param name="date">The date targeted by keyboard or pointer input.</param>
    /// <returns>True when the date changed pending or committed calendar state.</returns>
    internal bool ActivateDate(DateOnly date)
    {
        VerifyMutable();

        if (BlockedDates.Contains(date))
        {
            return false;
        }

        SetActiveDate(date);

        if (SelectionMode == CalendarSelectionMode.Select)
        {
            return CommitSelection(new DateInterval(date, date));
        }

        if (_intervalAnchor is not { } anchor)
        {
            _ = CommitSelection(null);
            SetIntervalAnchor(date);
            return true;
        }

        var interval = date < anchor
            ? new DateInterval(date, anchor)
            : new DateInterval(anchor, date);

        if (BlockedDates.Intersects(interval))
        {
            return false;
        }

        SetIntervalAnchor(null);
        return CommitSelection(interval);
    }

    #endregion

    #region Lifecycle and layout

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(_contentWidth, _contentHeight);
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
            HandleKey(key);
        }
        else if (eventArgs is PointerEventArgs pointer)
        {
            HandlePointer(pointer);
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        _hoveredDate = null;

        if (reason == ReleaseReason.Disposed)
        {
            SelectionChanged = null;
        }
    }

    #endregion

    #region Input

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
            Code.Left => MoveByDays(-1),
            Code.Right => MoveByDays(1),
            Code.Up => MoveByDays(-7),
            Code.Down => MoveByDays(7),
            Code.Home => MoveToWeekEdge(end: false),
            Code.End => MoveToWeekEdge(end: true),
            Code.PageUp => MoveByMonth(-1),
            Code.PageDown => MoveByMonth(1),
            Code.Enter => ActivateDate(ActiveDate),
            Code.Character when stroke.Character == new Rune(' ') => ActivateDate(ActiveDate),
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

        if (pointer.Action == PointerAction.Leave)
        {
            SetHoveredDate(null);
            return;
        }

        if (pointer.Action == PointerAction.Wheel)
        {
            if (pointer.WheelY != 0)
            {
                eventArgs.Handled = MoveDisplayMonth(pointer.WheelY > 0 ? -1 : 1);
            }

            return;
        }

        if (pointer.Cells is not { } cells)
        {
            return;
        }

        var date = DateAt(cells);
        SetHoveredDate(date is { } hovered && IsSelectable(hovered) ? hovered : null);

        if (pointer.Action != PointerAction.Press ||
            (pointer.Buttons & Buttons.Primary) == 0 ||
            !ContentBounds.Contains(cells))
        {
            return;
        }

        _ = RequestFocus();

        if (cells.Y == ContentBounds.Y && cells.X == ContentBounds.X)
        {
            eventArgs.Handled = MoveDisplayMonth(-1);
            return;
        }

        if (cells.Y == ContentBounds.Y && cells.X == ContentBounds.Right - 1)
        {
            eventArgs.Handled = MoveDisplayMonth(1);
            return;
        }

        if (date is { } pressed)
        {
            eventArgs.Handled = true;

            if (IsSelectable(pressed))
            {
                _ = ActivateDate(pressed);
            }
        }
    }

    private bool MoveByDays(int delta)
    {
        var candidate = (long) ActiveDate.DayNumber + delta;
        var direction = Math.Sign(delta);
        return MoveToSelectable(candidate, direction);
    }

    private bool MoveToWeekEdge(bool end)
    {
        var first = (int) FirstDayOfWeek;
        var offset = ((int) ActiveDate.DayOfWeek - first + _columnCount) % _columnCount;
        var weekStart = (long) ActiveDate.DayNumber - offset;
        var weekEnd = weekStart + _columnCount - 1;
        return end
            ? MoveToSelectable(weekEnd, -1, weekStart, weekEnd)
            : MoveToSelectable(weekStart, 1, weekStart, weekEnd);
    }

    private bool MoveByMonth(int delta)
    {
        var monthIndex = ((ActiveDate.Year - 1) * 12L) + ActiveDate.Month - 1 + delta;

        if (monthIndex is < 0 or >= 119988)
        {
            return false;
        }

        var year = (int) (monthIndex / 12) + 1;
        var month = (int) (monthIndex % 12) + 1;
        var day = Math.Min(ActiveDate.Day, DateTime.DaysInMonth(year, month));
        return MoveToSelectable(new DateOnly(year, month, day).DayNumber, Math.Sign(delta));
    }

    private bool MoveDisplayMonth(int delta)
    {
        var monthIndex = ((DisplayMonth.Year - 1) * 12L) + DisplayMonth.Month - 1 + delta;

        if (monthIndex is < 0 or >= 119988)
        {
            return false;
        }

        var year = (int) (monthIndex / 12) + 1;
        var month = (int) (monthIndex % 12) + 1;
        var first = new DateOnly(year, month, 1);
        var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        if (last < MinimumDate || first > MaximumDate)
        {
            return false;
        }

        DisplayMonth = first;
        return true;
    }

    private bool MoveToSelectable(
        long candidate,
        int direction,
        long? minimum = null,
        long? maximum = null)
    {
        Debug.Assert(direction is -1 or 1, "Calendar movement has a direction.");
        var lower = Math.Max(MinimumDate.DayNumber, minimum ?? DateOnly.MinValue.DayNumber);
        var upper = Math.Min(MaximumDate.DayNumber, maximum ?? DateOnly.MaxValue.DayNumber);

        while (candidate >= lower && candidate <= upper)
        {
            var date = DateOnly.FromDayNumber((int) candidate);

            if (!BlockedDates.Contains(date))
            {
                if (date == ActiveDate)
                {
                    return false;
                }

                SetActiveDate(date);
                return true;
            }

            candidate += direction;
        }

        return false;
    }

    private DateOnly? DateAt(Point point)
    {
        var bounds = ContentBounds;

        if (!bounds.Contains(point))
        {
            return null;
        }

        var row = point.Y - bounds.Y - _dateGridOffset;

        if (row is < 0 or >= _weekCount)
        {
            return null;
        }

        var column = (point.X - bounds.X) / _cellWidth;
        var dayNumber = GridStartDayNumber() + (row * _columnCount) + column;

        return dayNumber < DateOnly.MinValue.DayNumber || dayNumber > DateOnly.MaxValue.DayNumber
            ? null
            : DateOnly.FromDayNumber(dayNumber);
    }

    private int GridStartDayNumber()
    {
        var firstDay = (int) FirstDayOfWeek;
        var offset = ((int) DisplayMonth.DayOfWeek - firstDay + _columnCount) % _columnCount;
        return DisplayMonth.DayNumber - offset;
    }

    private bool IsSelectable(DateOnly date) =>
        date >= MinimumDate && date <= MaximumDate && !BlockedDates.Contains(date);

    private void SetHoveredDate(DateOnly? value) =>
        _ = SetProperty(ref _hoveredDate, value, InvalidationImpact.Render, nameof(HoveredDate));

    #endregion

    #region Rendering

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var bounds = ContentBounds;

        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return;
        }

        DrawHeader(canvas, bounds);
        DrawWeekdays(canvas, bounds);
        DrawDates(canvas, bounds);
    }

    private void DrawHeader(TerminalCanvas canvas, Rect bounds)
    {
        var row = canvas.Clip(new Rect(bounds.X, bounds.Y, bounds.Width, Math.Min(_headerHeight, bounds.Height)));
        var style = ResolvedStyle.WithForeground(Theme?.ResolveColor(ThemeColor.Accent) ?? Color.Default);
        row.DrawRune(new Rune('<'), new Point(bounds.X, bounds.Y), style);

        if (bounds.Width > _headerHeight)
        {
            row.DrawRune(new Rune('>'), new Point(bounds.Right - 1, bounds.Y), style);
        }

        if (bounds.Width > _minimumHeaderWidth)
        {
            var titleBounds = new Rect(bounds.X + _headerHeight, bounds.Y, bounds.Width - _minimumHeaderWidth, _headerHeight);
            DrawCentered(row, titleBounds, DisplayMonth.ToString("MMMM yyyy", Culture), ResolvedStyle);
        }
    }

    private void DrawWeekdays(TerminalCanvas canvas, Rect bounds)
    {
        if (bounds.Height < _dateGridOffset)
        {
            return;
        }

        var first = (int) FirstDayOfWeek;
        var style = ResolvedStyle.WithForeground(ResolveColor(ActualStyle.WeekdayHeaderColor));
        Span<SharpVision.Text.Line> lines = stackalloc SharpVision.Text.Line[1];

        for (var column = 0; column < _columnCount; column++)
        {
            var day = (DayOfWeek) ((first + column) % _columnCount);
            var label = Culture.DateTimeFormat.GetAbbreviatedDayName(day);
            var cell = new Rect(bounds.X + (column * _cellWidth), bounds.Y + _weekdayRowOffset, _cellWidth, _headerHeight);
            _ = SharpVision.Text.Layout.Format(
                label,
                2,
                SharpVision.Text.Overflow.Clip,
                SharpVision.Text.Alignment.Start,
                CellPolicy.AmbiguousWidth,
                lines);
            var line = lines[0];
            _ = canvas.Clip(cell).Draw(
                label.AsSpan(line.Offset, line.Length),
                new Point(cell.X + 1, cell.Y),
                style);
        }
    }

    private void DrawDates(TerminalCanvas canvas, Rect bounds)
    {
        if (bounds.Height < 3)
        {
            return;
        }

        var firstDayNumber = GridStartDayNumber();
        var maximumDayNumber = DateOnly.MaxValue.DayNumber;

        for (var week = 0; week < _weekCount; week++)
        {
            var y = bounds.Y + _dateGridOffset + week;

            if (y >= bounds.Bottom)
            {
                return;
            }

            for (var column = 0; column < _columnCount; column++)
            {
                var dayNumber = firstDayNumber + (week * _columnCount) + column;

                if (dayNumber < DateOnly.MinValue.DayNumber || dayNumber > maximumDayNumber)
                {
                    continue;
                }

                var date = DateOnly.FromDayNumber(dayNumber);
                var cell = new Rect(bounds.X + (column * _cellWidth), y, _cellWidth, 1);
                var style = ResolveDateStyle(date);
                canvas.Clear(cell, style);

                if (_markup.TryGetValue(date, out var markup))
                {
                    DrawMarkup(canvas, cell, date, markup);
                }
                else
                {
                    var face = $" {date.Day,2} ";
                    _ = canvas.Clip(cell).Draw(face, new Point(cell.X, cell.Y), style);
                }
            }
        }
    }

    private void DrawMarkup(
        TerminalCanvas canvas,
        Rect cell,
        DateOnly date,
        CalendarDateMarkup markup)
    {
        var cells = 0;

        foreach (var grapheme in Graphemes.Enumerate(markup.Display))
        {
            var cluster = markup.Display.AsSpan(grapheme.Offset, grapheme.Length);
            var width = Terminal.Unicode.Width.Measure(cluster, CellPolicy.AmbiguousWidth).Cells;

            if (cells + width > cell.Width)
            {
                break;
            }

            var span = markup.SpanAt(grapheme.Offset);
            var style = ResolveDateStyle(date, span);
            _ = canvas.Clip(cell).Draw(
                cluster,
                new Point(cell.X + cells, cell.Y),
                style,
                background: span?.Background.HasValue == true
                    ? BackgroundMode.Opaque
                    : BackgroundMode.Transparent);
            cells += width;
        }
    }

    private TerminalStyle ResolveDateStyle(DateOnly date, SharpVision.Text.StyleSpan? markup = null)
    {
        var style = markup is { } span ? ResolveMarkupStyle(span) : ResolvedStyle;
        var adjacent = date.Year != DisplayMonth.Year || date.Month != DisplayMonth.Month;
        var actualStyle = ActualStyle;

        if (adjacent)
        {
            style = style.WithForeground(ResolveColor(actualStyle.OutOfMonthDayColor));
        }

        if (_hoveredDate == date)
        {
            style = WithColors(
                style,
                ResolveColor(actualStyle.TodayMarkerColor),
                Theme?.ResolveColor(ThemeColor.ActiveControl) ?? Color.Default);
        }

        if (_intervalAnchor is { } anchor)
        {
            var target = _hoveredDate ?? ActiveDate;
            var preview = target < anchor
                ? new DateInterval(target, anchor)
                : new DateInterval(anchor, target);

            if (preview.Contains(date))
            {
                style = WithColors(
                    style,
                    ResolveColor(actualStyle.TodayMarkerColor),
                    Theme?.ResolveColor(ThemeColor.ActiveControl) ?? Color.Default);
            }
        }

        if (_selection is { } selection && selection.Contains(date))
        {
            style = WithColors(
                style,
                ResolveColor(actualStyle.SelectedDayColor),
                Theme?.ResolveColor(ThemeColor.SelectedControl) ?? Color.Default);
        }

        if (!EffectiveIsEnabled || BlockedDates.Contains(date) || date < MinimumDate || date > MaximumDate)
        {
            style = WithColors(
                style,
                ResolveColor(actualStyle.DisabledDayColor),
                Theme?.ResolveColor(ThemeColor.DisabledControl) ?? Color.Default);
        }

        if (IsFocused && date == ActiveDate)
        {
            style = style.Underline == Underline.None
                ? WithAttributes(style, style.Attributes | TerminalAttributes.Underline)
                : style;
        }

        return style;
    }

    private TerminalStyle ResolveMarkupStyle(SharpVision.Text.StyleSpan span)
    {
        var inherited = ResolvedStyle;
        var attributes = inherited.Attributes;

        if ((span.Attributes & _blinkAttributes) != 0)
        {
            attributes &= ~_blinkAttributes;
        }

        attributes |= span.Attributes;
        Underline? underline = null;

        if (span.Underline != Underline.None)
        {
            attributes &= ~TerminalAttributes.Underline;
            underline = span.Underline;
        }

        var underlineColor = span.UnderlineColor is { } configuredUnderlineColor
            ? ResolveThemeColor(configuredUnderlineColor)
            : (Color?) null;
        var (resolvedAttributes, resolvedUnderline, resolvedUnderlineColor) = DecorationResolver.Resolve(
            inherited,
            attributes,
            underline,
            underlineColor);
        return new TerminalStyle(
            span.Foreground is { } configuredForeground
                ? ResolveThemeColor(configuredForeground)
                : inherited.Foreground,
            span.Background is { } configuredBackground
                ? ResolveThemeColor(configuredBackground)
                : inherited.Background,
            resolvedAttributes,
            span.Link ?? inherited.Hyperlink,
            resolvedUnderline,
            resolvedUnderlineColor);
    }

    private void DrawCentered(TerminalCanvas canvas, Rect bounds, string value, TerminalStyle style)
    {
        Span<SharpVision.Text.Line> lines = stackalloc SharpVision.Text.Line[1];
        _ = SharpVision.Text.Layout.Format(
            value,
            bounds.Width,
            SharpVision.Text.Overflow.Clip,
            SharpVision.Text.Alignment.Center,
            CellPolicy.AmbiguousWidth,
            lines);
        var line = lines[0];
        _ = canvas.Clip(bounds).Draw(
            value.AsSpan(line.Offset, line.Length),
            new Point(bounds.X + line.Leading, bounds.Y),
            style);
    }

    private static TerminalStyle WithColors(TerminalStyle source, Color foreground, Color background) => new(
        foreground,
        background,
        source.Attributes,
        source.Hyperlink,
        source.Underline,
        source.UnderlineColor);

    private static TerminalStyle WithAttributes(TerminalStyle source, TerminalAttributes attributes) => new(
        source.Foreground,
        source.Background,
        attributes,
        source.Hyperlink,
        source.Underline,
        source.UnderlineColor);

    #endregion

    #region State repair

    private bool CommitSelection(DateInterval? value)
    {
        if (value is { } interval)
        {
            if (SelectionMode == CalendarSelectionMode.Select && interval.Days != 1)
            {
                throw new ArgumentException("Select mode accepts exactly one date.", nameof(value));
            }

            if (BlockedDates.Intersects(interval))
            {
                throw new ArgumentException("The selection contains a blocked date.", nameof(value));
            }

            if (interval.Start < MinimumDate || interval.End > MaximumDate)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The selection must be inside the calendar bounds.");
            }

            SetActiveDate(interval.Start);
        }

        SetIntervalAnchor(null);
        var previous = _selection;

        if (!SetProperty(ref _selection, value, InvalidationImpact.Render, nameof(Selection)))
        {
            return false;
        }

        SelectionChanged?.Invoke(this, new CalendarSelectionChangedEventArgs(previous, value));
        return true;
    }

    private void SetActiveDate(DateOnly value)
    {
        _ = SetProperty(ref _activeDate, value, InvalidationImpact.Render, nameof(ActiveDate));
        DisplayMonth = value;
    }

    private void SetIntervalAnchor(DateOnly? value) =>
        _ = SetProperty(ref _intervalAnchor, value, InvalidationImpact.Render, nameof(IntervalAnchor));

    private void RepairState()
    {
        if (_selection is { } selection &&
            (selection.Start < MinimumDate || selection.End > MaximumDate))
        {
            _ = CommitSelection(null);
        }

        if (_intervalAnchor is { } anchor && (anchor < MinimumDate || anchor > MaximumDate))
        {
            SetIntervalAnchor(null);
        }

        RepairActiveDate();
    }

    private void RepairActiveDate()
    {
        var clamped = DateOnly.FromDayNumber(Math.Clamp(
            ActiveDate.DayNumber,
            MinimumDate.DayNumber,
            MaximumDate.DayNumber));

        if (!BlockedDates.Contains(clamped))
        {
            SetActiveDate(clamped);
            return;
        }

        var forward = clamped.DayNumber;
        var backward = clamped.DayNumber;

        while (forward < MaximumDate.DayNumber || backward > MinimumDate.DayNumber)
        {
            if (forward < MaximumDate.DayNumber)
            {
                forward++;

                if (!BlockedDates.Contains(DateOnly.FromDayNumber(forward)))
                {
                    SetActiveDate(DateOnly.FromDayNumber(forward));
                    return;
                }
            }

            if (backward > MinimumDate.DayNumber)
            {
                backward--;

                if (!BlockedDates.Contains(DateOnly.FromDayNumber(backward)))
                {
                    SetActiveDate(DateOnly.FromDayNumber(backward));
                    return;
                }
            }
        }
    }

    private static DateOnly StartOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    #endregion
}
