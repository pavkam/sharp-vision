// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Runtime.ExceptionServices;

using Popups;

using SharpVision.Terminal.Input;

/// <summary>Displays a formatted date with inline segment editing and a Calendar popup for selection.</summary>
[PublicAPI]
public sealed class DateInput: PressInteractionBase
{
    /// <inheritdoc/>
    protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
        (theme ?? ThemeCatalog.Dark).GetStyleSet(InputStyle.Default).ToAppearanceStates();

    // Every supported calendar - including the narrowest, UmAlQuraCalendar
    // (max 2077-11-16) - can represent this date, unlike DateOnly.MaxValue.
    private static readonly DateOnly _probeDate = DateOnly.FromDateTime(DateTime.UnixEpoch);

    private static readonly IReadOnlyDictionary<char, TemporalSegmentKind> _tokenKinds =
        new Dictionary<char, TemporalSegmentKind>
        {
            ['M'] = TemporalSegmentKind.Month,
            ['d'] = TemporalSegmentKind.Day,
            ['y'] = TemporalSegmentKind.Year
        };

    private readonly Calendar _calendar;
    private readonly Popup _popup;
    private readonly OwnedControlSlot _popupSlot;
    private readonly PopupModalTracker _modalTracker;
    private readonly SegmentFieldBehavior _segments;
    private DateOnly? _value;
    private bool _seeded;
    private bool _synchronizingCalendar;
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
        _calendar = new Calendar
        {
            TabStop = false,
            SelectionMode = CalendarSelectionMode.Select
        };
        SyncCalendar();
        _calendar.SelectionChanged += OnCalendarSelectionChanged;
        _popup = new Popup
        {
            Anchor = this,
            Content = _calendar,
            FocusOnOpen = true,
            ModalBehavior = PopupModalBehavior.None,
            ConnectsToAnchor = true,
            Placement = PopupPlacement.Below,
            // DateInput re-arranges its own popup child from its own ArrangeOverride every pass
            // (RootBounds(bounds) below), so base Popup's anchor-reflow tracking would be a
            // redundant second placement pass reacting to the same self-owned anchor.
            TracksAnchorReflow = false
        };
        _popup.Opened += OnPopupOpened;
        _popup.Closing += OnPopupClosing;
        _popup.Closed += OnPopupClosed;
        _modalTracker = new PopupModalTracker(_popup, () => Opened = false);
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
            ApplySegmentDigit,
            ApplySegmentIncrement,
            ClearSegmentValue,
            () => Invalidate(InvalidationImpact.Render));
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
            EnsureSeeded();
            return _value;
        }
        set
        {
            VerifyMutable();
            _seeded = true;

            if (!value.HasValue && !AllowNull)
            {
                return;
            }

            if (value.HasValue)
            {
                value = ClampDate(value.Value);
            }

            if (_value == value)
            {
                return;
            }

            var previous = _value;
            _value = value;

            // Keeps an already-open popup showing the field's current value instead of a stale
            // selection and displayed month, matching how DateTimeInput's Commit re-pushes the
            // calendar's selection on every committed change regardless of Opened.
            SyncCalendar();
            NotifyPropertyChanged(nameof(Value), InvalidationImpact.Render);
            RaiseValueChanged(previous, value);
        }
    }

    /// <summary>Gets or sets whether a null value is permitted.</summary>
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
                Value = ClampDate(DateOnly.FromDateTime(TimeProvider.GetLocalNow().DateTime));
            }
        }
    } = true;

    /// <summary>Gets or sets the Gregorian culture used for date formatting and segment order.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The current <see cref="Format"/> cannot be rendered by a <see cref="DateOnly"/> under this culture.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public CultureInfo Culture
    {
        get => _culture;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            VerifyMutable();

            if (ReferenceEquals(_culture, value))
            {
                return;
            }

            ValidateFormat(Format, value, nameof(value));

            _culture = value;
            _calendar.Culture = value.DateTimeFormat.Calendar is GregorianCalendar
                ? value
                : CultureInfo.InvariantCulture;
            NotifyPropertyChanged(nameof(Culture), InvalidationImpact.Measure);
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
            ValidateFormat(value, _culture, nameof(value));
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = "d";

    /// <summary>Validates that a format pattern is renderable by <see cref="DateOnly"/> under a culture,
    /// so an invalid pattern is rejected at the property boundary instead of throwing later from the
    /// layout pass, where it would escape as an unhandled exception.</summary>
    private static void ValidateFormat(string format, CultureInfo culture, string paramName)
    {
        try
        {
            // A probe date every calendar can represent - DateOnly.MaxValue (9999-12-31) exceeds
            // UmAlQuraCalendar.MaxSupportedDateTime (2077-11-16), which threw
            // ArgumentOutOfRangeException naming an internal probe argument for every culture
            // whose default calendar is UmAlQura (ar-SA, en-SA), rejecting an otherwise-valid
            // culture.
            _ = _probeDate.ToString(format, culture);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
        {
            throw new ArgumentException(
                $"The format \"{format}\" cannot be rendered by a DateOnly value.",
                paramName,
                exception);
        }
    }

    /// <summary>Gets or sets the earliest selectable date.</summary>
    /// <exception cref="ArgumentException">The value exceeds <see cref="Maximum"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public DateOnly Minimum
    {
        get;
        set
        {
            if (value > Maximum)
            {
                throw new ArgumentException("Minimum cannot exceed Maximum.", nameof(value));
            }

            if (SetProperty(ref field, value, InvalidationImpact.None))
            {
                _calendar.MinimumDate = value;
                RepairValue();
            }
        }
    } = DateOnly.MinValue;

    /// <summary>Gets or sets the latest selectable date.</summary>
    /// <exception cref="ArgumentException">The value precedes <see cref="Minimum"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public DateOnly Maximum
    {
        get;
        set
        {
            if (value < Minimum)
            {
                throw new ArgumentException("Maximum cannot precede Minimum.", nameof(value));
            }

            if (SetProperty(ref field, value, InvalidationImpact.None))
            {
                _calendar.MaximumDate = value;
                RepairValue();
            }
        }
    } = DateOnly.MaxValue;

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

    /// <summary>Gets or sets whether the Calendar popup is open.</summary>
    /// <exception cref="ArgumentException">The attached DateInput is not an eligible modal root.</exception>
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

    #endregion

    #region Input, layout, and rendering

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        EnsureSeeded();
        _ = MeasureChild(_popup, new Constraint(constraint.Width, DropDownHeight.Add(1)));
        var width = MeasureCells(FormatValue()).Add(2);
        return new Size(width, 1);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) =>
        ArrangeChild(_popup, RootBounds(bounds), ResolvedAxes.Both);

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
        var textAreaWidth = Math.Max(0, content.Width - 2);
        var segments = BuildSegments();
        var isPlaceholder = _value is null;
        var x = content.X;
        var editableIndex = -1;

        for (var index = 0; index < segments.Length && x < content.X + textAreaWidth; index++)
        {
            var segment = segments[index];

            if (segment.Editable)
            {
                editableIndex++;
            }

            var segmentStyle = Focused && !Opened && segment.Editable && editableIndex == _segments.ActiveSegment
                ? SegmentHighlightStyle(style)
                : isPlaceholder
                    ? PlaceholderStyle(style)
                    : style;
            var clipped = canvas.Clip(new Rect(x, content.Y, Math.Max(0, content.X + textAreaWidth - x), 1));
            _ = clipped.Draw(
                segment.Text.AsSpan(),
                new Point(x, content.Y),
                segmentStyle,
                background: BackgroundMode.Transparent);
            x += MeasureCells(segment.Text);
        }

        var themed = ControlGlyphs.Disclosure.DropDown;
        var glyph = ResolveDropDownGlyph(themed.Fallback);
        canvas.DrawRune(
            glyph,
            new Point(Math.Max(content.X, content.Right - 1), content.Y),
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

        if (!Opened && eventArgs is KeyEventArgs { Stroke: { Action: KeyAction.Press } key })
        {
            if (key.Code == Code.Down && (key.Modifiers & Modifiers.Alt) != 0)
            {
                Opened = true;
                eventArgs.Handled = true;
                return;
            }

            if (key.Code == Code.F4)
            {
                Opened = true;
                eventArgs.Handled = true;
                return;
            }

            var hasEditableSegments = _segments.HasEditableSegments;

            if (hasEditableSegments && key.Code == Code.Up)
            {
                _ = _segments.Increment(1);
                eventArgs.Handled = true;
                return;
            }

            if (hasEditableSegments && key.Code == Code.Down)
            {
                _ = _segments.Increment(-1);
                eventArgs.Handled = true;
                return;
            }

            if (hasEditableSegments && key.Code == Code.Left)
            {
                _ = _segments.MoveSegment(-1, wrap: false);
                eventArgs.Handled = true;
                return;
            }

            if (hasEditableSegments && key.Code == Code.Right)
            {
                _ = _segments.MoveSegment(1, wrap: false);
                eventArgs.Handled = true;
                return;
            }

            if (hasEditableSegments && key.Code == Code.Home)
            {
                _ = _segments.MoveToEdge(first: true);
                eventArgs.Handled = true;
                return;
            }

            if (hasEditableSegments && key.Code == Code.End)
            {
                _ = _segments.MoveToEdge(first: false);
                eventArgs.Handled = true;
                return;
            }

            if (key.Code == Code.Delete && AllowNull)
            {
                Value = null;
                eventArgs.Handled = true;
                return;
            }

            if (hasEditableSegments && key.Code == Code.Backspace)
            {
                _ = _segments.ClearActiveSegment();
                eventArgs.Handled = true;
                return;
            }

            if (hasEditableSegments && key.Code == Code.Character && key.Character is { } ch &&
                ch.Value is >= '0' and <= '9')
            {
                _ = _segments.TypeDigit(ch.Value - '0');
                eventArgs.Handled = true;
                return;
            }
        }

        if (!Opened && eventArgs is PointerEventArgs pointer)
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

        if (focused)
        {
            _segments.ActivateFirstSegment();
        }
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
        SyncCalendar();
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

        if (eventArgs.Selection is { } interval)
        {
            Value = interval.Start;
        }

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

    #region Segment editing

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

        if (!_segments.ActivateSegmentAtColumn(localX, CellPolicy.AmbiguousWidth))
        {
            return;
        }

        if (!Focused)
        {
            _ = RequestFocus();
        }

        eventArgs.Handled = true;
    }

    private bool ApplySegmentIncrement(TemporalSegmentKind kind, int delta)
    {
        if (_value is not { } date)
        {
            return false;
        }

        try
        {
#pragma warning disable IDE0072 // Only date-kind segments are reachable from DateInput's layout.
            var adjusted = kind switch
            {
                TemporalSegmentKind.Month => date.AddMonths(delta),
                TemporalSegmentKind.Day => date.AddDays(delta),
                TemporalSegmentKind.Year => new DateOnly(date.Year + delta, date.Month, Math.Min(date.Day, DateTime.DaysInMonth(date.Year + delta, date.Month))),
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
        if (_value is not { } date)
        {
            return false;
        }

        try
        {
#pragma warning disable IDE0072 // Only date-kind segments are reachable from DateInput's layout.
            var result = kind switch
            {
                TemporalSegmentKind.Month => new DateOnly(date.Year, Math.Clamp(value, 1, 12),
                    Math.Min(date.Day, DateTime.DaysInMonth(date.Year, Math.Clamp(value, 1, 12)))),
                TemporalSegmentKind.Day => new DateOnly(date.Year, date.Month,
                    Math.Clamp(value, 1, DateTime.DaysInMonth(date.Year, date.Month))),
                TemporalSegmentKind.Year => new DateOnly(Math.Clamp(value, 1, 9999), date.Month,
                    Math.Min(date.Day, DateTime.DaysInMonth(Math.Clamp(value, 1, 9999), date.Month))),
                _ => date
            };
#pragma warning restore IDE0072

            return CommitSegmentValue(ClampDate(result));
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private bool ClearSegmentValue(TemporalSegmentKind kind)
    {
        if (_value is not { } date)
        {
            return false;
        }

        try
        {
#pragma warning disable IDE0072 // Only date-kind segments are reachable from DateInput's layout.
            var result = kind switch
            {
                TemporalSegmentKind.Month => new DateOnly(date.Year, 1, Math.Min(date.Day, 31)),
                TemporalSegmentKind.Day => new DateOnly(date.Year, date.Month, 1),
                TemporalSegmentKind.Year => new DateOnly(1, date.Month,
                    Math.Min(date.Day, DateTime.DaysInMonth(1, date.Month))),
                _ => date
            };
#pragma warning restore IDE0072

            return CommitSegmentValue(ClampDate(result));
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private bool CommitSegmentValue(DateOnly value)
    {
        var previous = _value;
        Value = value;
        return _value != previous;
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

        if (_value is { } date)
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

    private static TerminalStyle SegmentHighlightStyle(TerminalStyle source) => new(
        source.Foreground,
        source.Background,
        source.Attributes | TerminalAttributes.Reverse,
        source.Hyperlink,
        source.Underline,
        source.UnderlineColor);

    private static TerminalStyle PlaceholderStyle(TerminalStyle source) => new(
        source.Foreground,
        source.Background,
        source.Attributes | TerminalAttributes.Dim,
        source.Hyperlink,
        source.Underline,
        source.UnderlineColor);

    #endregion

    #region Value management

    private void SyncCalendar()
    {
        // Guards against OnCalendarSelectionChanged treating this programmatic resync as a user
        // pick: without it, setting _calendar.Selection below re-enters through the
        // SelectionChanged event and unconditionally closes the popup, even when the caller only
        // meant to keep an already-open popup showing the field's current value.
        _synchronizingCalendar = true;

        try
        {
            _calendar.Culture = _culture.DateTimeFormat.Calendar is GregorianCalendar
                ? _culture
                : CultureInfo.InvariantCulture;
            _calendar.MinimumDate = Minimum;
            _calendar.MaximumDate = Maximum;

            if (_value is { } date)
            {
                _calendar.Selection = new DateInterval(date, date);
            }
            else
            {
                _ = _calendar.ClearSelection();
            }
        }
        finally
        {
            _synchronizingCalendar = false;
        }
    }

    /// <summary>Latches Value to the current local date on first read, so a control mounted under
    /// a dispatcher observes that dispatcher's clock instead of the clock current at
    /// construction, and pushes the newly resolved value into the owned Calendar. A value already
    /// committed - including an explicit null under <see cref="AllowNull"/> - is left
    /// untouched.</summary>
    private void EnsureSeeded()
    {
        if (_seeded)
        {
            return;
        }

        _seeded = true;
        _value = ClampDate(DateOnly.FromDateTime(TimeProvider.GetLocalNow().DateTime));
        SyncCalendar();
    }

    private DateOnly ClampDate(DateOnly date) =>
        date < Minimum
            ? Minimum
            : date > Maximum
                ? Maximum
                : date;

    private void RepairValue()
    {
        if (_value is { } date)
        {
            var clamped = ClampDate(date);

            if (clamped != date)
            {
                Value = clamped;
            }
        }
    }

    private void RaiseValueChanged(DateOnly? previous, DateOnly? current)
    {
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(
            () => ValueChanged?.Invoke(this, new DateInputValueChangedEventArgs(previous, current)),
            ref failure);
        failure?.Throw();
    }

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
