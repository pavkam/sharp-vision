// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Runtime.ExceptionServices;

using Popups;

using SharpVision.Terminal.Input;

/// <summary>Displays a formatted date with inline segment editing and a Calendar popup for selection.</summary>
[PublicAPI]
public sealed class DateInput: InputBase
{
    // The indicator cell (InputBase.DropDownIndicatorWidth) plus its one-cell separating gap.
    private const int _indicatorReservedWidth = 2;

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
    private readonly Popup _popup;
    private readonly SegmentFieldBehavior _segments;
    private readonly SegmentFieldKeyOptions _segmentKeyOptions;
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
            VerifyMutable,
            NotifyPropertyChanged,
            () => DateOnly.FromDateTime(TimeProvider.GetLocalNow().DateTime),
            RaiseValueChanged,
            SynchronizeCalendarValue,
            SyncCalendarBounds);
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
        EnablePressActivation();
        _segments = EnableSegmentEditing(
            BuildSegments,
            ApplySegmentDigit,
            ApplySegmentIncrement,
            ClearSegmentValue);
        _segmentKeyOptions = new SegmentFieldKeyOptions(
            ResolveStepDelta,
            ClearValueCommand,
            handlePopupCommand: HandlePopupCommand,
            handleRecognizedWithoutChange: true);
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
                Format, value, nameof(value), "DateOnly", static (f, c) => _probeDate.ToString(f, c));

            _ = SetPropertyAndSynchronize(
                ref _culture,
                value,
                InvalidationImpact.Measure,
                () => _calendarDropDown.SyncCulture(Culture),
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
                value, _culture, nameof(value), "DateOnly", static (f, c) => _probeDate.ToString(f, c));
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
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
    } = 10;

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
        _ = MeasureChild(_popup, new Constraint(constraint.Width, DropDownHeight.Add(1)));
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());
        var width = MeasureCells(FormatValue())
            .Add(_indicatorReservedWidth)
            .Add(affixes.StartCells)
            .Add(affixes.EndCells);
        return new Size(width, 1);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) =>
        ArrangeChild(_popup, RootBounds(bounds), ResolvedAxes.Both);

    /// <summary>Resolves the box editable segment text is drawn into: the content box with the
    /// drop-down indicator's own reserved columns subtracted first, then deflated for any active
    /// <see cref="StartAffix"/>/<see cref="EndAffix"/> - keeping both affixes strictly inboard of
    /// the indicator, and never overlapping it.</summary>
    private Rect ResolveTextBox()
    {
        var content = ContentBounds;
        var fieldBox = new Rect(content.X, content.Y, Math.Max(0, content.Width - _indicatorReservedWidth), 1);
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());
        return DeflateForAffixes(fieldBox, affixes);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        EnsureSeeded();
        var content = ContentBounds;
        var style = ResolvedStyle;
        var fieldBox = new Rect(content.X, content.Y, Math.Max(0, content.Width - _indicatorReservedWidth), 1);
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());
        RenderAffixes(canvas, fieldBox, affixes, StartAffix, EndAffix, style);
        var textBox = DeflateForAffixes(fieldBox, affixes);
        var segments = BuildSegments();
        var isPlaceholder = _state.Value is null;
        var x = textBox.X;
        var editableIndex = -1;

        for (var index = 0; index < segments.Length && x < textBox.Right; index++)
        {
            var segment = segments[index];

            if (segment.IsEditable)
            {
                editableIndex++;
            }

            var segmentStyle = IsFocused && !IsOpen && segment.IsEditable && editableIndex == _segments.ActiveSegment
                ? SegmentHighlightStyle(style)
                : isPlaceholder
                    ? PlaceholderStyle(style)
                    : style;
            var clipped = canvas.Clip(new Rect(x, content.Y, Math.Max(0, textBox.Right - x), 1));
            _ = clipped.Draw(
                segment.Text.AsSpan(),
                new Point(x, content.Y),
                segmentStyle,
                background: BackgroundMode.Transparent);
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

        if (!IsOpen && eventArgs is KeyEventArgs keyEventArgs)
        {
            _segments.HandleKey(keyEventArgs, _segmentKeyOptions);

            if (keyEventArgs.IsHandled)
            {
                return;
            }
        }

        if (!IsOpen && eventArgs is PointerEventArgs pointer)
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

        if (focused)
        {
            _segments.ActivateFirstSegment();
        }
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

    private bool ClearValueCommand()
    {
        if (!AllowNull)
        {
            return false;
        }

        Value = null;
        return true;
    }

    private bool? HandlePopupCommand(KeyEventArgs eventArgs)
    {
        var stroke = eventArgs.Stroke;

        if (!eventArgs.IsInitialKeyDown)
        {
            return null;
        }

        var isAltDownGesture = stroke.Code == Code.Down &&
            (stroke.Modifiers & Modifiers.Alt) != 0;
        var isF4Gesture = stroke.Code == Code.F4;

        if (!isAltDownGesture && !isF4Gesture)
        {
            return null;
        }

        var admitted = isAltDownGesture
            ? KeyboardModifierPolicy.MatchesCommand(stroke.Modifiers, Modifiers.Alt)
            : KeyboardModifierPolicy.MatchesCommand(stroke.Modifiers, Modifiers.None);

        if (admitted)
        {
            IsOpen = true;
        }

        return admitted;
    }

    private bool ApplySegmentIncrement(TemporalSegmentKind kind, int delta)
    {
        if (_state.Value is not { } date)
        {
            return false;
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
            return CommitSegmentValue(ClampDate(new DateOnly(replacedYear, replacedMonth, replacedDay)));
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

            return CommitSegmentValue(ClampDate(adjusted));
        }
        catch (ArgumentOutOfRangeException)
        {
            // Silently ignore increments that push beyond DateOnly bounds.
            return false;
        }
    }

    private bool ApplySegmentDigit(TemporalSegmentKind kind, int value)
    {
        if (_state.Value is not { } date)
        {
            return false;
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

            return CommitSegmentValue(ClampDate(new DateOnly(year, month, day)));
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private bool ClearSegmentValue(TemporalSegmentKind kind)
    {
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

            return CommitSegmentValue(ClampDate(new DateOnly(year, month, day)));
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

    private SegmentDescriptor[] BuildSegments()
    {
        var pattern = ResolveDatePattern();
        var tokens = TemporalPatternSegmenter.ParseTokens(pattern, _tokenKinds, _culture);

        IReadOnlyList<string> text;

        if (_state.Value is { } date)
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

            descriptors[index] = token.Kind is not { } kind
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

    private string FormatValue()
    {
        var builder = new StringBuilder();

        foreach (var segment in BuildSegments())
        {
            _ = builder.Append(segment.Text);
        }

        return builder.ToString();
    }

    [Pure]
    private static TerminalStyle SegmentHighlightStyle(TerminalStyle source) => new(
        source.Foreground,
        source.Background,
        source.Attributes | TerminalAttributes.Reverse,
        source.Hyperlink,
        source.Underline,
        source.UnderlineColor);

    [Pure]
    private static TerminalStyle PlaceholderStyle(TerminalStyle source) => new(
        source.Foreground,
        source.Background,
        source.Attributes | TerminalAttributes.Dim,
        source.Hyperlink,
        source.Underline,
        source.UnderlineColor);

    #endregion

    #region Value management

    /// <summary>Latches Value to the current local date on first read, so a control mounted under
    /// a dispatcher observes that dispatcher's clock instead of the clock current at
    /// construction, and pushes the newly resolved value into the owned Calendar. A value already
    /// committed - including an explicit null under <see cref="AllowNull"/> - is left
    /// untouched.</summary>
    private void EnsureSeeded() => _ = _state.EnsureSeeded();

    [Pure]
    private DateOnly ClampDate(DateOnly date) => _state.Clamp(date);

    private void SyncCalendarBounds() => _calendarDropDown.SyncBounds();

    private void SynchronizeCalendarValue(DateOnly? value) =>
        _calendarDropDown.SyncValue(value);

    private void RaiseValueChanged(DateOnly? previous, DateOnly? current)
    {
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(
            () => ValueChanged?.Invoke(this, new DateInputValueChangedEventArgs(previous, current)),
            ref failure);
        failure?.Throw();
    }

    #endregion

}
