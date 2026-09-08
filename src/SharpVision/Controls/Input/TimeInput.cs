// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines a bordered field control for editing <see cref="TimeOnly"/> values using inline segment editing.</summary>
/// <remarks>
/// Each time segment (hour, minute, second, fractional second, AM/PM) is independently editable.
/// Up/Down arrows increment or decrement the focused segment. Left/Right arrows
/// navigate between segments. Typing digits replaces the segment value.
/// Delete clears the value to null when <see cref="TemporalInputBase{TValue}.AllowNull"/> is set; Backspace clears only
/// the active segment. Custom <c>f</c> and <c>F</c> runs expose one to seven fractional digits;
/// uppercase runs reserve blank editing cells when formatted trailing zeroes are omitted.
/// <see cref="TemporalInputBase{TValue}.Culture"/> localizes the rendered time separator, the AM/PM designator text, and
/// the digit glyphs used for each numeric segment. The segment order itself - hour, minute,
/// optionally second, optionally an AM/PM designator - defaults to <see cref="Use24HourFormat"/>
/// and <see cref="ShowSeconds"/> rather than <see cref="CultureInfo.DateTimeFormat"/>'s time
/// pattern, since those two properties are the field's own explicit structural contract; set
/// <see cref="Format"/> to override that structure with a custom pattern.
/// </remarks>
[PublicAPI]
public sealed class TimeInput: TemporalInputBase<TimeOnly>
{
    private static readonly IReadOnlyDictionary<char, TemporalSegmentKind> _tokenKinds =
        new Dictionary<char, TemporalSegmentKind>
        {
            ['H'] = TemporalSegmentKind.Hour,
            ['h'] = TemporalSegmentKind.Hour,
            ['m'] = TemporalSegmentKind.Minute,
            ['s'] = TemporalSegmentKind.Second,
            ['f'] = TemporalSegmentKind.FractionalSecond,
            ['F'] = TemporalSegmentKind.FractionalSecond,
            ['t'] = TemporalSegmentKind.AmPmDesignator
        };

    #region Construction and properties

    /// <summary>Initializes a focusable time input at the current local time with a light field border.</summary>
    /// <remarks>Value resolves the current local time lazily, on first read, rather than here: a
    /// control constructed off-dispatcher and then mounted under a dispatcher with its own
    /// TimeProvider must observe that dispatcher's clock instead of latching the clock that
    /// happened to be current at construction.</remarks>
    public TimeInput()
        : base(TimeOnly.MinValue, TimeOnly.MaxValue, CultureInfo.InvariantCulture) =>
        TabNavigation = TabNavigation.None;

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

    /// <summary>Gets or sets a custom time format pattern, or null to derive the pattern from
    /// <see cref="Use24HourFormat"/> and <see cref="ShowSeconds"/>. Default is null.</summary>
    /// <remarks>
    /// When set, the pattern's own hour/minute/second/AM-PM token runs - not
    /// <see cref="Use24HourFormat"/> or <see cref="ShowSeconds"/> - determine the segment order and
    /// count; pair a 12-hour <c>h</c>/<c>hh</c> hour token with a <c>t</c>/<c>tt</c> AM/PM
    /// designator token for correct 12-hour clamping, since a 12-hour hour token without a
    /// designator token is treated as a 24-hour segment for editing purposes.
    /// </remarks>
    /// <exception cref="ArgumentException">The value is empty, cannot be rendered by a <see cref="TimeOnly"/> under <see cref="TemporalInputBase{TValue}.Culture"/>, or declares an editable fractional-second run wider than seven digits (including via a percent-escaped <c>%f</c> run), which the segmented layout cannot represent even if <see cref="TimeOnly"/> itself would render it.</exception>
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
                    value, Culture, nameof(value), "TimeOnly", static (f, c) => TimeOnly.MinValue.ToString(f, c), _tokenKinds);
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

    #endregion

    #region Layout

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        EnsureSeeded();
        return MeasureSegmentedField();
    }

    #endregion

    #region Input

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
    protected override TimeOnly? ApplyDigit(TimeOnly value, TemporalSegmentKind kind, int digitValue, int digitCapacity)
    {
        var time = value;
        var hasAmPm = HasAmPmDesignator;
#pragma warning disable IDE0072 // AM/PM designator segments never reach this callback: their digit capacity is zero.
        var clamped = kind switch
        {
            TemporalSegmentKind.Hour => TemporalClockArithmetic.ClampHour(digitValue, hasAmPm),
            TemporalSegmentKind.Minute => TemporalClockArithmetic.ClampMinuteOrSecond(digitValue),
            TemporalSegmentKind.Second => TemporalClockArithmetic.ClampMinuteOrSecond(digitValue),
            _ => digitValue
        };

        var result = kind switch
        {
            TemporalSegmentKind.Hour when hasAmPm =>
                new TimeOnly(
                    TemporalSegmentClassification.To24Hour(clamped, time.Hour >= 12),
                    time.Minute,
                    time.Second),
            TemporalSegmentKind.Hour =>
                new TimeOnly(clamped, time.Minute, time.Second),
            TemporalSegmentKind.Minute =>
                new TimeOnly(time.Hour, clamped, time.Second),
            TemporalSegmentKind.Second =>
                new TimeOnly(time.Hour, time.Minute, clamped),
            TemporalSegmentKind.FractionalSecond => new TimeOnly(
                time.Ticks - (time.Ticks % TimeSpan.TicksPerSecond) +
                TemporalClockArithmetic.FractionalSecondTicks(digitValue, digitCapacity)),
            _ => time
        };
#pragma warning restore IDE0072

        return kind == TemporalSegmentKind.FractionalSecond
            ? result
            : WithSubSecondTicksOf(result, time);
    }

    /// <inheritdoc/>
    protected override TimeOnly? Increment(TimeOnly value, TemporalSegmentKind kind, int delta, int digitCapacity)
    {
        var time = value;

        // Every case below is only reached for a kind the current layout actually contains
        // (the engine dispatches by the active segment's own kind), so no additional
        // Use24HourFormat/ShowSeconds guard is needed here.
#pragma warning disable IDE0072 // Every calendar kind (Month, Day, Year) is unreachable from TimeInput's time-only layout.
        return kind switch
        {
            TemporalSegmentKind.Hour => AddWithoutWrap(time, TimeSpan.TicksPerHour * delta),
            TemporalSegmentKind.Minute => AddWithoutWrap(time, TimeStep.Ticks * delta),
            TemporalSegmentKind.AmPmDesignator => time.AddHours(time.Hour < 12 ? 12 : -12),
            TemporalSegmentKind.Second => AddWithoutWrap(time, TimeSpan.TicksPerSecond * delta),
            TemporalSegmentKind.FractionalSecond => AddWithoutWrap(
                time,
                TemporalClockArithmetic.FractionalSecondUnitTicks(digitCapacity) * delta),
            _ => time
        };
#pragma warning restore IDE0072
    }

    /// <inheritdoc/>
    protected override TimeOnly? ClearSegment(TimeOnly value, TemporalSegmentKind kind)
    {
        var time = value;
#pragma warning disable IDE0072 // Every calendar kind (Month, Day, Year) and AmPmDesignator are unreachable or intentionally no-op here.
        var result = kind switch
        {
            TemporalSegmentKind.Hour => new TimeOnly(0, time.Minute, time.Second),
            TemporalSegmentKind.Minute => new TimeOnly(time.Hour, 0, time.Second),
            TemporalSegmentKind.Second => new TimeOnly(time.Hour, time.Minute, 0),
            TemporalSegmentKind.FractionalSecond => new TimeOnly(
                time.Ticks - (time.Ticks % TimeSpan.TicksPerSecond)),
            _ => time
        };
#pragma warning restore IDE0072

        return kind == TemporalSegmentKind.FractionalSecond
            ? result
            : WithSubSecondTicksOf(result, time);
    }

    private static TimeOnly AddWithoutWrap(TimeOnly value, long ticks)
        => ticks switch
        {
            > 0 when ticks > TimeOnly.MaxValue.Ticks - value.Ticks => TimeOnly.MaxValue,
            < 0 when ticks < TimeOnly.MinValue.Ticks - value.Ticks => TimeOnly.MinValue,
            _ => new TimeOnly(value.Ticks + ticks)
        };

    /// <summary>Rebuilds <paramref name="result"/> with the sub-second (tick-resolution) remainder
    /// carried over from <paramref name="original"/>, since <paramref name="result"/> is always
    /// reconstructed from whole hour/minute/second components and would otherwise silently drop
    /// any fractional-second precision <paramref name="original"/> already had.</summary>
    [Pure]
    private static TimeOnly WithSubSecondTicksOf(TimeOnly result, TimeOnly original) =>
        new(result.Ticks + TemporalClockSegments.SubSecondRemainderTicks(original.Ticks));

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
        RenderSegmentedField(canvas, isPlaceholder: _state.Value is null);
    }

    #endregion

    #region Temporal seams

    /// <inheritdoc/>
    protected override TimeOnly ResolveClockSeed() => TimeOnly.FromDateTime(TimeProvider.GetLocalNow().DateTime);

    /// <inheritdoc/>
    protected override TimeOnly ResolveDigitEntrySeed() => TimeOnly.MinValue;

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<char, TemporalSegmentKind> TokenKinds => _tokenKinds;

    /// <inheritdoc/>
    protected override string ResolvePattern() =>
        Format ?? TemporalClockSegments.BuildDefaultTimePattern(Use24HourFormat, ShowSeconds);

    /// <inheritdoc/>
    protected override string FormatValue(TimeOnly value, string format, CultureInfo culture) =>
        value.ToString(format, culture);

    /// <inheritdoc/>
    protected override bool IsPm(TimeOnly value) => value.Hour >= 12;

    /// <inheritdoc/>
    protected override int MaxValueFor(TemporalSegmentKind kind, bool hasAmPmDesignator, int runLength) =>
        TemporalClockSegments.MaxValueFor(kind, hasAmPmDesignator, runLength);

    /// <inheritdoc/>
    protected override void ValidateCulture(CultureInfo culture)
    {
        if (Format is { } format)
        {
            TemporalFormatValidation.Validate(
                format, culture, "value", "TimeOnly", static (f, c) => TimeOnly.MinValue.ToString(f, c), _tokenKinds);
        }
    }

    /// <inheritdoc/>
    protected override Func<Rune, bool>? ResolveCharacterCommand() => HandleCharacterCommand;

    #endregion
}
