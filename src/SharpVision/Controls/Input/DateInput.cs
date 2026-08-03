// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Runtime.ExceptionServices;

using Popups;

using SharpVision.Terminal.Input;

/// <summary>Displays a formatted date with inline segment editing and a Calendar popup for selection.</summary>
[PublicAPI]
public sealed class DateInput: Control
{
    private const char _segmentStartMarker = '\uE000';
    private const char _segmentEndMarker = '\uE001';

    /// <inheritdoc/>
    protected override ThemeRole ThemeRole => ThemeRole.Input;

    // Every supported calendar - including the narrowest, UmAlQuraCalendar
    // (max 2077-11-16) - can represent this date, unlike DateOnly.MaxValue (see #182).
    private static readonly DateOnly _probeDate = DateOnly.FromDateTime(DateTime.UnixEpoch);

    private readonly Calendar _calendar;
    private readonly Popup _popup;
    private readonly OwnedControlSlot _popupSlot;
    private readonly PressBehavior _interaction;
    private readonly PopupModalTracker _modalTracker;
    private DateOnly? _value;
    private CultureInfo _culture;
    private int _activeSegment;

    #region Construction and properties

    /// <summary>Initializes a bordered date field with a connected Calendar popup.</summary>
    public DateInput()
    {
        _culture = CultureInfo.CurrentCulture.DateTimeFormat.Calendar is GregorianCalendar
            ? CultureInfo.CurrentCulture
            : CultureInfo.InvariantCulture;
        _value = DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime);
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
            Placement = PopupPlacement.Below
        };
        _popup.Opened += OnPopupOpened;
        _popup.Closing += OnPopupClosing;
        _popup.Closed += OnPopupClosed;
        _modalTracker = new PopupModalTracker(_popup, () => IsOpen = false);
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
    public event EventHandler<DateInputValueChangedEventArgs>? ValueChanged;

    /// <summary>Gets or sets the committed date, or null when cleared.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public DateOnly? Value
    {
        get => _value;
        set
        {
            VerifyMutable();

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
            if (SetProperty(ref field, value, InvalidationImpact.None) && !value && _value is null)
            {
                Value = ClampDate(DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime));
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
    /// layout pass, where it would escape as an unhandled exception (see #182).</summary>
    private static void ValidateFormat(string format, CultureInfo culture, string paramName)
    {
        try
        {
            // A probe date every calendar can represent - DateOnly.MaxValue (9999-12-31) exceeds
            // UmAlQuraCalendar.MaxSupportedDateTime (2077-11-16), which threw
            // ArgumentOutOfRangeException naming an internal probe argument for every culture
            // whose default calendar is UmAlQura (ar-SA, en-SA), rejecting an otherwise-valid
            // culture (see #182).
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

            if (SetProperty(ref field, value, InvalidationImpact.None))
            {
                _calendar.MinimumDate = value;
                RepairValue();
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

    /// <summary>Gets or sets whether the Calendar popup is open.</summary>
    /// <exception cref="ArgumentException">The attached DateInput is not an eligible modal root.</exception>
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

    #endregion

    #region Input, layout, and rendering

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = MeasureChild(_popup, new Constraint(constraint.Width, DropDownHeight.Add(1)));
        var width = Terminal.Unicode.Width.Measure(FormatValue()).Cells.Add(2);
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

        var content = ContentBounds;
        var style = ResolvedStyle;
        var textAreaWidth = Math.Max(0, content.Width - 2);
        var segments = BuildSegments();
        var x = content.X;

        for (var index = 0; index < segments.Length && x < content.X + textAreaWidth; index++)
        {
            var segment = segments[index];
            var segmentStyle = IsFocused && !IsOpen && segment.EditableIndex == _activeSegment
                ? SegmentHighlightStyle(style)
                : segment.IsPlaceholder
                    ? PlaceholderStyle(style)
                    : style;
            var clipped = canvas.Clip(new Rect(x, content.Y, Math.Max(0, content.X + textAreaWidth - x), 1));
            _ = clipped.Draw(
                segment.Text.AsSpan(),
                new Point(x, content.Y),
                segmentStyle,
                background: BackgroundMode.Transparent);
            x += Terminal.Unicode.Width.Measure(segment.Text).Cells;
        }

        var themed = ControlGlyphs.Disclosure.DropDown;
        var glyph = themed.Value.Resolve(themed.Fallback, CellPolicy.AmbiguousWidth);
        canvas.DrawRune(
            glyph,
            new Point(Math.Max(content.X, content.Right - 1), content.Y),
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

        if (!IsOpen && eventArgs is KeyEventArgs { Stroke: { Action: KeyAction.Press } key })
        {
            if (key.Code == Code.Down && (key.Modifiers & Modifiers.Alt) != 0)
            {
                IsOpen = true;
                eventArgs.Handled = true;
                return;
            }

            if (key.Code == Code.F4)
            {
                IsOpen = true;
                eventArgs.Handled = true;
                return;
            }

            if (key.Code == Code.Up)
            {
                IncrementActiveSegment(1);
                eventArgs.Handled = true;
                return;
            }

            if (key.Code == Code.Down)
            {
                IncrementActiveSegment(-1);
                eventArgs.Handled = true;
                return;
            }

            if (key.Code == Code.Left)
            {
                NavigateSegment(-1);
                eventArgs.Handled = true;
                return;
            }

            if (key.Code == Code.Right)
            {
                NavigateSegment(1);
                eventArgs.Handled = true;
                return;
            }

            if (key.Code == Code.Home)
            {
                MoveToEdge(first: true);
                eventArgs.Handled = true;
                return;
            }

            if (key.Code == Code.End)
            {
                MoveToEdge(first: false);
                eventArgs.Handled = true;
                return;
            }

            if (key.Code == Code.Delete && AllowNull)
            {
                Value = null;
                eventArgs.Handled = true;
                return;
            }

            if (key.Code == Code.Backspace)
            {
                ClearActiveSegment();
                eventArgs.Handled = true;
                return;
            }

            if (key.Code == Code.Character && key.Character is { } ch &&
                ch.Value is >= '0' and <= '9')
            {
                TypeDigit(ch.Value - '0');
                eventArgs.Handled = true;
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

        if (focused)
        {
            _activeSegment = 0;
            _digitBuffer = null;
            _yearDigitCount = 0;
        }
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
        SyncCalendar();
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

        if (eventArgs.Selection is { } interval)
        {
            Value = interval.Start;
        }

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

    #region Segment editing

    private void IncrementActiveSegment(int delta)
    {
        if (_value is not { } date)
        {
            return;
        }

        _digitBuffer = null;
        _yearDigitCount = 0;
        var segmentKind = ResolveSegmentKind(_activeSegment);

        try
        {
            var adjusted = segmentKind switch
            {
                SegmentKind.Month => date.AddMonths(delta),
                SegmentKind.Day => date.AddDays(delta),
                SegmentKind.Year => new DateOnly(date.Year + delta, date.Month, Math.Min(date.Day, DateTime.DaysInMonth(date.Year + delta, date.Month))),
                _ => date
            };

            Value = ClampDate(adjusted);
        }
        catch (ArgumentOutOfRangeException)
        {
            // Silently ignore increments that push beyond DateOnly bounds.
        }
    }

    private int? _digitBuffer;
    private int _yearDigitCount;

    private void TypeDigit(int digit)
    {
        if (_value is not { } date)
        {
            return;
        }

        var kind = ResolveSegmentKind(_activeSegment);

        if (_digitBuffer.HasValue)
        {
            var combined = (_digitBuffer.Value * 10) + digit;

            // Year needs four digits, not two: keep buffering and stay on the segment
            // until the count reaches four instead of committing after the second.
            if (kind == SegmentKind.Year && ++_yearDigitCount < 4)
            {
                _digitBuffer = combined;
                ApplySegmentDigit(date, kind, combined);
                return;
            }

            _digitBuffer = null;
            _yearDigitCount = 0;
            ApplySegmentDigit(date, kind, combined);

            if (_activeSegment < SegmentCount() - 1)
            {
                _activeSegment++;
                Invalidate(InvalidationImpact.Render);
            }

            return;
        }

        var maxFirst = kind switch
        {
            SegmentKind.Month => 1,
            SegmentKind.Day => 3,
            SegmentKind.Year => 9,
            _ => 0
        };

        if (digit > maxFirst && kind != SegmentKind.Year)
        {
            ApplySegmentDigit(date, kind, digit);

            if (_activeSegment < SegmentCount() - 1)
            {
                _activeSegment++;
                Invalidate(InvalidationImpact.Render);
            }

            return;
        }

        _digitBuffer = digit;
        _yearDigitCount = kind == SegmentKind.Year ? 1 : 0;
        ApplySegmentDigit(date, kind, digit);
    }

    private void ApplySegmentDigit(DateOnly date, SegmentKind kind, int value)
    {
        try
        {
            var result = kind switch
            {
                SegmentKind.Month => new DateOnly(date.Year, Math.Clamp(value, 1, 12),
                    Math.Min(date.Day, DateTime.DaysInMonth(date.Year, Math.Clamp(value, 1, 12)))),
                SegmentKind.Day => new DateOnly(date.Year, date.Month,
                    Math.Clamp(value, 1, DateTime.DaysInMonth(date.Year, date.Month))),
                SegmentKind.Year => new DateOnly(Math.Clamp(value, 1, 9999), date.Month,
                    Math.Min(date.Day, DateTime.DaysInMonth(Math.Clamp(value, 1, 9999), date.Month))),
                _ => date
            };

            Value = ClampDate(result);
        }
        catch (ArgumentOutOfRangeException)
        {
        }
    }

    private void ClearActiveSegment()
    {
        if (_value is not { } date)
        {
            return;
        }

        _digitBuffer = null;
        _yearDigitCount = 0;
        var kind = ResolveSegmentKind(_activeSegment);

        try
        {
            var result = kind switch
            {
                SegmentKind.Month => new DateOnly(date.Year, 1, Math.Min(date.Day, 31)),
                SegmentKind.Day => new DateOnly(date.Year, date.Month, 1),
                SegmentKind.Year => new DateOnly(1, date.Month,
                    Math.Min(date.Day, DateTime.DaysInMonth(1, date.Month))),
                _ => date
            };

            Value = ClampDate(result);
        }
        catch (ArgumentOutOfRangeException)
        {
        }
    }

    private void NavigateSegment(int direction)
    {
        var count = SegmentCount();
        var next = _activeSegment + direction;

        if (next >= 0 && next < count)
        {
            _activeSegment = next;
            _digitBuffer = null;
            _yearDigitCount = 0;
            Invalidate(InvalidationImpact.Render);
        }
    }

    private void MoveToEdge(bool first)
    {
        var target = first ? 0 : SegmentCount() - 1;

        if (_activeSegment == target)
        {
            return;
        }

        _activeSegment = target;
        _digitBuffer = null;
        _yearDigitCount = 0;
        Invalidate(InvalidationImpact.Render);
    }

    private int SegmentCount()
        => BuildPlaceholderSegments().Count(static segment => segment.Kind.HasValue);

    private SegmentKind ResolveSegmentKind(int segmentIndex)
    {
        var current = 0;

        foreach (var segment in BuildPlaceholderSegments())
        {
            if (segment.Kind is not { } kind)
            {
                continue;
            }

            if (current == segmentIndex)
            {
                return kind;
            }

            current++;
        }

        return SegmentKind.Day;
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

    private readonly record struct DisplaySegment(
        string Text,
        bool IsPlaceholder,
        SegmentKind? Kind = null,
        int EditableIndex = -1);

    private DisplaySegment[] BuildSegments()
    {
        if (_value is not { } date)
        {
            return BuildPlaceholderSegments();
        }

        var kinds = BuildPlaceholderSegments()
            .Where(static segment => segment.Kind.HasValue)
            .Select(static segment => segment.Kind!.Value)
            .ToArray();
        var renderingCulture = Format.Length == 1 && Format[0] is 'r' or 'R'
            ? CultureInfo.InvariantCulture
            : _culture;
        var formatted = date.ToString(BuildMarkedFormat(), renderingCulture);
        var segments = new List<DisplaySegment>();
        var cursor = 0;
        var editableIndex = 0;

        while (cursor < formatted.Length)
        {
            var start = formatted.IndexOf(_segmentStartMarker, cursor);

            if (start < 0)
            {
                segments.Add(new DisplaySegment(formatted[cursor..], false));
                break;
            }

            if (start > cursor)
            {
                segments.Add(new DisplaySegment(formatted[cursor..start], false));
            }

            var contentStart = start + 1;
            var end = formatted.IndexOf(_segmentEndMarker, contentStart);

            if (end < 0)
            {
                Debug.Fail("Validated marked date format omitted an editable-segment end marker.");
                segments.Add(new DisplaySegment(formatted[contentStart..], false));
                break;
            }

            Debug.Assert(editableIndex < kinds.Length);
            var kind = editableIndex < kinds.Length ? kinds[editableIndex] : SegmentKind.Day;
            segments.Add(new DisplaySegment(
                formatted[contentStart..end],
                false,
                kind,
                editableIndex));
            editableIndex++;
            cursor = end + 1;
        }

        return [.. segments];
    }

    private string BuildMarkedFormat()
    {
        var pattern = ResolveDatePattern();
        var marked = new StringBuilder(pattern.Length + 16);
        var index = 0;

        while (index < pattern.Length)
        {
            var ch = pattern[index];

            if (ch is '\'' or '"')
            {
                var quote = ch;
                _ = marked.Append(ch);
                index++;

                while (index < pattern.Length)
                {
                    ch = pattern[index];
                    _ = marked.Append(ch);
                    index++;

                    if (ch == '\\' && index < pattern.Length)
                    {
                        _ = marked.Append(pattern[index]);
                        index++;
                    }
                    else if (ch == quote)
                    {
                        break;
                    }
                }

                continue;
            }

            if (ch == '\\' && index + 1 < pattern.Length)
            {
                _ = marked.Append(ch).Append(pattern[index + 1]);
                index += 2;
                continue;
            }

            var percentPrefixed = ch == '%' && index + 1 < pattern.Length;
            var tokenStart = percentPrefixed ? index + 1 : index;
            var token = pattern[tokenStart];

            if (token is 'M' or 'd' or 'y')
            {
                AppendFormatMarker(marked, _segmentStartMarker);

                if (percentPrefixed)
                {
                    _ = marked.Append('%');
                }

                do
                {
                    _ = marked.Append(pattern[tokenStart]);
                    tokenStart++;
                }
                while (!percentPrefixed && tokenStart < pattern.Length && pattern[tokenStart] == token);

                AppendFormatMarker(marked, _segmentEndMarker);
                index = tokenStart;
                continue;
            }

            _ = marked.Append(ch);
            index++;
        }

        return marked.ToString();
    }

    private static void AppendFormatMarker(StringBuilder format, char marker) =>
        _ = format.Append('\'').Append(marker).Append('\'');

    private DisplaySegment[] BuildPlaceholderSegments()
    {
        var pattern = ResolveDatePattern();
        var segments = new List<DisplaySegment>();
        var index = 0;
        var editableIndex = 0;

        while (index < pattern.Length)
        {
            var ch = pattern[index];

            if (ch is '\'' or '"')
            {
                var quote = ch;
                var literal = new StringBuilder();
                index++;

                while (index < pattern.Length && pattern[index] != quote)
                {
                    if (pattern[index] == '\\' && index + 1 < pattern.Length)
                    {
                        index++;
                    }

                    _ = literal.Append(pattern[index]);
                    index++;
                }

                if (index < pattern.Length)
                {
                    index++;
                }

                AppendLiteral(segments, literal.ToString());
                continue;
            }

            if (ch == '\\' && index + 1 < pattern.Length)
            {
                AppendLiteral(segments, pattern[index + 1].ToString());
                index += 2;
                continue;
            }

            if (ch == '%')
            {
                index++;

                if (index >= pattern.Length)
                {
                    break;
                }

                ch = pattern[index];
            }

            if (ch is 'M' or 'd' or 'y')
            {
                var count = 0;

                while (index < pattern.Length && pattern[index] == ch)
                {
                    count++;
                    index++;
                }

                var kind = ch switch
                {
                    'M' => SegmentKind.Month,
                    'd' => SegmentKind.Day,
                    'y' => SegmentKind.Year,
                    _ => SegmentKind.Day
                };
                var placeholder = kind == SegmentKind.Year && count >= 4 ? "----" : "--";
                segments.Add(new DisplaySegment(placeholder, true, kind, editableIndex));
                editableIndex++;
                continue;
            }

            AppendLiteral(
                segments,
                ch == '/' ? _culture.DateTimeFormat.DateSeparator : ch.ToString());
            index++;
        }

        return [.. segments];
    }

    private static void AppendLiteral(List<DisplaySegment> segments, string literal)
    {
        if (literal.Length == 0)
        {
            return;
        }

        if (segments.Count > 0 && segments[^1].Kind is null)
        {
            var previous = segments[^1];
            segments[^1] = new DisplaySegment(previous.Text + literal, true);
            return;
        }

        segments.Add(new DisplaySegment(literal, true));
    }

    private string FormatValue()
    {
        if (_value is not { } date)
        {
            var placeholder = BuildPlaceholderSegments();
            var sb = new StringBuilder();

            foreach (var segment in placeholder)
            {
                _ = sb.Append(segment.Text);
            }

            return sb.ToString();
        }

        return date.ToString(Format, _culture);
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
        _calendar.Culture = _culture.DateTimeFormat.Calendar is GregorianCalendar
            ? _culture
            : CultureInfo.InvariantCulture;
        _calendar.MinimumDate = MinimumDate;
        _calendar.MaximumDate = MaximumDate;

        if (_value is { } date)
        {
            _calendar.Selection = new DateInterval(date, date);
        }
        else
        {
            _ = _calendar.ClearSelection();
        }
    }

    private DateOnly ClampDate(DateOnly date) =>
        date < MinimumDate
            ? MinimumDate
            : date > MaximumDate
                ? MaximumDate
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

    private enum SegmentKind
    {
        Month,
        Day,
        Year
    }
}
