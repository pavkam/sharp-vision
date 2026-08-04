// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves the detached public DateTimeInput contract, clamping, and rendering.</summary>
public sealed class DateTimeInputTests
{
    #region Properties

    /// <summary>Verifies a null value is accepted when AllowNull is enabled.</summary>
    [ComponentUnitEvidence(typeof(DateTimeInput))]
    [Fact]
    public void Properties_WhenValueIsNull_AllowsNullWhenEnabled()
    {
        // Arrange
        using var control = new DateTimeInput { AllowNull = true };

        // Act
        control.Value = null;

        // Assert
        control.Value.ShouldBeNull();
    }

    /// <summary>Verifies disabling null support repairs an already-cleared value.</summary>
    [Fact]
    public void AllowNull_WhenDisabledAfterValueWasCleared_RepairsValue()
    {
        // Arrange
        using var control = new DateTimeInput { Value = null };

        // Act
        control.AllowNull = false;

        // Assert
        _ = control.Value.ShouldNotBeNull();
    }

    /// <summary>Verifies the value is clamped to the minimum when set below the allowed range.</summary>
    [Fact]
    public void Properties_WhenMinMaxAreSet_ClampsValue()
    {
        // Arrange
        using var control = new DateTimeInput
        {
            MinimumValue = new DateTime(2026, 7, 15, 10, 0, 0),
            MaximumValue = new DateTime(2026, 7, 25, 14, 0, 0)
        };

        // Act
        control.Value = new DateTime(2026, 7, 10, 8, 0, 0);

        // Assert
        control.Value.ShouldBe(new DateTime(2026, 7, 15, 10, 0, 0));
    }

    /// <summary>Verifies the configurable minute step is applied by the inline minute segment.</summary>
    [Fact]
    public void Input_WhenMinuteStepIsConfigured_ArrowAdjustsByConfiguredMinutes()
    {
        using var control = new DateTimeInput
        {
            Value = new DateTime(2026, 7, 19, 10, 10, 0),
            TimeStep = TimeSpan.FromMinutes(15)
        };

        for (var i = 0; i < 4; i++)
        {
            _ = Router.Route(control, Events.Key, Key(Code.Right));
        }

        _ = Router.Route(control, Events.Key, Key(Code.Up));

        control.Value.ShouldBe(new DateTime(2026, 7, 19, 10, 25, 0));
    }

    /// <summary>Verifies clock adjustment at the DateTime range edge stays bounded.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Input_WhenHourAdjustmentCrossesDateTimeRange_PreservesBound(bool maximum)
    {
        // Arrange
        var bound = maximum ? DateTime.MaxValue : DateTime.MinValue;
        var code = maximum ? Code.Up : Code.Down;
        using var control = new DateTimeInput { Value = bound };

        for (var index = 0; index < 3; index++)
        {
            _ = Router.Route(control, Events.Key, Key(Code.Right));
        }

        // Act and assert
        Should.NotThrow(() =>
        {
            _ = Router.Route(control, Events.Key, Key(code));
        });
        control.Value.ShouldBe(bound);
    }

    /// <summary>Verifies IsOpen publishes PropertyChanged on open as well as on close, instead of
    /// only republishing the private Popup's Closed notification (see #191).</summary>
    [Fact]
    public async Task IsOpen_WhenChanged_PublishesPropertyChangedOnBothTransitionsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var control = new DateTimeInput();
            root.Children.Add(control);
            new LayoutEngine().Layout(root, new Size(20, 10));
            root.Attach(dispatcher);
            var notifications = new List<bool>();
            control.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(DateTimeInput.IsOpen))
                {
                    notifications.Add(control.IsOpen);
                }
            };

            control.IsOpen = true;
            control.IsOpen = false;

            notifications.ShouldBe([true, false]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies DropDownOpened fires when the Calendar popup opens and DropDownClosed
    /// fires when it closes, matching ComboBox's and DateInput's drop-down event shape (see #69).</summary>
    [Fact]
    public async Task DropDownOpened_WhenDropDownOpens_FiresEventAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var control = new DateTimeInput();
            root.Children.Add(control);
            new LayoutEngine().Layout(root, new Size(20, 10));
            root.Attach(dispatcher);
            var opened = 0;
            var closed = 0;
            control.DropDownOpened += (_, _) => opened++;
            control.DropDownClosed += (_, _) => closed++;

            control.IsOpen = true;

            opened.ShouldBe(1);
            closed.ShouldBe(0);

            control.IsOpen = false;

            opened.ShouldBe(1);
            closed.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies DropDownHeight rejects a non-positive value and otherwise bounds the
    /// measured Calendar popup height, matching DateInput's and ComboBox's identically named
    /// property (see #69).</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Properties_WhenDropDownHeightIsNonPositive_ThrowsArgumentOutOfRangeException(int value)
    {
        // Arrange
        using var control = new DateTimeInput();

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.DropDownHeight = value);
    }

    /// <summary>Verifies DropDownHeight round-trips a valid value.</summary>
    [Fact]
    public void Properties_WhenDropDownHeightIsSet_RoundTrips()
    {
        // Arrange
        using var control = new DateTimeInput();

        // Act
        control.DropDownHeight = 4;

        // Assert
        control.DropDownHeight.ShouldBe(4);
    }

    /// <summary>Verifies PopupBorder applies to the owned Calendar popup without leaking it, and
    /// ResetPopupBorder returns it to the ThemeRole.Popup appearance (see #81).</summary>
    [Fact]
    public void PopupBorder_WhenSetAndReset_AppliesToOwnedPopupAndReturnsToThemeRole()
    {
        // Arrange
        using var control = new DateTimeInput();
        var popup = OwnedTree.Find<Popup>(control).ShouldNotBeNull();
        var themeRoleBorder = popup.Border;
        var border = new Border(BorderSide.All, BorderGlyphStyle.Rounded, Color.Rgb(65, 43, 21), Color.Transparent, TerminalAttributes.None);

        // Act
        control.PopupBorder = border;

        // Assert
        popup.Border.ShouldBe(border);

        // Act
        control.ResetPopupBorder();

        // Assert
        control.PopupBorder.ShouldBeNull();
        popup.Border.ShouldBe(themeRoleBorder);
    }

    #endregion

    #region Commit

    /// <summary>Verifies the ValueChanged event fires with correct previous and current values.</summary>
    [Fact]
    public void Commit_WhenValueChanges_RaisesValueChanged()
    {
        // Arrange
        using var control = new DateTimeInput
        {
            Value = new DateTime(2026, 7, 10, 10, 0, 0)
        };
        DateTimeInputValueChangedEventArgs? observed = null;
        control.ValueChanged += (_, eventArgs) => observed = eventArgs;

        // Act
        control.Value = new DateTime(2026, 7, 19, 14, 30, 0);

        // Assert
        _ = observed.ShouldNotBeNull();
        observed.PreviousValue.ShouldBe(new DateTime(2026, 7, 10, 10, 0, 0));
        observed.Value.ShouldBe(new DateTime(2026, 7, 19, 14, 30, 0));
    }

    /// <summary>Verifies selecting a date from the popup preserves the value's time-zone interpretation.</summary>
    [Fact]
    public void Popup_WhenDateIsSelected_PreservesDateTimeKind()
    {
        // Arrange
        using var control = new DateTimeInput
        {
            Value = new DateTime(2026, 7, 19, 14, 30, 0, DateTimeKind.Utc),
            IsOpen = true
        };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Enter));

        // Assert
        _ = control.Value.ShouldNotBeNull();
        control.Value.Value.Date.ShouldBe(new DateTime(2026, 7, 20));
        control.Value.Value.Kind.ShouldBe(DateTimeKind.Utc);
    }

    #endregion

    #region Culture

    /// <summary>Verifies the default culture is invariant, so out-of-the-box rendering never
    /// depends on the host operating system's locale.</summary>
    [Fact]
    public void Culture_WhenControlIsConstructed_DefaultsToInvariant()
    {
        // Arrange
        using var control = new DateTimeInput();

        // Assert
        control.Culture.ShouldBeSameAs(CultureInfo.InvariantCulture);
    }

    /// <summary>Verifies assigning a culture whose active calendar is not Gregorian is rejected.</summary>
    [Fact]
    public void Culture_WhenCalendarIsNotGregorian_Throws()
    {
        // Arrange
        using var control = new DateTimeInput();

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => control.Culture = new CultureInfo("ar-SA"));
    }

    #endregion

    #region Rendering

    /// <summary>Verifies a German culture draws the typed field's date segments in
    /// day-month-year order with a period separator, instead of the fixed month-day-year slash
    /// order. This is the culture-order bug the segmented-field engine extraction fixes (see #69):
    /// before the fix, <see cref="DateTimeInput.Culture"/> affected only the popup calendar, and
    /// the typed field's order/digits/separators were always fixed regardless of this value.</summary>
    [Fact]
    public void Render_WhenCultureIsGermanGregorian_DrawsDayMonthYearSegmentsWithLocalizedSeparators()
    {
        // Arrange
        using var control = new DateTimeInput
        {
            Culture = new CultureInfo("de-DE"),
            Value = new DateTime(2026, 7, 19, 14, 30, 0)
        };
        new LayoutEngine().Layout(control, new Size(30, 3));
        using Frame frame = new(new Size(30, 3));

        // Act
        control.Render(frame.Canvas);

        // Assert — German ShortDatePattern is "dd.MM.yyyy": day before month, period separator.
        var row = Row(frame, 1);
        row.ShouldContain("19.07.2026");
        row.ShouldNotContain("07/19/2026");
    }

    /// <summary>Verifies a Finnish culture localizes the time portion's separator and AM/PM
    /// designator text, in addition to the date portion's order (see #69).</summary>
    [Fact]
    public void Render_WhenCultureIsFinnishAndTwelveHour_DrawsLocalizedTimeSeparatorAndDesignator()
    {
        // Arrange
        using var control = new DateTimeInput
        {
            Culture = new CultureInfo("fi-FI"),
            Use24HourFormat = false,
            Value = new DateTime(2026, 7, 19, 14, 30, 0)
        };
        new LayoutEngine().Layout(control, new Size(30, 3));
        using Frame frame = new(new Size(30, 3));

        // Act
        control.Render(frame.Canvas);

        // Assert — Finnish ShortDatePattern is "d.M.yyyy" and TimeSeparator is ".",
        // with AM/PM designators "ap."/"ip.".
        var row = Row(frame, 1);
        row.ShouldContain("19.7.2026");
        row.ShouldContain("02.30");
        row.ShouldContain("ip.");
    }

    /// <summary>Verifies a set date-time renders both date and time formatted text inside the border.</summary>
    [Fact]
    public void Render_WhenValueIsSet_DrawsFormattedDateTime()
    {
        // Arrange
        using var control = new DateTimeInput
        {
            Value = new DateTime(2026, 7, 19, 14, 30, 0)
        };
        new LayoutEngine().Layout(control, new Size(30, 3));
        using Frame frame = new(new Size(30, 3));

        // Act
        control.Render(frame.Canvas);

        // Assert — DrawSegments renders "07/19/2026 14:30" with border.
        var row = Row(frame, 1);
        row.ShouldContain("07/19/2026");
        row.ShouldContain("14:30");
    }

    /// <summary>Verifies a null value renders the full placeholder dashes inside the border.</summary>
    [Fact]
    public void Render_WhenValueIsNull_DrawsPlaceholder()
    {
        // Arrange
        using var control = new DateTimeInput { AllowNull = true };
        control.Value = null;
        new LayoutEngine().Layout(control, new Size(30, 3));
        using Frame frame = new(new Size(30, 3));

        // Act
        control.Render(frame.Canvas);

        // Assert — default placeholder: "--/--/---- --:--"
        var row = Row(frame, 1);
        row.ShouldContain("--/--/----");
        row.ShouldContain("--:--");
    }

    #endregion

    #region Helpers

    private static string Row(Frame frame, int y)
    {
        var result = new StringBuilder(frame.Size.Width);

        for (var x = 0; x < frame.Size.Width; x++)
        {
            var text = FrameOracle.Get(frame, new Point(x, y));
            _ = result.Append(text.Length == 0 ? " " : text);
        }

        return result.ToString();
    }

    private static KeyEventArgs Key(Code code) => new(new Stroke(
        code,
        default,
        nativeCode: 0,
        Modifiers.None,
        KeyAction.Press));

    private static KeyEventArgs CharacterKey(char character) => new(new Stroke(
        Code.Character,
        new Rune(character),
        nativeCode: 0,
        Modifiers.None,
        KeyAction.Press));

    #endregion

    #region Typing

    /// <summary>Verifies typing four digits on the Year segment produces a year above 99
    /// instead of committing after two digits and misapplying the rest to Hour.</summary>
    [Fact]
    public void TypeDigit_WhenFourDigitsTypedOnYearSegment_ProducesFullYear()
    {
        // Arrange
        using var control = new DateTimeInput { Value = new DateTime(2020, 1, 1, 0, 0, 0) };

        // Act: focus starts on Month (segment 0); Month/Day/Year ordering means two
        // Right presses reach Year.
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, CharacterKey('2'));
        _ = Router.Route(control, Events.Key, CharacterKey('0'));
        _ = Router.Route(control, Events.Key, CharacterKey('2'));
        _ = Router.Route(control, Events.Key, CharacterKey('6'));

        // Assert
        control.Value.ShouldNotBeNull().Year.ShouldBe(2026);
    }

    #endregion
}
