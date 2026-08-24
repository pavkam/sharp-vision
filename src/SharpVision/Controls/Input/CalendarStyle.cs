// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable calendar presentation. This style's own
/// "calendar" theme key falls back to <see cref="InputStyle"/>'s "input" key.</summary>
[PublicAPI]
public sealed record CalendarStyle: InputStyle
{
    /// <summary>Gets the primary calendar-style definition.</summary>
    internal static StyleDefinition<CalendarStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(InputStyle.Default),
        Complete,
        static (previous, previousTheme, current, currentTheme) =>
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
                    : InvalidationImpact.None);

    private static CalendarStyle Complete(InputStyle input, VisualState state, Theme theme) =>
        new(
            input.Face,
            input.Border,
            input.Shadow,
            SemanticColor.SelectedText,
            SemanticColor.ActiveText,
            SemanticColor.Muted,
            SemanticColor.Muted,
            SemanticColor.DisabledText,
            SemanticColor.Accent,
            ControlGlyphs.Calendar.PreviousMonth.Value,
            ControlGlyphs.Calendar.NextMonth.Value,
            SemanticColor.ActiveControl,
            SemanticColor.SelectedControl,
            SemanticColor.DisabledControl,
            new Thickness(horizontal: 1, vertical: 0))
        {
            // Forwarded from the fallback rather than left at the code-owned value the base
            // constructor supplies. Without this the member is inherited into this style's own
            // theme key, accepted by the parser, and silently ignored - the exact
            // blessed-but-unread section themes.md says the design exists to prevent.
            //
            // AffixGap is deliberately NOT forwarded here: docs/concepts/styling.md documents it
            // as exposed "on each hosting style type" - Calendar has no StartAffix/EndAffix of
            // its own (unlike Button/TextInput/etc.) and is not a text-field-like control that
            // could plausibly grow one, so there is no hosting style to forward the value into.
            DropDownGlyph = input.DropDownGlyph
        };

    /// <summary>Initializes a complete calendar presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="selectedDayColor">The non-transparent foreground for a date inside the committed selection.</param>
    /// <param name="todayMarkerColor">The non-transparent foreground for today's date, and for the hovered date or a pending interval preview.</param>
    /// <param name="outOfMonthDayColor">The non-transparent foreground for a date outside the displayed month.</param>
    /// <param name="weekdayHeaderColor">The non-transparent foreground for the abbreviated weekday row.</param>
    /// <param name="disabledDayColor">The non-transparent foreground for a blocked, out-of-range, or disabled date.</param>
    /// <param name="previousMonthGlyph">The printable one-cell previous-month arrow.</param>
    /// <param name="nextMonthGlyph">The printable one-cell next-month arrow.</param>
    /// <param name="navigationColor">The non-transparent foreground for month navigation arrows.</param>
    /// <param name="activeDayBackground">The non-transparent background for hover and interval preview.</param>
    /// <param name="selectedDayBackground">The non-transparent background for committed selection.</param>
    /// <param name="disabledDayBackground">The non-transparent background for unavailable dates.</param>
    /// <param name="contentInset">The non-negative internal content inset in cells.</param>
    /// <exception cref="ArgumentException">A part foreground is transparent.</exception>
    [SetsRequiredMembers]
    public CalendarStyle(
        Face face,
        Border border,
        Shadow shadow,
        ControlColor selectedDayColor,
        ControlColor todayMarkerColor,
        ControlColor outOfMonthDayColor,
        ControlColor weekdayHeaderColor,
        ControlColor disabledDayColor,
        ControlColor navigationColor,
        Rune previousMonthGlyph,
        Rune nextMonthGlyph,
        ControlColor activeDayBackground,
        ControlColor selectedDayBackground,
        ControlColor disabledDayBackground,
        Thickness contentInset) : base(face, border, shadow, InputStyle.Default.DropDownGlyph)
    {
        // Validated by parameter name here as well as in each init accessor. The accessor guards
        // the doors a constructor cannot see - a `with` expression and the theme overlay's
        // reflective write - but it only ever knows the value as "value", which for a constructor
        // taking several channels of the same type identifies nothing.
        ControlColor.ValidatePaint(selectedDayColor, nameof(selectedDayColor));
        ControlColor.ValidatePaint(todayMarkerColor, nameof(todayMarkerColor));
        ControlColor.ValidatePaint(outOfMonthDayColor, nameof(outOfMonthDayColor));
        ControlColor.ValidatePaint(weekdayHeaderColor, nameof(weekdayHeaderColor));
        ControlColor.ValidatePaint(disabledDayColor, nameof(disabledDayColor));
        ControlColor.ValidatePaint(navigationColor, nameof(navigationColor));
        ControlColor.ValidatePaint(activeDayBackground, nameof(activeDayBackground));
        ControlColor.ValidatePaint(selectedDayBackground, nameof(selectedDayBackground));
        ControlColor.ValidatePaint(disabledDayBackground, nameof(disabledDayBackground));

        SelectedDayColor = selectedDayColor;
        TodayMarkerColor = todayMarkerColor;
        OutOfMonthDayColor = outOfMonthDayColor;
        WeekdayHeaderColor = weekdayHeaderColor;
        DisabledDayColor = disabledDayColor;
        NavigationColor = navigationColor;
        PreviousMonthGlyph = previousMonthGlyph;
        NextMonthGlyph = nextMonthGlyph;
        ActiveDayBackground = activeDayBackground;
        SelectedDayBackground = selectedDayBackground;
        DisabledDayBackground = disabledDayBackground;
        ContentInset = contentInset;
    }

    /// <summary>Gets the standard calendar presentation.</summary>
    public static new CalendarStyle Default { get; } = Complete(InputStyle.Default, VisualState.Normal, Theme.Unthemed);

    /// <summary>Gets the foreground for a date inside the committed selection.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor SelectedDayColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the foreground for today's date, and for the hovered date or a pending
    /// interval preview.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor TodayMarkerColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the foreground for a date outside the displayed month.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor OutOfMonthDayColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the foreground for the abbreviated weekday row.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor WeekdayHeaderColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the foreground for a blocked, out-of-range, or disabled date.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor DisabledDayColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the month-navigation arrow foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor NavigationColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the arrow drawn for the previous-month affordance.</summary>
    /// <remarks>
    /// <c>NavigationColor</c> exists precisely to paint this affordance, so the style had already
    /// accepted that the part is theme-owned - it just stopped at the colour.
    /// </remarks>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune PreviousMonthGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the arrow drawn for the next-month affordance.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune NextMonthGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the hover and interval-preview background.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor ActiveDayBackground
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the committed-selection background.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor SelectedDayBackground
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the unavailable-date background.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor DisabledDayBackground
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the internal content inset in terminal cells.</summary>
    public required Thickness ContentInset { get; init; }
}
