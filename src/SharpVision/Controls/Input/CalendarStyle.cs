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
    private readonly ColorValue? _navigationColor;
    private readonly ColorValue? _activeDayBackground;
    private readonly ColorValue? _selectedDayBackground;
    private readonly ColorValue? _disabledDayBackground;
    private readonly Thickness? _contentInset;
    private readonly ThemeProfile? _appearance;

    /// <summary>Gets the primary calendar-style definition.</summary>
    internal static StyleDefinition<CalendarStyle> Definition { get; } = StyleDefinitions.Control(
        ThemeRole.Input,
        static profile => new CalendarStyle(
            Default.SelectedDayColor,
            Default.TodayMarkerColor,
            Default.OutOfMonthDayColor,
            Default.WeekdayHeaderColor,
            Default.DisabledDayColor,
            Default.NavigationColor,
            Default.ActiveDayBackground,
            Default.SelectedDayBackground,
            Default.DisabledDayBackground,
            Default.ContentInset,
            profile),
        static style => style.Appearance,
        Compare);

    /// <summary>Initializes a complete calendar presentation.</summary>
    /// <param name="selectedDayColor">The non-transparent foreground for a date inside the committed selection.</param>
    /// <param name="todayMarkerColor">The non-transparent foreground for the hovered date or a pending interval preview.</param>
    /// <param name="outOfMonthDayColor">The non-transparent foreground for a date outside the displayed month.</param>
    /// <param name="weekdayHeaderColor">The non-transparent foreground for the abbreviated weekday row.</param>
    /// <param name="disabledDayColor">The non-transparent foreground for a blocked, out-of-range, or disabled date.</param>
    /// <param name="navigationColor">The non-transparent foreground for month navigation arrows.</param>
    /// <param name="activeDayBackground">The non-transparent background for hover and interval preview.</param>
    /// <param name="selectedDayBackground">The non-transparent background for committed selection.</param>
    /// <param name="disabledDayBackground">The non-transparent background for unavailable dates.</param>
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
        ColorValue navigationColor,
        ColorValue activeDayBackground,
        ColorValue selectedDayBackground,
        ColorValue disabledDayBackground,
        Thickness contentInset,
        ThemeProfile appearance)
    {
        ColorValue.ValidatePaint(selectedDayColor, nameof(selectedDayColor));
        ColorValue.ValidatePaint(todayMarkerColor, nameof(todayMarkerColor));
        ColorValue.ValidatePaint(outOfMonthDayColor, nameof(outOfMonthDayColor));
        ColorValue.ValidatePaint(weekdayHeaderColor, nameof(weekdayHeaderColor));
        ColorValue.ValidatePaint(disabledDayColor, nameof(disabledDayColor));
        ColorValue.ValidatePaint(navigationColor, nameof(navigationColor));
        ColorValue.ValidatePaint(activeDayBackground, nameof(activeDayBackground));
        ColorValue.ValidatePaint(selectedDayBackground, nameof(selectedDayBackground));
        ColorValue.ValidatePaint(disabledDayBackground, nameof(disabledDayBackground));
        ArgumentNullException.ThrowIfNull(appearance);

        _selectedDayColor = selectedDayColor;
        _todayMarkerColor = todayMarkerColor;
        _outOfMonthDayColor = outOfMonthDayColor;
        _weekdayHeaderColor = weekdayHeaderColor;
        _disabledDayColor = disabledDayColor;
        _navigationColor = navigationColor;
        _activeDayBackground = activeDayBackground;
        _selectedDayBackground = selectedDayBackground;
        _disabledDayBackground = disabledDayBackground;
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

    /// <summary>Gets the month-navigation arrow foreground.</summary>
    public ColorValue NavigationColor => _navigationColor ?? ThemeColor.Accent;

    /// <summary>Gets the hover and interval-preview background.</summary>
    public ColorValue ActiveDayBackground => _activeDayBackground ?? ThemeColor.ActiveControl;

    /// <summary>Gets the committed-selection background.</summary>
    public ColorValue SelectedDayBackground => _selectedDayBackground ?? ThemeColor.SelectedControl;

    /// <summary>Gets the unavailable-date background.</summary>
    public ColorValue DisabledDayBackground => _disabledDayBackground ?? ThemeColor.DisabledControl;

    /// <summary>Gets the internal content inset in terminal cells.</summary>
    public Thickness ContentInset => _contentInset ?? new Thickness(horizontal: 1, vertical: 0);

    /// <summary>Gets the complete normal and visual-state appearance profile.</summary>
    public ThemeProfile Appearance => ResolveAppearance();

    /// <summary>Creates a validated copy with selected members replaced.</summary>
    /// <param name="selectedDayColor">The optional selected-date foreground.</param>
    /// <param name="todayMarkerColor">The optional today-marker foreground.</param>
    /// <param name="outOfMonthDayColor">The optional out-of-month foreground.</param>
    /// <param name="weekdayHeaderColor">The optional weekday-header foreground.</param>
    /// <param name="disabledDayColor">The optional disabled-date foreground.</param>
    /// <param name="navigationColor">The optional navigation-arrow foreground.</param>
    /// <param name="activeDayBackground">The optional hover and preview background.</param>
    /// <param name="selectedDayBackground">The optional selection background.</param>
    /// <param name="disabledDayBackground">The optional unavailable-date background.</param>
    /// <param name="contentInset">The optional content inset.</param>
    /// <param name="appearance">The optional appearance-profile overlay.</param>
    /// <returns>A complete validated calendar style.</returns>
    /// <exception cref="ArgumentException">A composed foreground is transparent.</exception>
    public CalendarStyle With(
        ColorValue? selectedDayColor = null,
        ColorValue? todayMarkerColor = null,
        ColorValue? outOfMonthDayColor = null,
        ColorValue? weekdayHeaderColor = null,
        ColorValue? disabledDayColor = null,
        ColorValue? navigationColor = null,
        ColorValue? activeDayBackground = null,
        ColorValue? selectedDayBackground = null,
        ColorValue? disabledDayBackground = null,
        Thickness? contentInset = null,
        AppearanceProfileSet? appearance = null) => new(
        selectedDayColor ?? SelectedDayColor,
        todayMarkerColor ?? TodayMarkerColor,
        outOfMonthDayColor ?? OutOfMonthDayColor,
        weekdayHeaderColor ?? WeekdayHeaderColor,
        disabledDayColor ?? DisabledDayColor,
        navigationColor ?? NavigationColor,
        activeDayBackground ?? ActiveDayBackground,
        selectedDayBackground ?? SelectedDayBackground,
        disabledDayBackground ?? DisabledDayBackground,
        contentInset ?? ContentInset,
        appearance is null ? Appearance : StyleResolution.Apply(Appearance, appearance.Value));

    /// <summary>Determines whether this value and another style resolve to the same presentation.</summary>
    /// <param name="other">The other style to compare.</param>
    /// <returns><see langword="true"/> when all resolved presentation members are equal.</returns>
    public bool Equals(CalendarStyle other) =>
        SelectedDayColor == other.SelectedDayColor &&
        TodayMarkerColor == other.TodayMarkerColor &&
        OutOfMonthDayColor == other.OutOfMonthDayColor &&
        WeekdayHeaderColor == other.WeekdayHeaderColor &&
        DisabledDayColor == other.DisabledDayColor &&
        NavigationColor == other.NavigationColor &&
        ActiveDayBackground == other.ActiveDayBackground &&
        SelectedDayBackground == other.SelectedDayBackground &&
        DisabledDayBackground == other.DisabledDayBackground &&
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
        hash.Add(NavigationColor);
        hash.Add(ActiveDayBackground);
        hash.Add(SelectedDayBackground);
        hash.Add(DisabledDayBackground);
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

    private static InvalidationImpact Compare(
        CalendarStyle previous,
        Theme? previousTheme,
        CalendarStyle current,
        Theme? currentTheme) =>
        previous.ContentInset != current.ContentInset
            ? InvalidationImpact.Measure
            : previous != current ||
              ControlBase.ResolveColor(previous.SelectedDayColor, previousTheme) != ControlBase.ResolveColor(current.SelectedDayColor, currentTheme) ||
              ControlBase.ResolveColor(previous.TodayMarkerColor, previousTheme) != ControlBase.ResolveColor(current.TodayMarkerColor, currentTheme) ||
              ControlBase.ResolveColor(previous.OutOfMonthDayColor, previousTheme) != ControlBase.ResolveColor(current.OutOfMonthDayColor, currentTheme) ||
              ControlBase.ResolveColor(previous.WeekdayHeaderColor, previousTheme) != ControlBase.ResolveColor(current.WeekdayHeaderColor, currentTheme) ||
              ControlBase.ResolveColor(previous.DisabledDayColor, previousTheme) != ControlBase.ResolveColor(current.DisabledDayColor, currentTheme) ||
              ControlBase.ResolveColor(previous.NavigationColor, previousTheme) != ControlBase.ResolveColor(current.NavigationColor, currentTheme) ||
              ControlBase.ResolveColor(previous.ActiveDayBackground, previousTheme) != ControlBase.ResolveColor(current.ActiveDayBackground, currentTheme) ||
              ControlBase.ResolveColor(previous.SelectedDayBackground, previousTheme) != ControlBase.ResolveColor(current.SelectedDayBackground, currentTheme) ||
              ControlBase.ResolveColor(previous.DisabledDayBackground, previousTheme) != ControlBase.ResolveColor(current.DisabledDayBackground, currentTheme)
                ? InvalidationImpact.Render
                : InvalidationImpact.None;
}
