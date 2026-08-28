// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Proves culture-aware controls treat a mutable culture instance as configuration
/// identity, including customized clones that retain the same culture name.</summary>
public sealed partial class ControlBaseTests
{
    /// <summary>Verifies DateInput commits and renders a customized equal-named culture clone.</summary>
    [Fact]
    public void Culture_WhenDateInputReceivesCustomizedEqualNamedClone_CommitsOnceAndRendersClone()
    {
        var baseline = new CultureInfo("en-US");
        var customized = (CultureInfo) baseline.Clone();
        customized.DateTimeFormat.ShortDatePattern = "yyyy~MM~dd";
        customized.DateTimeFormat.DateSeparator = "~";
        using var control = new DateInput { Value = new DateOnly(2026, 8, 28), Culture = baseline };
        var changes = CountCultureChanges(control);

        control.Culture = customized;
        control.Culture = customized;

        control.Culture.ShouldBeSameAs(customized);
        changes().ShouldBe(1);
        RenderRow(control, 24).ShouldContain("2026~08~28");
    }

    /// <summary>Verifies TimeInput refreshes separator and designator segments from a customized
    /// equal-named culture clone.</summary>
    [Fact]
    public void Culture_WhenTimeInputReceivesCustomizedEqualNamedClone_CommitsOnceAndRendersClone()
    {
        var baseline = new CultureInfo("en-US");
        var customized = (CultureInfo) baseline.Clone();
        customized.DateTimeFormat.TimeSeparator = "~";
        customized.DateTimeFormat.PMDesignator = "post";
        using var control = new TimeInput
        {
            Value = new TimeOnly(14, 30),
            Use24HourFormat = false,
            Culture = baseline
        };
        var changes = CountCultureChanges(control);

        control.Culture = customized;
        control.Culture = customized;

        control.Culture.ShouldBeSameAs(customized);
        changes().ShouldBe(1);
        var row = RenderRow(control, 24);
        row.ShouldContain("02~30");
        row.ShouldContain("post");
    }

    /// <summary>Verifies DateTimeInput refreshes its segments and retained Calendar from a
    /// customized equal-named culture clone.</summary>
    [Fact]
    public void Culture_WhenDateTimeInputReceivesCustomizedEqualNamedClone_SynchronizesAndRendersClone()
    {
        var baseline = new CultureInfo("en-US");
        var customized = (CultureInfo) baseline.Clone();
        customized.DateTimeFormat.ShortDatePattern = "yyyy~MM~dd";
        customized.DateTimeFormat.DateSeparator = "~";
        customized.DateTimeFormat.TimeSeparator = "!";
        customized.DateTimeFormat.PMDesignator = "post";
        using var control = new DateTimeInput
        {
            Value = new DateTime(2026, 8, 28, 14, 30, 0),
            Use24HourFormat = false,
            Culture = baseline
        };
        var changes = CountCultureChanges(control);

        control.Culture = customized;
        control.Culture = customized;

        control.Culture.ShouldBeSameAs(customized);
        control.OwnedCalendar.Culture.ShouldBeSameAs(customized);
        changes().ShouldBe(1);
        var row = RenderRow(control, 36);
        row.ShouldContain("2026~08~28");
        row.ShouldContain("02!30");
        row.ShouldContain("post");
    }

    /// <summary>Verifies NumberInput refreshes grouping and decimal tokens from a customized
    /// equal-named culture clone.</summary>
    [Fact]
    public void Culture_WhenNumberInputReceivesCustomizedEqualNamedClone_CommitsOnceAndRendersClone()
    {
        var baseline = new CultureInfo("en-US");
        var customized = (CultureInfo) baseline.Clone();
        customized.NumberFormat.NumberGroupSeparator = "_";
        customized.NumberFormat.NumberDecimalSeparator = "~";
        using var control = new NumberInput
        {
            Value = 1234.5m,
            DecimalPlaces = 1,
            Culture = baseline
        };
        var changes = CountCultureChanges(control);

        control.Culture = customized;
        control.Culture = customized;

        control.Culture.ShouldBeSameAs(customized);
        changes().ShouldBe(1);
        RenderRow(control, 24).ShouldContain("1_234~5");
    }

    /// <summary>Verifies CurrencyInput refreshes its symbol, grouping, and decimal tokens from a
    /// customized equal-named culture clone.</summary>
    [Fact]
    public void Culture_WhenCurrencyInputReceivesCustomizedEqualNamedClone_CommitsOnceAndRendersClone()
    {
        var baseline = new CultureInfo("en-US");
        var customized = (CultureInfo) baseline.Clone();
        customized.NumberFormat.CurrencySymbol = "USD$";
        customized.NumberFormat.CurrencyGroupSeparator = "_";
        customized.NumberFormat.CurrencyDecimalSeparator = "~";
        using var control = new CurrencyInput
        {
            Value = 1234.5m,
            DecimalPlaces = 1,
            Culture = baseline
        };
        var changes = CountCultureChanges(control);

        control.Culture = customized;
        control.Culture = customized;

        control.Culture.ShouldBeSameAs(customized);
        changes().ShouldBe(1);
        var row = RenderRow(control, 28);
        row.ShouldContain("USD$");
        row.ShouldContain("1_234~5");
    }

    /// <summary>Verifies Calendar refreshes first-day and weekday presentation from a customized
    /// equal-named culture clone.</summary>
    [Fact]
    public void Culture_WhenCalendarReceivesCustomizedEqualNamedClone_CommitsOnceAndRendersClone()
    {
        var baseline = new CultureInfo("en-US");
        var customized = (CultureInfo) baseline.Clone();
        customized.DateTimeFormat.FirstDayOfWeek = DayOfWeek.Monday;
        customized.DateTimeFormat.AbbreviatedDayNames = ["Su", "M1", "T2", "W3", "T4", "F5", "S6"];
        using var control = new UiCalendar
        {
            Culture = baseline,
            DisplayMonth = new DateOnly(2026, 8, 1)
        };
        var changes = CountCultureChanges(control);

        control.Culture = customized;
        control.Culture = customized;

        control.Culture.ShouldBeSameAs(customized);
        control.FirstDayOfWeek.ShouldBe(DayOfWeek.Monday);
        changes().ShouldBe(1);
        RenderRow(control, 32, 10, 2).ShouldBe("┃  M1  T2  W3  T4  F5  S6  Su  ┃");
    }

    private static Func<int> CountCultureChanges(ControlBase control)
    {
        var changes = 0;
        control.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == "Culture")
            {
                changes++;
            }
        };
        return () => changes;
    }

    private static string RenderRow(ControlBase control, int width, int height = 3, int row = 1)
    {
        new LayoutEngine().Layout(control, new Size(width, height));
        using Frame frame = new(new Size(width, height));
        control.Render(frame.Canvas);
        var result = new StringBuilder(width);

        for (var x = 0; x < width; x++)
        {
            var text = FrameOracle.Get(frame, new Point(x, row));
            _ = result.Append(text.Length == 0 ? " " : text);
        }

        return result.ToString();
    }
}
