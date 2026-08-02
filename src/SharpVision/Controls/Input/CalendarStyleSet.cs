// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines optional member-wise contributions to a complete calendar presentation.</summary>
[PublicAPI]
public readonly record struct CalendarStyleSet
{
    /// <summary>Initializes a partial calendar presentation contribution.</summary>
    /// <param name="selectedDayColor">The optional replacement foreground for a selected date.</param>
    /// <param name="todayMarkerColor">The optional replacement foreground for the hovered date or interval preview.</param>
    /// <param name="outOfMonthDayColor">The optional replacement foreground for an out-of-month date.</param>
    /// <param name="weekdayHeaderColor">The optional replacement foreground for the weekday row.</param>
    /// <param name="disabledDayColor">The optional replacement foreground for a disabled date.</param>
    /// <param name="contentInset">The optional replacement internal content inset.</param>
    /// <param name="appearance">The optional partial normal and visual-state appearance profile.</param>
    /// <exception cref="ArgumentException">A supplied part foreground is transparent.</exception>
    public CalendarStyleSet(
        ColorValue? selectedDayColor = null,
        ColorValue? todayMarkerColor = null,
        ColorValue? outOfMonthDayColor = null,
        ColorValue? weekdayHeaderColor = null,
        ColorValue? disabledDayColor = null,
        Thickness? contentInset = null,
        AppearanceProfileSet? appearance = null)
    {
        if (selectedDayColor is { } selectedDayColorValue)
        {
            ColorValue.ValidatePaint(selectedDayColorValue, nameof(selectedDayColor));
        }

        if (todayMarkerColor is { } todayMarkerColorValue)
        {
            ColorValue.ValidatePaint(todayMarkerColorValue, nameof(todayMarkerColor));
        }

        if (outOfMonthDayColor is { } outOfMonthDayColorValue)
        {
            ColorValue.ValidatePaint(outOfMonthDayColorValue, nameof(outOfMonthDayColor));
        }

        if (weekdayHeaderColor is { } weekdayHeaderColorValue)
        {
            ColorValue.ValidatePaint(weekdayHeaderColorValue, nameof(weekdayHeaderColor));
        }

        if (disabledDayColor is { } disabledDayColorValue)
        {
            ColorValue.ValidatePaint(disabledDayColorValue, nameof(disabledDayColor));
        }

        SelectedDayColor = selectedDayColor;
        TodayMarkerColor = todayMarkerColor;
        OutOfMonthDayColor = outOfMonthDayColor;
        WeekdayHeaderColor = weekdayHeaderColor;
        DisabledDayColor = disabledDayColor;
        ContentInset = contentInset;
        Appearance = appearance;
    }

    /// <summary>Gets the optional replacement foreground for a selected date.</summary>
    public ColorValue? SelectedDayColor { get; }

    /// <summary>Gets the optional replacement foreground for the hovered date or interval preview.</summary>
    public ColorValue? TodayMarkerColor { get; }

    /// <summary>Gets the optional replacement foreground for an out-of-month date.</summary>
    public ColorValue? OutOfMonthDayColor { get; }

    /// <summary>Gets the optional replacement foreground for the weekday row.</summary>
    public ColorValue? WeekdayHeaderColor { get; }

    /// <summary>Gets the optional replacement foreground for a disabled date.</summary>
    public ColorValue? DisabledDayColor { get; }

    /// <summary>Gets the optional replacement internal content inset.</summary>
    public Thickness? ContentInset { get; }

    /// <summary>Gets the optional partial normal and visual-state appearance profile.</summary>
    public AppearanceProfileSet? Appearance { get; }

    /// <summary>Applies this partial contribution to a complete calendar presentation.</summary>
    /// <param name="baseline">The complete presentation that supplies omitted members.</param>
    /// <returns>The validated complete composed presentation.</returns>
    /// <exception cref="ArgumentException">A composed part foreground is transparent.</exception>
    public CalendarStyle Apply(CalendarStyle baseline)
    {
        var appearance = Appearance is null
            ? baseline.Appearance
            : StyleResolution.Apply(baseline.Appearance, Appearance.Value);

        return new CalendarStyle(
            SelectedDayColor ?? baseline.SelectedDayColor,
            TodayMarkerColor ?? baseline.TodayMarkerColor,
            OutOfMonthDayColor ?? baseline.OutOfMonthDayColor,
            WeekdayHeaderColor ?? baseline.WeekdayHeaderColor,
            DisabledDayColor ?? baseline.DisabledDayColor,
            ContentInset ?? baseline.ContentInset,
            appearance);
    }
}
