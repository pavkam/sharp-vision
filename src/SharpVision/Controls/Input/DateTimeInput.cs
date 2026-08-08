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
public sealed class DateTimeInput: PressInteractionBase
{
    /// <inheritdoc/>
    protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
        (theme ?? ThemeCatalog.Dark).GetStyleSet(InputStyle.Default).ToAppearanceStates();

    // Terminal field geometry: one content row, two border columns, and one indicator cell.
    private const int _fieldContentHeight = 1;
    private const int _fieldBorderWidth = 2;
    private const int _indicatorWidth = 1;
    private const int _defaultDropDownHeight = 10;

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

    private readonly Calendar _calendar;
    private readonly Popup _popup;
    private readonly OwnedControlSlot _popupSlot;
    private readonly PopupModalTracker _modalTracker;
    private readonly SegmentFieldBehavior _segments;

    private DateTime? _value;
    private bool _seeded;
    private bool _synchronizingCalendar;
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
            ConnectsToAnchor = true,
            // DateTimeInput re-arranges its own popup child from its own ArrangeOverride every
            // pass (RootBounds(bounds) below), so base Popup's anchor-reflow tracking would be a
            // redundant second placement pass reacting to the same self-owned anchor.
            TracksAnchorReflow = false
        };
        _popup.Opened += OnPopupOpened;
        _popup.Closing += OnPopupClosing;
        _popup.Closed += OnPopupClosed;
        _modalTracker = new PopupModalTracker(_popup, () => Opened = false);

        // Register event handler after _popup is created to avoid NullReferenceException
        // when setting _calendar.Selection fires SelectionChanged → Opened accessor.
        _calendar.SelectionChanged += OnCalendarSelectionChanged;

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
        _segments = new SegmentFieldBehavior(
            BuildSegments,
            ApplyDigitValue,
            IncrementSegmentValue,
            ClearSegmentValue,
            () => Invalidate(InvalidationImpact.Render));
        TabNavigation = TabNavigation.None;
    }

    /// <summary>Raised after a committed value transition.</summary>
    public event EventHandler<DateTimeInputValueChangedEventArgs>? ValueChanged;

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
            EnsureSeeded();
            return _value;
        }
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
            // The eager re-seed only runs once seeding has already happened: resolving "now"
            // here for a not-yet-seeded control would latch whatever clock is current at this
            // call (often the construction-time wall clock, before a dispatcher with its own
            // TimeProvider is attached) instead of the correct one. Leaving _value untouched
            // when unseeded is safe - EnsureSeeded resolves a non-null value from the right
            // clock before Value (or any other observer) can ever read _value as null.
            if (SetProperty(ref field, value, InvalidationImpact.None) && !value && _seeded && _value is null)
            {
                _ = Commit(ClampToRange(TimeProvider.GetLocalNow().DateTime));
            }
        }
    } = true;

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
                ValidateDateTimeFormat(format, value, nameof(value));
            }

            if (SetProperty(ref _culture, value, InvalidationImpact.Measure))
            {
                _calendar.Culture = value;
                _segments.ClampActiveSegment();
                _segments.ResetDigitBuffer();
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
    /// designator token for correct 12-hour clamping, since a 12-hour hour token without a
    /// designator token is treated as a 24-hour segment for editing purposes.
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
                ValidateDateTimeFormat(value, _culture, nameof(value));
            }

            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                _segments.ClampActiveSegment();
                _segments.ResetDigitBuffer();
            }
        }
    }

    /// <summary>Validates that a format pattern is renderable by <see cref="DateTime"/> under a
    /// culture, so an invalid pattern is rejected at the property boundary instead of throwing
    /// later from the layout pass.</summary>
    private static void ValidateDateTimeFormat(string format, CultureInfo culture, string paramName)
    {
        try
        {
            _ = DateTime.MinValue.ToString(format, culture);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
        {
            throw new ArgumentException(
                $"The format \"{format}\" cannot be rendered by a DateTime value.",
                paramName,
                exception);
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
    /// <exception cref="ArgumentException">The minimum exceeds <see cref="Maximum"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public DateTime Minimum
    {
        get;
        set
        {
            VerifyMutable();
            value.ThrowIfAboveMaximum(Maximum, nameof(value), "Minimum cannot exceed Maximum.");

            if (SetProperty(ref field, value, InvalidationImpact.Render))
            {
                SyncCalendarBounds();
                ClampCurrentValue();
            }
        }
    } = DateTime.MinValue;

    /// <summary>Gets or sets the inclusive upper bound for the value. Default is <see cref="DateTime.MaxValue"/>.</summary>
    /// <exception cref="ArgumentException">The maximum is below <see cref="Minimum"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public DateTime Maximum
    {
        get;
        set
        {
            VerifyMutable();
            value.ThrowIfBelowMinimum(Minimum, nameof(value), "Maximum cannot be less than Minimum.");

            if (SetProperty(ref field, value, InvalidationImpact.Render))
            {
                SyncCalendarBounds();
                ClampCurrentValue();
            }
        }
    } = DateTime.MaxValue;

    /// <summary>Gets or sets the maximum visible calendar height in terminal cells.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int DropDownHeight
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = _defaultDropDownHeight;

    /// <summary>Gets or sets whether the private calendar popup owns a dismissing modal plane rooted at this field.</summary>
    /// <exception cref="ArgumentException">The attached control is not an eligible modal root.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    /// <exception cref="Exception">A focus, scope, pointer-cleanup, or user callback fails after committed cleanup.</exception>
    public bool Opened
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
        get => _calendar.Style;
        set
        {
            VerifyMutable();

            if (_calendar.Style == value)
            {
                return;
            }

            _calendar.Style = value;
            NotifyPropertyChanged(nameof(CalendarStyle), InvalidationImpact.None);
        }
    }

    /// <summary>Gets the resolved presentation of the owned Calendar.</summary>
    public CalendarStyle ActualCalendarStyle => _calendar.ActualStyle;

    #endregion

    #region Input, layout, and rendering

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        EnsureSeeded();
        _ = MeasureChild(_popup, new Constraint(constraint.Width, DropDownHeight));
        var width = _fieldBorderWidth;

        foreach (var segment in BuildSegments())
        {
            width += MeasureCells(segment.Text);
        }

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

        EnsureSeeded();
        var style = ResolvedStyle;
        var highlight = new TerminalStyle(
            style.Foreground,
            style.Background,
            style.Attributes | TerminalAttributes.Reverse);
        var canHighlight = Focused;
        var x = content.X;
        var editableIndex = -1;

        foreach (var segment in BuildSegments())
        {
            if (segment.Editable)
            {
                editableIndex++;
            }

            var segmentStyle = canHighlight && segment.Editable && editableIndex == _segments.ActiveSegment
                ? highlight
                : style;
            _ = canvas.Draw(segment.Text.AsSpan(), new Point(x, content.Y), segmentStyle, background: BackgroundMode.Transparent);
            x += MeasureCells(segment.Text);
        }

        var themed = ControlGlyphs.Disclosure.DropDown;
        var glyph = ResolveDropDownGlyph(themed.Fallback);
        canvas.DrawRune(
            glyph,
            new Point(Math.Max(content.X, content.Right - _indicatorWidth), content.Y),
            style,
            BackgroundMode.Transparent);
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

        if (Opened && eventArgs is KeyEventArgs { Stroke: { Action: KeyAction.Press } stroke })
        {
            if (stroke.Code == Code.Escape)
            {
                Opened = false;
                eventArgs.Handled = true;
                return;
            }

            if (stroke.Code == Code.Tab)
            {
                Opened = false;
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

        if (eventArgs is KeyEventArgs key && !Opened)
        {
            HandleKey(key);

            if (key.Handled)
            {
                return;
            }
        }

        if (eventArgs is PointerEventArgs pointer && !Opened)
        {
            HandlePointer(pointer);

            if (pointer.Handled)
            {
                return;
            }
        }

        if (!eventArgs.Handled)
        {
            Interaction.Handle(eventArgs);
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

        if (!focused)
        {
            _segments.ResetDigitBuffer();
        }

        Invalidate(InvalidationImpact.Render);
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

        if (reason == ReleaseReason.Disposed)
        {
            _calendar.SelectionChanged -= OnCalendarSelectionChanged;
            _popup.Opened -= OnPopupOpened;
            _popup.Closing -= OnPopupClosing;
            _popup.Closed -= OnPopupClosed;
            ValueChanged = null;
            DropDownOpened = null;
            DropDownClosed = null;
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
            Opened = true;
            eventArgs.Handled = true;
            return;
        }

#pragma warning disable IDE0072 // Unknown or unsupported keys intentionally remain unhandled.
        var handled = stroke.Code switch
        {
            Code.Left => _segments.MoveSegment(-1, wrap: true),
            Code.Right => _segments.MoveSegment(1, wrap: true),
            Code.Up => _segments.Increment(1),
            Code.Down => _segments.Increment(-1),
            Code.Home => _segments.MoveToEdge(first: true),
            Code.End => _segments.MoveToEdge(first: false),
            Code.Delete => ClearValue(),
            Code.Backspace => _segments.ClearActiveSegment(),
            Code.Character when stroke.Character is { } ch && IsDigit(ch) => _segments.TypeDigit(ch.Value - '0'),
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
        _ = _segments.ActivateSegmentAtColumn(localX, CellPolicy.AmbiguousWidth);

        if (!Focused)
        {
            _ = RequestFocus();
        }

        eventArgs.Handled = true;
    }

    private bool ToggleAmPm()
    {
        if (!_value.HasValue)
        {
            return false;
        }

        var index = FindEditableIndex(TemporalSegmentKind.AmPmDesignator);

        if (index < 0)
        {
            return false;
        }

        _segments.ActivateSegment(index);
        return _segments.Increment(1);
    }

    private int FindEditableIndex(TemporalSegmentKind kind)
    {
        var editableIndex = -1;

        foreach (var segment in BuildSegments())
        {
            if (!segment.Editable)
            {
                continue;
            }

            editableIndex++;

            if (segment.Kind == kind)
            {
                return editableIndex;
            }
        }

        return -1;
    }

    /// <summary>Gets whether the current layout - whether derived from <see cref="Use24HourFormat"/>
    /// or overridden by <see cref="Format"/> - includes an AM/PM designator segment, used as the
    /// effective 12-versus-24-hour policy for editing the hour segment.</summary>
    private bool HasAmPmDesignator => FindEditableIndex(TemporalSegmentKind.AmPmDesignator) >= 0;

    private bool ApplyDigitValue(TemporalSegmentKind kind, int value)
    {
        if (!_value.HasValue)
        {
            _ = Commit(ClampToRange(TimeProvider.GetLocalNow().DateTime));

            if (!_value.HasValue)
            {
                return false;
            }
        }

        var dt = _value.Value;
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
                    dt.Date.Add(new TimeSpan(
                        To24Hour(Math.Clamp(value, 1, 12), dt.Hour >= 12),
                        dt.Minute, dt.Second)),
                TemporalSegmentKind.Hour =>
                    dt.Date.Add(new TimeSpan(
                        Math.Clamp(value, 0, 23), dt.Minute, dt.Second)),
                TemporalSegmentKind.Minute =>
                    dt.Date.Add(new TimeSpan(
                        dt.Hour, Math.Clamp(value, 0, 59), dt.Second)),
                TemporalSegmentKind.Second =>
                    dt.Date.Add(new TimeSpan(
                        dt.Hour, dt.Minute, Math.Clamp(value, 0, 59))),
                _ => dt
            };
#pragma warning restore IDE0072

            return Commit(result);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private bool IncrementSegmentValue(TemporalSegmentKind kind, int delta)
    {
        if (!_value.HasValue)
        {
            return Commit(ClampToRange(TimeProvider.GetLocalNow().DateTime));
        }

        var dt = _value.Value;

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

        return Commit(result);
    }

    private bool ClearSegmentValue(TemporalSegmentKind kind)
    {
        if (!_value.HasValue)
        {
            return false;
        }

        var dt = _value.Value;

        try
        {
#pragma warning disable IDE0072 // AmPmDesignator is intentionally a no-op here.
            var result = kind switch
            {
                TemporalSegmentKind.Month => ReplaceMonth(dt, 1),
                TemporalSegmentKind.Day => ReplaceDay(dt, 1),
                TemporalSegmentKind.Year => ReplaceYear(dt, 1),
                TemporalSegmentKind.Hour => dt.Date.Add(new TimeSpan(0, dt.Minute, dt.Second)),
                TemporalSegmentKind.Minute => dt.Date.Add(new TimeSpan(dt.Hour, 0, dt.Second)),
                TemporalSegmentKind.Second => dt.Date.Add(new TimeSpan(dt.Hour, dt.Minute, 0)),
                _ => dt
            };
#pragma warning restore IDE0072

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
        EnsureSeeded();
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
            PushCalendarSelection(DateOnly.FromDateTime(clamped.Value));
        }

        ValueChanged?.Invoke(this, new DateTimeInputValueChangedEventArgs(previous, clamped));
        return true;
    }

    /// <summary>Latches Value to the current local date and time on first read, so a control
    /// mounted under a dispatcher observes that dispatcher's clock instead of the clock current
    /// at construction, and pushes the newly resolved value into the owned Calendar. A value
    /// already committed - including an explicit null under <see cref="AllowNull"/> - is left
    /// untouched.</summary>
    private void EnsureSeeded()
    {
        if (_seeded)
        {
            return;
        }

        _seeded = true;
        var now = ClampToRange(TimeProvider.GetLocalNow().DateTime);
        _value = now;
        PushCalendarSelection(DateOnly.FromDateTime(now));
    }

    private DateTime ClampToRange(DateTime dateTime) =>
        dateTime < Minimum ? Minimum
        : dateTime > Maximum ? Maximum
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
        _calendar.MinimumDate = Minimum > DateTime.MinValue
            ? DateOnly.FromDateTime(Minimum)
            : DateOnly.MinValue;

        _calendar.MaximumDate = Maximum < DateTime.MaxValue
            ? DateOnly.FromDateTime(Maximum)
            : DateOnly.MaxValue;
    }

    /// <summary>Pushes a date into the owned Calendar's selection under a re-entrancy guard.</summary>
    /// <remarks>
    /// Guards against OnCalendarSelectionChanged treating this programmatic push as a user pick:
    /// without it, setting _calendar.Selection below re-enters through the SelectionChanged
    /// event and triggers a redundant Commit plus Opened = false, converging today only by
    /// incidental value equality. Mirrors DateInput's SyncCalendar guard.
    /// </remarks>
    private void PushCalendarSelection(DateOnly date)
    {
        _synchronizingCalendar = true;

        try
        {
            _calendar.Selection = new DateInterval(date, date);
        }
        finally
        {
            _synchronizingCalendar = false;
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

        Opened = !Opened;
    }

    private void OpenDropDown()
    {
        EnsureSeeded();

        if (_value.HasValue)
        {
            var date = DateOnly.FromDateTime(_value.Value);
            _calendar.DisplayMonth = new DateOnly(date.Year, date.Month, 1);
            PushCalendarSelection(date);
        }

        _popup.IsOpen = true;
        _modalTracker.Enter(this);
        DropDownOpened?.Invoke(this, EventArgs.Empty);
    }

    private void CloseDropDown()
    {
        _modalTracker.Exit();
        _popup.IsOpen = false;
        DropDownClosed?.Invoke(this, EventArgs.Empty);
    }

    private void OnCalendarSelectionChanged(object? sender, CalendarSelectionChangedEventArgs eventArgs)
    {
        _ = sender;

        if (_synchronizingCalendar)
        {
            return;
        }

        if (eventArgs.Selection is not { } interval)
        {
            return;
        }

        var selectedDate = interval.Start;
        var timePart = _value?.TimeOfDay ?? TimeSpan.Zero;
        var kind = _value?.Kind ?? DateTimeKind.Unspecified;
        var combined = selectedDate.ToDateTime(TimeOnly.FromTimeSpan(timePart), kind);
        _ = Commit(combined);
        Opened = false;
    }

    private void OnPopupOpened(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        NotifyPropertyChanged(nameof(Opened), InvalidationImpact.None);
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
        NotifyPropertyChanged(nameof(Opened), InvalidationImpact.None);
    }

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

    private SegmentDescriptor[] BuildSegments()
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

        if (_value is { } dt)
        {
            text = TemporalPatternSegmenter.FormatSegments(
                pattern,
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

            descriptors[index] = token.Kind is not { } kind
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

#pragma warning disable IDE0072 // Every segment kind is individually handled.
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

    /// <summary>Resolves the disclosure chevron from the active theme's input style.</summary>
    /// <remarks>
    /// The chevron lives on <see cref="InputStyle"/> rather than on this control, so a theme
    /// targeting a terminal without dependable arrow coverage replaces it for every drop-down
    /// input at once instead of per instance. This control resolves appearance through
    /// <c>AppearanceStates</c>, which drops non-appearance members, so the style set is read
    /// directly.
    /// </remarks>
    /// <param name="fallback">The code-owned narrow-policy fallback.</param>
    /// <returns>The glyph to draw.</returns>
    private Rune ResolveDropDownGlyph(Rune fallback) =>
        (Theme ?? ThemeCatalog.Dark)
        .GetStyleSet(InputStyle.Default)
        .Normal.DropDownGlyph.Resolve(fallback, CellPolicy.AmbiguousWidth);

}
