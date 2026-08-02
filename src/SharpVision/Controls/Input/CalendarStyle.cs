// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines one complete immutable calendar presentation.</summary>
[PublicAPI]
public readonly struct CalendarStyle: IEquatable<CalendarStyle>
{
    private static readonly ThemeProfile _standardAppearance = ControlStyleProfiles.Input;
    private readonly ColorValue? _selectedDayColor;
    private readonly ColorValue? _todayMarkerColor;
    private readonly ColorValue? _outOfMonthDayColor;
    private readonly ColorValue? _weekdayHeaderColor;
    private readonly ColorValue? _disabledDayColor;
    private readonly Thickness? _contentInset;
    private readonly ThemeProfile? _appearance;

    /// <summary>Initializes a complete calendar presentation.</summary>
    /// <param name="selectedDayColor">The non-transparent foreground for a date inside the committed selection.</param>
    /// <param name="todayMarkerColor">The non-transparent foreground for the hovered date or a pending interval preview.</param>
    /// <param name="outOfMonthDayColor">The non-transparent foreground for a date outside the displayed month.</param>
    /// <param name="weekdayHeaderColor">The non-transparent foreground for the abbreviated weekday row.</param>
    /// <param name="disabledDayColor">The non-transparent foreground for a blocked, out-of-range, or disabled date.</param>
    /// <param name="contentInset">The non-negative internal content inset in cells.</param>
    /// <param name="appearance">The complete normal and visual-state appearance profile.</param>
    /// <exception cref="ArgumentException">A part foreground is transparent.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="appearance"/> is <see langword="null"/>.</exception>
    public CalendarStyle(
        ColorValue selectedDayColor,
        ColorValue todayMarkerColor,
        ColorValue outOfMonthDayColor,
        ColorValue weekdayHeaderColor,
        ColorValue disabledDayColor,
        Thickness contentInset,
        ThemeProfile appearance)
    {
        ColorValue.ValidatePaint(selectedDayColor, nameof(selectedDayColor));
        ColorValue.ValidatePaint(todayMarkerColor, nameof(todayMarkerColor));
        ColorValue.ValidatePaint(outOfMonthDayColor, nameof(outOfMonthDayColor));
        ColorValue.ValidatePaint(weekdayHeaderColor, nameof(weekdayHeaderColor));
        ColorValue.ValidatePaint(disabledDayColor, nameof(disabledDayColor));
        ArgumentNullException.ThrowIfNull(appearance);

        _selectedDayColor = selectedDayColor;
        _todayMarkerColor = todayMarkerColor;
        _outOfMonthDayColor = outOfMonthDayColor;
        _weekdayHeaderColor = weekdayHeaderColor;
        _disabledDayColor = disabledDayColor;
        _contentInset = contentInset;
        _appearance = appearance;
    }

    /// <summary>Gets the standard calendar presentation.</summary>
    public static CalendarStyle Default => default;

    /// <summary>Gets the foreground for a date inside the committed selection.</summary>
    public ColorValue SelectedDayColor => _selectedDayColor ?? ThemeColor.SelectedText;

    /// <summary>Gets the foreground for the hovered date or a pending interval preview.</summary>
    public ColorValue TodayMarkerColor => _todayMarkerColor ?? ThemeColor.ActiveText;

    /// <summary>Gets the foreground for a date outside the displayed month.</summary>
    public ColorValue OutOfMonthDayColor => _outOfMonthDayColor ?? ThemeColor.Muted;

    /// <summary>Gets the foreground for the abbreviated weekday row.</summary>
    public ColorValue WeekdayHeaderColor => _weekdayHeaderColor ?? ThemeColor.Muted;

    /// <summary>Gets the foreground for a blocked, out-of-range, or disabled date.</summary>
    public ColorValue DisabledDayColor => _disabledDayColor ?? ThemeColor.DisabledText;

    /// <summary>Gets the internal content inset in terminal cells.</summary>
    public Thickness ContentInset => _contentInset ?? new Thickness(horizontal: 1, vertical: 0);

    /// <summary>Gets the complete normal and visual-state appearance profile.</summary>
    public ThemeProfile Appearance => ResolveAppearance();

    /// <summary>Determines whether this value and another style resolve to the same presentation.</summary>
    /// <param name="other">The other style to compare.</param>
    /// <returns><see langword="true"/> when all resolved presentation members are equal.</returns>
    public bool Equals(CalendarStyle other) =>
        SelectedDayColor == other.SelectedDayColor &&
        TodayMarkerColor == other.TodayMarkerColor &&
        OutOfMonthDayColor == other.OutOfMonthDayColor &&
        WeekdayHeaderColor == other.WeekdayHeaderColor &&
        DisabledDayColor == other.DisabledDayColor &&
        ContentInset == other.ContentInset &&
        Appearance.Equals(other.Appearance);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CalendarStyle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(SelectedDayColor);
        hash.Add(TodayMarkerColor);
        hash.Add(OutOfMonthDayColor);
        hash.Add(WeekdayHeaderColor);
        hash.Add(DisabledDayColor);
        hash.Add(ContentInset);
        hash.Add(Appearance);
        return hash.ToHashCode();
    }

    /// <summary>Determines whether two calendar styles resolve to the same presentation.</summary>
    /// <param name="left">The first style.</param>
    /// <param name="right">The second style.</param>
    /// <returns><see langword="true"/> when the styles resolve equally.</returns>
    public static bool operator ==(CalendarStyle left, CalendarStyle right) => left.Equals(right);

    /// <summary>Determines whether two calendar styles resolve to different presentations.</summary>
    /// <param name="left">The first style.</param>
    /// <param name="right">The second style.</param>
    /// <returns><see langword="true"/> when the styles resolve differently.</returns>
    public static bool operator !=(CalendarStyle left, CalendarStyle right) => !left.Equals(right);

    private ThemeProfile ResolveAppearance() => _appearance ?? _standardAppearance;
}
