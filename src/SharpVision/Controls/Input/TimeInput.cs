// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using SharpVision.Terminal.Input;

/// <summary>Defines a bordered field control for editing <see cref="TimeOnly"/> values using inline segment editing.</summary>
/// <remarks>
/// Each time segment (hour, minute, second, AM/PM) is independently editable.
/// Up/Down arrows increment or decrement the focused segment. Left/Right arrows
/// navigate between segments. Typing digits replaces the segment value.
/// Delete or Backspace clears the value to null when <see cref="AllowNull"/> is set.
/// <see cref="Culture"/> localizes the rendered time separator, the AM/PM designator text, and
/// the digit glyphs used for each numeric segment. The segment order itself - hour, minute,
/// optionally second, optionally an AM/PM designator - defaults to <see cref="Use24HourFormat"/>
/// and <see cref="ShowSeconds"/> rather than <see cref="CultureInfo.DateTimeFormat"/>'s time
/// pattern, since those two properties are the field's own explicit structural contract; set
/// <see cref="Format"/> to override that structure with a custom pattern.
/// </remarks>
[PublicAPI]
public sealed class TimeInput: InputBase
{
    /// <inheritdoc/>
    protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
        (theme ?? ThemeCatalog.Dark).GetStyleSet(InputStyle.Default).ToAppearanceStates();

    private const int _contentHeight = 1;

    private static readonly IReadOnlyDictionary<char, TemporalSegmentKind> _tokenKinds =
        new Dictionary<char, TemporalSegmentKind>
        {
            ['H'] = TemporalSegmentKind.Hour,
            ['h'] = TemporalSegmentKind.Hour,
            ['m'] = TemporalSegmentKind.Minute,
            ['s'] = TemporalSegmentKind.Second,
            ['t'] = TemporalSegmentKind.AmPmDesignator
        };

    private readonly SegmentFieldBehavior _segments;
    private TimeOnly? _value;
    private bool _seeded;
    private CultureInfo _culture;

    #region Construction and properties

    /// <summary>Initializes a focusable time input at the current local time with a light field border.</summary>
    public TimeInput()
    {
        // Value resolves the current local time lazily, on first read, rather than here: a
        // control constructed off-dispatcher and then mounted under a dispatcher with its own
        // TimeProvider must observe that dispatcher's clock instead of latching the clock that
        // happened to be current at construction.
        _culture = CultureInfo.InvariantCulture;
        _segments = EnableSegmentEditing(
            BuildSegments,
            ApplyDigitValue,
            IncrementSegmentValue,
            ClearSegmentValue);
        TabNavigation = TabNavigation.None;
    }

    /// <summary>Raised after a committed value transition.</summary>
    public event EventHandler<TimeInputValueChangedEventArgs>? ValueChanged;

    /// <summary>Gets or sets the current time value, or null when cleared.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public TimeOnly? Value
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
                _ = Commit(ClampToRange(TimeOnly.FromDateTime(TimeProvider.GetLocalNow().DateTime)));
            }
        }
    } = true;

    /// <summary>Gets or sets the culture applied to the rendered time separator, the AM/PM
    /// designator text, and each numeric segment's digit glyphs. Default is
    /// <see cref="CultureInfo.InvariantCulture"/>, so out-of-the-box rendering never depends on
    /// the host operating system's locale; set this explicitly to localize the field.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">A non-null <see cref="Format"/> cannot be rendered by a <see cref="TimeOnly"/> under this culture.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public CultureInfo Culture
    {
        get => _culture;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (Format is { } format)
            {
                TemporalFormatValidation.Validate(
                    format, value, nameof(value), "TimeOnly", static (f, c) => TimeOnly.MinValue.ToString(f, c));
            }

            if (SetProperty(ref _culture, value, InvalidationImpact.Measure))
            {
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

    /// <summary>Gets or sets a custom time format pattern, or null to derive the pattern from
    /// <see cref="Use24HourFormat"/> and <see cref="ShowSeconds"/>. Default is null.</summary>
    /// <remarks>
    /// When set, the pattern's own hour/minute/second/AM-PM token runs - not
    /// <see cref="Use24HourFormat"/> or <see cref="ShowSeconds"/> - determine the segment order and
    /// count; pair a 12-hour <c>h</c>/<c>hh</c> hour token with a <c>t</c>/<c>tt</c> AM/PM
    /// designator token for correct 12-hour clamping, since a 12-hour hour token without a
    /// designator token is treated as a 24-hour segment for editing purposes.
    /// </remarks>
    /// <exception cref="ArgumentException">The value is empty, or cannot be rendered by a <see cref="TimeOnly"/> under <see cref="Culture"/>.</exception>
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
                    value, _culture, nameof(value), "TimeOnly", static (f, c) => TimeOnly.MinValue.ToString(f, c));
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

    /// <summary>Gets or sets the inclusive lower bound for the value. Default is <see cref="TimeOnly.MinValue"/>.</summary>
    /// <exception cref="ArgumentException">The minimum exceeds <see cref="Maximum"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public TimeOnly Minimum
    {
        get;
        set
        {
            ArgumentException.ThrowIfAboveMaximum(value, Maximum, nameof(value), "Minimum cannot exceed Maximum.");

            _ = SetPropertyAndContinue(ref field, value, InvalidationImpact.Render, ClampCurrentValue);
        }
    } = TimeOnly.MinValue;

    /// <summary>Gets or sets the inclusive upper bound for the value. Default is <see cref="TimeOnly.MaxValue"/>.</summary>
    /// <exception cref="ArgumentException">The maximum is below <see cref="Minimum"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public TimeOnly Maximum
    {
        get;
        set
        {
            ArgumentException.ThrowIfBelowMinimum(value, Minimum, nameof(value), "Maximum cannot be less than Minimum.");

            _ = SetPropertyAndContinue(ref field, value, InvalidationImpact.Render, ClampCurrentValue);
        }
    } = TimeOnly.MaxValue;

    /// <summary>Gets or sets the optional leading edge-pinned decoration, reserved inside the
    /// content box and outside the editable segment layout - it never overlaps a segment.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Affix? StartAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    /// <summary>Gets or sets the optional trailing edge-pinned decoration, reserved inside the
    /// content box and outside the editable segment layout - it never overlaps a segment.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Affix? EndAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    #endregion

    #region Layout

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        EnsureSeeded();
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());
        var width = affixes.StartCells + affixes.EndCells;

        foreach (var segment in BuildSegments())
        {
            width += MeasureCells(segment.Text);
        }

        return new Size(width, _contentHeight);
    }

    /// <summary>Resolves the box editable segment text is drawn into - the content box deflated
    /// for any active <see cref="StartAffix"/>/<see cref="EndAffix"/>. There is no drop-down
    /// indicator to further deflate around: unlike <see cref="ComboBox"/>, <see
    /// cref="DateInput"/>, and <see cref="DateTimeInput"/>, TimeInput has no popup.</summary>
    private Rect ResolveTextBox()
    {
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());
        return DeflateForAffixes(ContentBounds, affixes);
    }

    #endregion

    #region Input

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        EnsureSeeded();

        if (!EffectiveIsEnabled || !EffectiveIsVisible)
        {
            base.OnEvent(eventArgs);
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

        if (!eventArgs.IsHandled)
        {
            base.OnEvent(eventArgs);
        }
    }

    private void HandleKey(KeyEventArgs eventArgs)
    {
        var stroke = eventArgs.Stroke;

        if (!eventArgs.IsKeyDown)
        {
            return;
        }

        if (TryGetStepDelta(eventArgs, out var delta))
        {
            if (_segments.Increment(delta))
            {
                eventArgs.IsHandled = true;
            }

            return;
        }

#pragma warning disable IDE0072 // Unknown or unsupported keys intentionally remain unhandled.
        var handled = stroke.Code switch
        {
            Code.Left => _segments.MoveSegment(-1, wrap: false),
            Code.Right => _segments.MoveSegment(1, wrap: false),
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
            eventArgs.IsHandled = true;
        }
    }

    private void HandlePointer(PointerEventArgs eventArgs) =>
        TemporalSegmentClassification.HandlePointer(
            eventArgs,
            ResolveTextBox(),
            CellPolicy.AmbiguousWidth,
            _segments,
            IsFocused,
            RequestFocus);

    private bool ToggleAmPm() =>
        TemporalSegmentClassification.ToggleAmPm(BuildSegments, () => _value.HasValue, _segments);

    /// <summary>Gets whether the current layout - whether derived from <see cref="Use24HourFormat"/>
    /// or overridden by <see cref="Format"/> - includes an AM/PM designator segment, used as the
    /// effective 12-versus-24-hour policy for editing the hour segment.</summary>
    private bool HasAmPmDesignator => TemporalSegmentClassification.HasAmPmDesignator(BuildSegments);

    private bool ClearValue() =>
        AllowNull && _value.HasValue && Commit(null);

    [Pure]
    private static bool IsDigit(Rune character) => TemporalSegmentClassification.IsDigit(character);

    [Pure]
    private static bool IsAmPmToggle(Rune character) => TemporalSegmentClassification.IsAmPmToggle(character);

    #endregion

    #region Segment value callbacks

    private bool ApplyDigitValue(TemporalSegmentKind kind, int value)
    {
        if (!_value.HasValue)
        {
            _ = Commit(ClampToRange(TimeOnly.MinValue));

            if (!_value.HasValue)
            {
                return false;
            }
        }

        var time = _value.Value;

        var hasAmPm = HasAmPmDesignator;
#pragma warning disable IDE0072 // AM/PM designator segments never reach this callback: their digit capacity is zero.
        var clamped = kind switch
        {
            TemporalSegmentKind.Hour => TemporalClockArithmetic.ClampHour(value, hasAmPm),
            TemporalSegmentKind.Minute => TemporalClockArithmetic.ClampMinuteOrSecond(value),
            TemporalSegmentKind.Second => TemporalClockArithmetic.ClampMinuteOrSecond(value),
            _ => value
        };

        var result = kind switch
        {
            TemporalSegmentKind.Hour when hasAmPm =>
                new TimeOnly(To24Hour(clamped, time.Hour >= 12), time.Minute, time.Second),
            TemporalSegmentKind.Hour =>
                new TimeOnly(clamped, time.Minute, time.Second),
            TemporalSegmentKind.Minute =>
                new TimeOnly(time.Hour, clamped, time.Second),
            TemporalSegmentKind.Second =>
                new TimeOnly(time.Hour, time.Minute, clamped),
            _ => time
        };
#pragma warning restore IDE0072

        return Commit(result);
    }

    private bool IncrementSegmentValue(TemporalSegmentKind kind, int delta)
    {
        if (!_value.HasValue)
        {
            return Commit(ClampToRange(TimeOnly.FromDateTime(TimeProvider.GetLocalNow().DateTime)));
        }

        var time = _value.Value;

        // Every case below is only reached for a kind the current layout actually contains
        // (the engine dispatches by the active segment's own kind), so no additional
        // Use24HourFormat/ShowSeconds guard is needed here.
#pragma warning disable IDE0072 // Every calendar kind (Month, Day, Year) is unreachable from TimeInput's time-only layout.
        var result = kind switch
        {
            TemporalSegmentKind.Hour => AddWithoutWrap(time, TimeSpan.TicksPerHour * delta),
            TemporalSegmentKind.Minute => AddWithoutWrap(time, TimeStep.Ticks * delta),
            TemporalSegmentKind.AmPmDesignator => time.AddHours(time.Hour < 12 ? 12 : -12),
            TemporalSegmentKind.Second => AddWithoutWrap(time, TimeSpan.TicksPerSecond * delta),
            _ => time
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

        var time = _value.Value;
#pragma warning disable IDE0072 // Every calendar kind (Month, Day, Year) and AmPmDesignator are unreachable or intentionally no-op here.
        var result = kind switch
        {
            TemporalSegmentKind.Hour => new TimeOnly(0, time.Minute, time.Second),
            TemporalSegmentKind.Minute => new TimeOnly(time.Hour, 0, time.Second),
            TemporalSegmentKind.Second => new TimeOnly(time.Hour, time.Minute, 0),
            _ => time
        };
#pragma warning restore IDE0072

        return Commit(result);
    }

    private static TimeOnly AddWithoutWrap(TimeOnly value, long ticks)
        => ticks switch
        {
            > 0 when ticks > TimeOnly.MaxValue.Ticks - value.Ticks => TimeOnly.MaxValue,
            < 0 when ticks < TimeOnly.MinValue.Ticks - value.Ticks => TimeOnly.MinValue,
            _ => new TimeOnly(value.Ticks + ticks)
        };

    private static int To24Hour(int hour12, bool isPm) => TemporalSegmentClassification.To24Hour(hour12, isPm);

    #endregion

    #region Commit and validation

    private bool Commit(TimeOnly? requested)
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

        ValueChanged?.Invoke(this, new TimeInputValueChangedEventArgs(previous, clamped));
        return true;
    }

    /// <summary>Latches Value to the current local time on first read, so a control mounted under
    /// a dispatcher observes that dispatcher's clock instead of the clock current at
    /// construction. A value already committed - including an explicit null under
    /// <see cref="AllowNull"/> - is left untouched.</summary>
    private void EnsureSeeded()
    {
        if (_seeded)
        {
            return;
        }

        _seeded = true;
        _value = ClampToRange(TimeOnly.FromDateTime(TimeProvider.GetLocalNow().DateTime));
    }

    [Pure]
    private TimeOnly ClampToRange(TimeOnly time) => time.Clamp(Minimum, Maximum);

    private void ClampCurrentValue()
    {
        if (_value.HasValue)
        {
            _ = Commit(ClampToRange(_value.Value));
        }
    }

    #endregion

    #region Rendering

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
        var highlight = SegmentHighlightStyle(style);
        var canHighlight = IsFocused && _value.HasValue;
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());
        RenderAffixes(canvas, content, affixes, StartAffix, EndAffix, style);
        var textBox = DeflateForAffixes(content, affixes);
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
    }

    [Pure]
    private static TerminalStyle SegmentHighlightStyle(TerminalStyle source) => new(
        source.Foreground,
        source.Background,
        source.Attributes | TerminalAttributes.Reverse,
        source.Hyperlink,
        source.Underline,
        source.UnderlineColor);

    #endregion

    #region Segment layout

    private string ResolveTimePattern() => Format ?? BuildDefaultTimePattern();

    private string BuildDefaultTimePattern()
    {
        var pattern = new StringBuilder(Use24HourFormat ? "HH" : "hh").Append(':').Append("mm");

        if (ShowSeconds)
        {
            _ = pattern.Append(':').Append("ss");
        }

        if (!Use24HourFormat)
        {
            _ = pattern.Append(' ').Append("tt");
        }

        return pattern.ToString();
    }

    private SegmentDescriptor[] BuildSegments()
    {
        var pattern = ResolveTimePattern();
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

        if (_value is { } time)
        {
            text = TemporalPatternSegmenter.FormatSegments(
                pattern,
                tokens,
                _tokenKinds,
                format => time.ToString(format, _culture));
        }
        else
        {
            var placeholder = new string[tokens.Count];

            for (var index = 0; index < tokens.Count; index++)
            {
                placeholder[index] = tokens[index].Kind is null ? tokens[index].LiteralText : "--";
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
                    kind == TemporalSegmentKind.AmPmDesignator ? 0 : 2,
                    MaxValueFor(kind, hasAmPm));
        }

        return descriptors;
    }

#pragma warning disable IDE0072 // Month, Day, Year, and AmPmDesignator are unreachable from TimeInput's time-only layout.
    [Pure]
    private static int MaxValueFor(TemporalSegmentKind kind, bool hasAmPm) =>
        kind switch
        {
            TemporalSegmentKind.Hour => hasAmPm ? 12 : 23,
            TemporalSegmentKind.Minute or TemporalSegmentKind.Second => 59,
            _ => 0
        };
#pragma warning restore IDE0072

    #endregion

    #region Lifecycle

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
            ValueChanged = null;
        }
    }

    #endregion
}
