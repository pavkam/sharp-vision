// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>A minimal <see cref="TemporalInputBase{TValue}"/> derivative over <see cref="DateOnly"/>
/// that edits only a year and a month segment - no day, no clock component - proving the base's
/// shared range, lazy-seed, commit, and event contract independent of any shipped field's own
/// richer calendar or clock policy, and proving that a derivative outside the three shipped
/// fields' own segment set (year and month only, never day) still composes correctly against the
/// abstract per-segment seams alone.</summary>
internal sealed class YearMonthInputProbe: TemporalInputBase<DateOnly>
{
    private static readonly IReadOnlyDictionary<char, TemporalSegmentKind> _tokenKinds =
        new Dictionary<char, TemporalSegmentKind>
        {
            ['y'] = TemporalSegmentKind.Year,
            ['M'] = TemporalSegmentKind.Month
        };

    /// <summary>The fixed seed <see cref="ResolveClockSeed"/> returns, standing in for a real
    /// dispatcher clock so tests observe a deterministic seed instead of the ambient current
    /// month.</summary>
    internal static readonly DateOnly Seed = new(2026, 6, 1);

    /// <summary>Initializes a focusable year-month probe seeded from <see cref="Seed"/>.</summary>
    internal YearMonthInputProbe()
        : base(DateOnly.MinValue, DateOnly.MaxValue, CultureInfo.InvariantCulture) =>
        TabNavigation = TabNavigation.None;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        EnsureSeeded();
        return MeasureSegmentedField();
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        EnsureSeeded();
        RenderSegmentedField(canvas, isPlaceholder: Value is null);
    }

    /// <inheritdoc/>
    protected override DateOnly ResolveClockSeed() => Seed;

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<char, TemporalSegmentKind> TokenKinds => _tokenKinds;

    /// <inheritdoc/>
    protected override string ResolvePattern() => "yyyy-MM";

    /// <inheritdoc/>
    protected override string FormatValue(DateOnly value, string format, CultureInfo culture) =>
        value.ToString(format, culture);

    /// <inheritdoc/>
    protected override int MaxValueFor(TemporalSegmentKind kind, bool hasAmPmDesignator, int runLength)
    {
        _ = hasAmPmDesignator;
        _ = runLength;
        return kind == TemporalSegmentKind.Month ? 12 : 9999;
    }

    /// <inheritdoc/>
    protected override DateOnly? Increment(DateOnly value, TemporalSegmentKind kind, int delta, int digitCapacity)
    {
        _ = digitCapacity;

        try
        {
            return kind == TemporalSegmentKind.Month ? value.AddMonths(delta) : ReplaceYear(value, value.Year + delta);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    protected override DateOnly? ApplyDigit(DateOnly value, TemporalSegmentKind kind, int digitValue, int digitCapacity)
    {
        _ = digitCapacity;
        return kind == TemporalSegmentKind.Month
            ? ReplaceMonth(value, Math.Clamp(digitValue, 1, 12))
            : ReplaceYear(value, Math.Clamp(digitValue, 1, 9999));
    }

    /// <inheritdoc/>
    protected override DateOnly? ClearSegment(DateOnly value, TemporalSegmentKind kind) =>
        kind == TemporalSegmentKind.Month ? ReplaceMonth(value, 1) : ReplaceYear(value, 1);

    /// <inheritdoc/>
    protected override void ValidateCulture(CultureInfo culture)
    {
    }

    private static DateOnly ReplaceMonth(DateOnly value, int month)
    {
        var (year, resolvedMonth, day) = TemporalCalendarArithmetic.ReplaceMonth(value.Year, value.Day, month);
        return new DateOnly(year, resolvedMonth, day);
    }

    private static DateOnly ReplaceYear(DateOnly value, int year)
    {
        var (clampedYear, month, day) = TemporalCalendarArithmetic.ReplaceYear(value.Month, value.Day, year);
        return new DateOnly(clampedYear, month, day);
    }
}
