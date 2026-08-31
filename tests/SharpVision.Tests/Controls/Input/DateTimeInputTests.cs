// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;


/// <summary>Proves the detached public DateTimeInput contract, clamping, and rendering.</summary>
public sealed class DateTimeInputTests
{
    /// <summary>Verifies the retained Calendar receives the newest culture committed from owner
    /// publication instead of the outer setter's captured culture.</summary>
    [Fact]
    public void Culture_WhenPropertyObserverCommitsNewerCulture_SynchronizesOwnedCalendar()
    {
        var outer = CultureInfo.GetCultureInfo("en-US");
        var nested = CultureInfo.GetCultureInfo("de-DE");
        using var input = new DateTimeInput();
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(DateTimeInput.Culture) && input.Culture.Equals(outer))
            {
                input.Culture = nested;
            }
        };

        input.Culture = outer;

        input.Culture.ShouldBe(nested);
        input.OwnedCalendar.Culture.ShouldBe(nested);
    }
    /// <summary>Verifies reentrant value publication suppresses the superseded typed event.</summary>
    [Fact]
    public void Value_WhenPropertyObserverCommitsNewerValue_SuppressesStaleTypedEvent()
    {
        var initial = new DateTime(2026, 8, 1, 8, 0, 0);
        var first = new DateTime(2026, 8, 10, 9, 0, 0);
        var second = new DateTime(2026, 8, 20, 10, 0, 0);
        using var input = new DateTimeInput { Value = initial };
        var observations = new List<(DateTime? EventValue, DateTime? LiveValue)>();
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(DateTimeInput.Value) && input.Value == first)
            {
                input.Value = second;
            }
        };
        input.ValueChanged += (_, eventArgs) => observations.Add((eventArgs.Value, input.Value));

        input.Value = first;

        observations.ShouldBe([(second, second)]);
    }

    /// <summary>Verifies a reentrant AllowNull restoration prevents obsolete eager seeding.</summary>
    [Fact]
    public void AllowNull_WhenPropertyObserverRestoresTrue_PreservesNullValue()
    {
        using var input = new DateTimeInput { Value = null };
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(DateTimeInput.AllowNull) && !input.AllowNull)
            {
                input.AllowNull = true;
            }
        };

        input.AllowNull = false;

        input.AllowNull.ShouldBeTrue();
        input.Value.ShouldBeNull();
    }

    /// <summary>Verifies both date-time endpoints repair the value and retained calendar after a throwing observer.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Bounds_WhenPropertyObserverThrows_StillRepairDependentState(bool minimum)
    {
        // Arrange
        var value = minimum ? new DateTime(2026, 7, 10, 10, 0, 0) : new DateTime(2026, 7, 20, 10, 0, 0);
        var bound = minimum ? new DateTime(2026, 7, 12, 12, 0, 0) : new DateTime(2026, 7, 18, 12, 0, 0);
        using var control = new DateTimeInput { Value = value };
        var propertyName = minimum ? nameof(DateTimeInput.Minimum) : nameof(DateTimeInput.Maximum);
        control.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == propertyName)
            {
                throw new InvalidOperationException("observer failure");
            }
        };

        // Act
        _ = Should.Throw<InvalidOperationException>(() =>
        {
            if (minimum)
            {
                control.Minimum = bound;
            }
            else
            {
                control.Maximum = bound;
            }
        });

        // Assert
        control.Value.ShouldBe(bound);
        control.OwnedCalendar.MinimumDate.ShouldBe(DateOnly.FromDateTime(control.Minimum));
        control.OwnedCalendar.MaximumDate.ShouldBe(DateOnly.FromDateTime(control.Maximum));
    }

    #region Properties

    /// <summary>Verifies a null value is accepted when AllowNull is enabled.</summary>
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
            Minimum = new DateTime(2026, 7, 15, 10, 0, 0),
            Maximum = new DateTime(2026, 7, 25, 14, 0, 0)
        };

        // Act
        control.Value = new DateTime(2026, 7, 10, 8, 0, 0);

        // Assert
        control.Value.ShouldBe(new DateTime(2026, 7, 15, 10, 0, 0));
    }

    /// <summary>Verifies Minimum and Maximum default to DateTime.MinValue and DateTime.MaxValue.</summary>
    [Fact]
    public void Properties_WhenConstructed_MinimumAndMaximumDefaultToFullRange()
    {
        // Arrange
        using var control = new DateTimeInput();

        // Assert
        control.Minimum.ShouldBe(DateTime.MinValue);
        control.Maximum.ShouldBe(DateTime.MaxValue);
    }

    /// <summary>Verifies raising Minimum above the committed value repairs it by clamping and
    /// raises ValueChanged with the exact previous and new committed date-times.</summary>
    [Fact]
    public void Minimum_WhenRaisedAboveCommittedValue_RepairsByClampingAndRaisesValueChanged()
    {
        // Arrange
        using var control = new DateTimeInput { Value = new DateTime(2026, 7, 10, 8, 0, 0) };
        DateTimeInputValueChangedEventArgs? change = null;
        control.ValueChanged += (_, args) => change = args;

        // Act
        control.Minimum = new DateTime(2026, 7, 15, 10, 0, 0);

        // Assert
        control.Value.ShouldBe(new DateTime(2026, 7, 15, 10, 0, 0));
        var raised = change.ShouldNotBeNull();
        raised.PreviousValue.ShouldBe(new DateTime(2026, 7, 10, 8, 0, 0));
        raised.Value.ShouldBe(new DateTime(2026, 7, 15, 10, 0, 0));
    }

    /// <summary>Verifies lowering Maximum below the committed value repairs it by clamping and
    /// raises ValueChanged with the exact previous and new committed date-times.</summary>
    [Fact]
    public void Maximum_WhenLoweredBelowCommittedValue_RepairsByClampingAndRaisesValueChanged()
    {
        // Arrange
        using var control = new DateTimeInput { Value = new DateTime(2026, 7, 30, 8, 0, 0) };
        DateTimeInputValueChangedEventArgs? change = null;
        control.ValueChanged += (_, args) => change = args;

        // Act
        control.Maximum = new DateTime(2026, 7, 25, 14, 0, 0);

        // Assert
        control.Value.ShouldBe(new DateTime(2026, 7, 25, 14, 0, 0));
        var raised = change.ShouldNotBeNull();
        raised.PreviousValue.ShouldBe(new DateTime(2026, 7, 30, 8, 0, 0));
        raised.Value.ShouldBe(new DateTime(2026, 7, 25, 14, 0, 0));
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

    /// <summary>Verifies TimeStep rejects a value that is zero, negative, or not a whole minute.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    [InlineData(90)]
    public void TimeStep_WhenValueIsNotAPositiveWholeMinute_Throws(int seconds)
    {
        // Arrange
        using var control = new DateTimeInput();

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.TimeStep = TimeSpan.FromSeconds(seconds));
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
    /// only republishing the private Popup's Closed notification.</summary>
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
    /// fires when it closes, matching ComboBox's and DateInput's drop-down event shape.</summary>
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
    /// property.</summary>
    [Theory]
    [MemberData(nameof(InvalidDropDownHeights))]
    public void Properties_WhenDropDownHeightIsInvalid_ThrowsBeforeMutation(Length value)
    {
        // Arrange
        using var control = new DateTimeInput();

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => control.DropDownHeight = value);
        control.DropDownHeight.ShouldBe(Length.Cells(10));
    }

    /// <summary>Verifies DropDownHeight round-trips a valid value.</summary>
    [Fact]
    public void Properties_WhenDropDownHeightIsSet_RoundTrips()
    {
        // Arrange
        using var control = new DateTimeInput();

        // Act
        control.DropDownHeight = Length.Auto;

        // Assert
        control.DropDownHeight.ShouldBe(Length.Auto);
    }

    /// <summary>Provides unsupported or empty responsive caps.</summary>
    public static TheoryData<Length> InvalidDropDownHeights =>
    [
        Length.Cells(0),
        Length.Percent(0),
        Length.Star(1)
    ];

    /// <summary>Verifies PopupChrome applies to the owned Calendar popup without leaking it, and
    /// ResetPopupChrome returns it to the PopupChrome appearance.</summary>
    [Fact]
    public void PopupStyle_WhenSetAndReset_AppliesToOwnedPopupAndReturnsToThemeDefault()
    {
        // Arrange
        using var control = new DateTimeInput();
        var popup = OwnedTree.Find<Popup>(control).ShouldNotBeNull();
        var themeRoleBorder = popup.Border;
        var border = new Border(BorderSide.All, BorderGlyphStyle.Rounded, Color.Rgb(65, 43, 21), Color.Transparent, TerminalAttributes.None);

        // Act
        control.PopupChrome = new PopupChrome { Border = border };

        // Assert
        popup.Border.ShouldBe(border);

        // Act
        control.ResetPopupChrome();

        // Assert
        control.PopupChrome.ShouldBe(default);
        popup.Border.ShouldBe(themeRoleBorder);
    }

    /// <summary>Verifies ResetPopupChrome rejects use after disposal.</summary>
    [Fact]
    public void ResetPopupChrome_WhenDisposed_Throws()
    {
        // Arrange
        var control = new DateTimeInput();
        control.Dispose();

        // Act and assert
        _ = Should.Throw<ObjectDisposedException>(control.ResetPopupChrome);
    }

    /// <summary>Verifies CalendarStyle applies to the owned Calendar without leaking it, and reading
    /// it back reflects the resolved presentation through ActualCalendarStyle.</summary>
    [Fact]
    public void CalendarStyle_WhenSet_AppliesToOwnedCalendar()
    {
        using var control = new DateTimeInput();
        var calendar = OwnedTree.Find<UiCalendar>(control).ShouldNotBeNull();
        var style = CalendarStyle.Default with { SelectedDayColor = Color.Rgb(65, 43, 21) };

        control.CalendarStyle = style;

        calendar.Style.ShouldBe(style);
        control.ActualCalendarStyle.ShouldBe(calendar.ActualStyle);
    }

    /// <summary>Verifies reading Value on a detached, never-mounted control falls back to the
    /// system clock instead of throwing or returning null, proving the lazy default still
    /// resolves without a dispatcher to observe.</summary>
    [Fact]
    public void Value_WhenReadBeforeAttach_FallsBackToSystemClockWithoutThrowing()
    {
        // Arrange
        using var control = new DateTimeInput();

        // Act
        var value = control.Value;

        // Assert
        _ = value.ShouldNotBeNull();
    }

    /// <summary>Verifies a disposed DateTimeInput rejects every Value read even when a failed lazy
    /// seed attempt has already exercised the getter once.</summary>
    [Fact]
    public void Value_WhenDisposedBeforeSeeding_ThrowsOnEveryRead()
    {
        // Arrange
        var control = new DateTimeInput();
        control.Dispose();

        // Act and assert
        _ = Should.Throw<ObjectDisposedException>(() => _ = control.Value);
        _ = Should.Throw<ObjectDisposedException>(() => _ = control.Value);
    }

    /// <summary>Verifies a disabled DateTimeInput ignores a segment-adjustment key instead of
    /// committing a changed Value, proving the detached OnEvent gate honors EffectiveIsEnabled on
    /// its own - independently of the mounted focus and hit-test pipeline the surface evidence
    /// exercises end-to-end.</summary>
    [Fact]
    public void Input_WhenDisabled_IgnoresSegmentAdjustmentKeys()
    {
        // Arrange
        using var control = new DateTimeInput
        {
            Value = new DateTime(2026, 3, 15, 14, 30, 0),
            IsEnabled = false
        };
        var up = Key(Code.Up);

        // Act
        _ = Router.Route(control, Events.Key, up);

        // Assert
        control.EffectiveIsEnabled.ShouldBeFalse();
        control.Value.ShouldBe(new DateTime(2026, 3, 15, 14, 30, 0));
        up.IsHandled.ShouldBeFalse();
    }

    /// <summary>Verifies Minimum validates its argument against Maximum before checking whether
    /// the control is disposed, matching every other Min/Max-bounded control in the library
    /// (TimeInput, DateInput, NumberInput, CurrencyInput, Slider, Calendar): an out-of-range
    /// argument always surfaces as ArgumentException, even on a disposed control, rather than
    /// masking the argument problem behind ObjectDisposedException.</summary>
    [Fact]
    public void Minimum_WhenAboveMaximumOnDisposedControl_ThrowsArgumentException()
    {
        // Arrange
        var control = new DateTimeInput { Maximum = new DateTime(2026, 1, 1) };
        control.Dispose();

        // Act & Assert
        _ = Should.Throw<ArgumentException>(() => control.Minimum = new DateTime(2026, 2, 1));
    }

    /// <summary>Verifies Maximum validates its argument against Minimum before checking whether
    /// the control is disposed, matching every other Min/Max-bounded control in the library.
    /// See <see cref="Minimum_WhenAboveMaximumOnDisposedControl_ThrowsArgumentException"/>.</summary>
    [Fact]
    public void Maximum_WhenBelowMinimumOnDisposedControl_ThrowsArgumentException()
    {
        // Arrange
        var control = new DateTimeInput { Minimum = new DateTime(2026, 2, 1) };
        control.Dispose();

        // Act & Assert
        _ = Should.Throw<ArgumentException>(() => control.Maximum = new DateTime(2026, 1, 1));
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

    /// <summary>Verifies an incidental Control modifier on Enter forwarded to the open popup's
    /// calendar does not commit the active date, and leaves the stroke unhandled.</summary>
    [Fact]
    public void Dispatch_WhenOpenAndEnterHasControlModifier_DoesNotCommitAndLeavesUnhandled()
    {
        using var control = new DateTimeInput
        {
            Value = new DateTime(2026, 7, 19, 14, 30, 0, DateTimeKind.Utc),
            IsOpen = true
        };

        _ = Router.Route(control, Events.Key, Key(Code.Right));
        var enter = Key(Code.Enter, Modifiers.Control);
        _ = Router.Route(control, Events.Key, enter);

        enter.IsHandled.ShouldBeFalse();
        control.Value.ShouldBe(new DateTime(2026, 7, 19, 14, 30, 0, DateTimeKind.Utc));
        control.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies Shift-held Enter (a common terminal chord) forwarded to the open popup's
    /// calendar still commits the active date.</summary>
    [Fact]
    public void Dispatch_WhenOpenAndEnterHasShiftModifier_StillCommits()
    {
        using var control = new DateTimeInput
        {
            Value = new DateTime(2026, 7, 19, 14, 30, 0, DateTimeKind.Utc),
            IsOpen = true
        };

        _ = Router.Route(control, Events.Key, Key(Code.Right));
        var enter = Key(Code.Enter, Modifiers.Shift);
        _ = Router.Route(control, Events.Key, enter);

        enter.IsHandled.ShouldBeTrue();
        _ = control.Value.ShouldNotBeNull();
        control.Value.Value.Date.ShouldBe(new DateTime(2026, 7, 20));
        control.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies the repeat report from the Alt+Down opening gesture is consumed instead
    /// of becoming calendar navigation after the popup opens.</summary>
    [Fact]
    public void Dispatch_WhenAltDownOpeningGestureRepeats_DoesNotMoveTheCalendarSelection()
    {
        using var control = new DateTimeInput
        {
            Value = new DateTime(2026, 7, 19, 14, 30, 0, DateTimeKind.Utc)
        };

        _ = Router.Route(control, Events.Key, Key(Code.Down, Modifiers.Alt));
        _ = Router.Route(control, Events.Key, Key(Code.Down, Modifiers.Alt, KeyAction.Repeat));
        _ = Router.Route(control, Events.Key, Key(Code.Enter));

        _ = control.Value.ShouldNotBeNull();
        control.Value.Value.Date.ShouldBe(new DateTime(2026, 7, 19));
    }

    /// <summary>Verifies a repeat report cannot originate the Alt+Down opening transition without
    /// an initial key down accepted by the control.</summary>
    [Fact]
    public void Dispatch_WhenAltDownOpeningGestureIsRepeatOnly_DoesNotOpen()
    {
        using var control = new DateTimeInput();

        _ = Router.Route(control, Events.Key, Key(Code.Down, Modifiers.Alt, KeyAction.Repeat));

        control.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies Alt+Down rejects every additional command modifier.</summary>
    [Theory]
    [InlineData(Modifiers.Alt | Modifiers.Control)]
    [InlineData(Modifiers.Alt | Modifiers.Shift)]
    [InlineData(Modifiers.Alt | Modifiers.Super)]
    [InlineData(Modifiers.Alt | Modifiers.Meta)]
    [InlineData(Modifiers.Alt | Modifiers.Hyper)]
    public void Dispatch_WhenAltDownHasExtraModifier_LeavesInputUnhandled(Modifiers modifiers)
    {
        using var control = new DateTimeInput();
        var key = Key(Code.Down, modifiers);

        _ = Router.Route(control, Events.Key, key);

        control.IsOpen.ShouldBeFalse();
        key.IsHandled.ShouldBeFalse();
    }

    /// <summary>Verifies command-modified Tab preserves an open calendar for shortcut routing.</summary>
    [Theory]
    [InlineData(Modifiers.Control)]
    [InlineData(Modifiers.Alt)]
    [InlineData(Modifiers.Super)]
    [InlineData(Modifiers.Meta)]
    [InlineData(Modifiers.Hyper)]
    [InlineData(Modifiers.Control | Modifiers.Shift)]
    public void Dispatch_WhenOpenTabHasCommandModifier_PreservesPopup(Modifiers modifiers)
    {
        using var input = new DateTimeInput { IsOpen = true };
        var key = Key(Code.Tab, modifiers);

        _ = Router.Route(input, Events.Key, key);

        input.IsOpen.ShouldBeTrue();
        key.IsHandled.ShouldBeFalse();
    }

    /// <summary>Verifies selecting a date from the popup preserves the value's time-zone
    /// interpretation and complete sub-second precision.</summary>
    [Fact]
    public void Popup_WhenDateIsSelected_PreservesDateTimeKind()
    {
        // Arrange
        using var control = new DateTimeInput
        {
            Value = new DateTime(2026, 7, 19, 14, 30, 0, 789, DateTimeKind.Utc).AddTicks(4321),
            IsOpen = true
        };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Enter));

        // Assert
        _ = control.Value.ShouldNotBeNull();
        control.Value.Value.Date.ShouldBe(new DateTime(2026, 7, 20));
        control.Value.Value.TimeOfDay.ShouldBe(new TimeSpan(0, 14, 30, 0, 789).Add(TimeSpan.FromTicks(4321)));
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

    #region Format

    /// <summary>Verifies the default Format is null, so layout derives from Culture, Use24HourFormat, and ShowSeconds.</summary>
    [Fact]
    public void Format_WhenControlIsConstructed_DefaultsToNull()
    {
        // Arrange
        using var control = new DateTimeInput();

        // Assert
        control.Format.ShouldBeNull();
    }

    /// <summary>Verifies assigning an empty Format throws.</summary>
    [Fact]
    public void Format_WhenAssignedEmpty_Throws()
    {
        // Arrange
        using var control = new DateTimeInput();

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => control.Format = string.Empty);
    }

    /// <summary>Verifies a custom Format overrides the culture- and flag-derived layout.</summary>
    [Fact]
    public void Render_WhenFormatIsCustom_OverridesDerivedLayout()
    {
        // Arrange
        using var control = new DateTimeInput
        {
            Format = "yyyy/MM/dd hh:mm tt",
            Value = new DateTime(2026, 7, 19, 14, 30, 0)
        };
        new LayoutEngine().Layout(control, new Size(30, 3));
        using Frame frame = new(new Size(30, 3));

        // Act
        control.Render(frame.Canvas);

        // Assert — year-first order with a 12-hour clock and PM designator, overriding the
        // default invariant "MM/dd/yyyy HH:mm" layout.
        var row = Row(frame, 1);
        row.ShouldContain("2026/07/19");
        row.ShouldContain("02:30 PM");
    }

    #endregion

    #region Rendering

    /// <summary>Verifies a German culture draws the typed field's date segments in
    /// day-month-year order with a period separator, instead of the fixed month-day-year slash
    /// order. This is the culture-order bug the segmented-field engine extraction fixes:
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
    /// designator text, in addition to the date portion's order.</summary>
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

    #region Affixes

    /// <summary>Verifies desired width grows by exactly one reserved column per set affix, plus
    /// the shared theme gap, over an equivalent affix-less DateTimeInput.</summary>
    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 2)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 4)]
    public void Measure_WhenAffixesAreSet_ReservesCellsPerAffixPlusGap(
        bool hasStart,
        bool hasEnd,
        int expectedExtraWidth)
    {
        // Arrange
        using var control = new DateTimeInput
        {
            Value = new DateTime(2026, 7, 19, 14, 30, 0),
            StartAffix = hasStart ? new Affix("!") : null,
            EndAffix = hasEnd ? new Affix("!") : null
        };
        using var bare = new DateTimeInput { Value = new DateTime(2026, 7, 19, 14, 30, 0) };

        // Act
        new LayoutEngine().Layout(control, new Size(40, 3));
        new LayoutEngine().Layout(bare, new Size(40, 3));

        // Assert
        (control.DesiredSize.Width - bare.DesiredSize.Width).ShouldBe(expectedExtraWidth);
    }

    /// <summary>Verifies null-to-set and set-to-null affix assignment requires Measure.</summary>
    [Fact]
    public void StartAffix_WhenAssignedOrCleared_InvalidatesMeasure()
    {
        // Arrange
        using var control = new DateTimeInput { Value = new DateTime(2026, 7, 19, 14, 30, 0) };
        control.Clear(Invalidation.All);

        // Act
        control.StartAffix = new Affix("!");

        // Assert
        control.Pending.ShouldBe(Invalidation.All);
        control.Clear(Invalidation.All);

        // Act
        control.StartAffix = null;

        // Assert
        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies a same-resolved-width content or color swap invalidates rendering only.</summary>
    [Fact]
    public void StartAffix_WhenContentChangesAtTheSameResolvedWidth_InvalidatesRenderOnly()
    {
        // Arrange
        using var control = new DateTimeInput
        {
            Value = new DateTime(2026, 7, 19, 14, 30, 0),
            StartAffix = new Affix("|")
        };
        control.Clear(Invalidation.All);

        // Act
        control.StartAffix = new Affix("/");

        // Assert
        control.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies a resolved-width change invalidates Measure again, not just Render.</summary>
    [Fact]
    public void EndAffix_WhenResolvedWidthChanges_InvalidatesMeasure()
    {
        // Arrange
        using var control = new DateTimeInput
        {
            Value = new DateTime(2026, 7, 19, 14, 30, 0),
            EndAffix = new Affix("!")
        };
        control.Clear(Invalidation.All);

        // Act - U+4E16 '世' is a wide CJK ideograph (two cells wide), unlike the one-cell '!' above.
        control.EndAffix = new Affix("世");

        // Assert
        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies reassigning the identical affix value is a no-op.</summary>
    [Fact]
    public void StartAffix_WhenReassignedTheSameValue_DoesNotInvalidate()
    {
        // Arrange
        var affix = new Affix("!");
        using var control = new DateTimeInput
        {
            Value = new DateTime(2026, 7, 19, 14, 30, 0),
            StartAffix = affix
        };
        control.Clear(Invalidation.All);

        // Act
        control.StartAffix = affix;

        // Assert
        control.Pending.ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies the drop-down indicator keeps its own column whether or not affixes are
    /// set, proving an affix is reserved strictly inboard of the indicator and never shifts or
    /// overlaps it.</summary>
    [Fact]
    public void Render_WhenAffixesAreSet_NeverMovesTheDropDownIndicator()
    {
        // Arrange
        using var bare = new DateTimeInput { Value = new DateTime(2026, 7, 19, 14, 30, 0) };
        using var affixed = new DateTimeInput
        {
            Value = new DateTime(2026, 7, 19, 14, 30, 0),
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };
        new LayoutEngine().Layout(bare, new Size(40, 3));
        new LayoutEngine().Layout(affixed, new Size(40, 3));
        using Frame bareFrame = new(new Size(40, 3));
        using Frame affixedFrame = new(new Size(40, 3));

        // Act
        bare.Render(bareFrame.Canvas);
        affixed.Render(affixedFrame.Canvas);

        // Assert - the indicator sits at the same offset from the right edge of each control's own
        // (differently sized) bounds, and it never reads as the affix glyph.
        var bareIndicatorX = bare.Bounds.Right - 2;
        var affixedIndicatorX = affixed.Bounds.Right - 2;
        FrameOracle.Get(bareFrame, new Point(bareIndicatorX, 1)).ShouldBe("▼");
        FrameOracle.Get(affixedFrame, new Point(affixedIndicatorX, 1)).ShouldBe("▼");
        FrameOracle.Get(affixedFrame, new Point(affixedIndicatorX, 1)).ShouldNotBe("<");
    }

    /// <summary>Verifies the start affix survives and the end affix drops whole when the field box
    /// has room for only one, matching the documented priority order.</summary>
    [Fact]
    public void Render_WhenFieldBoxHasRoomForOnlyOneAffix_DropsTheEndAffixFirst()
    {
        // Arrange - a field box with exactly one drawable cell before the indicator's own reserved
        // columns: two border cells, one indicator reservation (2), one content cell.
        using var control = new DateTimeInput
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(5),
            Height = Length.Cells(3),
            Value = new DateTime(2026, 7, 19, 14, 30, 0),
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };
        new LayoutEngine().Layout(control, new Size(5, 3));
        using Frame frame = new(new Size(5, 3));

        // Act
        control.Render(frame.Canvas);

        // Assert - the one drawable field cell goes to the start affix.
        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe(">");
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

    private static KeyEventArgs Key(
        Code code,
        Modifiers modifiers = Modifiers.None,
        KeyAction action = KeyAction.Press) => new(new Stroke(
        code,
        default,
        nativeCode: 0,
        modifiers,
        action));

    private static KeyEventArgs CharacterKey(char character) => CharacterKey(character, Modifiers.None);

    private static KeyEventArgs CharacterKey(char character, Modifiers modifiers) => new(new Stroke(
        Code.Character,
        new Rune(character),
        nativeCode: 0,
        modifiers,
        KeyAction.Press));

    #endregion

    #region Typing

    /// <summary>Verifies date-time digit and AM/PM entry accept only text-entry modifier states
    /// and leave command-modified characters unhandled.</summary>
    [Theory]
    [InlineData(Modifiers.None, true)]
    [InlineData(Modifiers.Shift, true)]
    [InlineData(Modifiers.CapsLock | Modifiers.NumLock, true)]
    [InlineData(Modifiers.Control, false)]
    [InlineData(Modifiers.Alt, false)]
    [InlineData(Modifiers.Super, false)]
    [InlineData(Modifiers.Hyper, false)]
    [InlineData(Modifiers.Meta, false)]
    public void Input_WhenCharacterCarriesModifiers_EditsOnlyForTextEntryState(
        Modifiers modifiers,
        bool expectedEdit)
    {
        // Arrange
        using var digitInput = new DateTimeInput
        {
            Value = new DateTime(2026, 3, 15, 10, 30, 0),
            Use24HourFormat = false
        };
        using var designatorInput = new DateTimeInput
        {
            Value = new DateTime(2026, 3, 15, 10, 30, 0),
            Use24HourFormat = false
        };
        var digit = CharacterKey('9', modifiers);
        var designator = CharacterKey('p', modifiers);

        // Act
        _ = Router.Route(digitInput, Events.Key, digit);
        _ = Router.Route(designatorInput, Events.Key, designator);

        // Assert
        digitInput.Value.ShouldBe(expectedEdit
            ? new DateTime(2026, 9, 15, 10, 30, 0)
            : new DateTime(2026, 3, 15, 10, 30, 0));
        designatorInput.Value.ShouldBe(expectedEdit
            ? new DateTime(2026, 3, 15, 22, 30, 0)
            : new DateTime(2026, 3, 15, 10, 30, 0));
        digit.IsHandled.ShouldBe(expectedEdit);
        designator.IsHandled.ShouldBe(expectedEdit);
    }

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

    /// <summary>Verifies Right stops at the last segment instead of wrapping back to Month,
    /// matching the non-wrapping Left/Right convention <see cref="TimeInput"/> and
    /// <see cref="DateInput"/> both use for the identical shared
    /// <c>SegmentFieldBehavior.MoveSegment</c> navigation engine.</summary>
    [Fact]
    public void MoveSegment_WhenRightIsPressedPastLastSegment_StaysOnLastSegmentInsteadOfWrapping()
    {
        // Arrange: default Use24HourFormat/ShowSeconds layout is Month, Day, Year, Hour, Minute.
        using var control = new DateTimeInput { Value = new DateTime(2026, 6, 15, 10, 30, 0) };

        // Act: four Right presses reach Minute (the last segment); a fifth must not move past it.
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Up));

        // Assert: Up still increments Minute, not Month (which wrapping would land on instead).
        control.Value.ShouldBe(new DateTime(2026, 6, 15, 10, 31, 0));
    }

    #endregion

    #region Segment clamp arithmetic

    /// <summary>Verifies typing a non-leap year over a Feb 29 value clamps the day to Feb 28
    /// instead of throwing from an invalid DateTime construction.</summary>
    [Fact]
    public void TypeDigit_WhenYearEntryLandsOnNonLeapYear_ClampsFebTwentyNineToTwentyEight()
    {
        // Arrange
        using var control = new DateTimeInput { Value = new DateTime(2024, 2, 29, 10, 30, 0) };

        // Act: two Right presses reach the Year segment (Month/Day/Year ordering).
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, CharacterKey('2'));
        _ = Router.Route(control, Events.Key, CharacterKey('0'));
        _ = Router.Route(control, Events.Key, CharacterKey('2'));
        _ = Router.Route(control, Events.Key, CharacterKey('6'));

        // Assert
        control.Value.ShouldBe(new DateTime(2026, 2, 28, 10, 30, 0));
    }

    /// <summary>Verifies typing a 30-day month over a day-31 value clamps the day to 30.</summary>
    [Fact]
    public void TypeDigit_WhenMonthEntryLandsOnThirtyDayMonth_ClampsDayThirtyOneToThirty()
    {
        // Arrange
        using var control = new DateTimeInput { Value = new DateTime(2026, 1, 31, 10, 30, 0) };

        // Act: Month is the first segment, already active.
        _ = Router.Route(control, Events.Key, CharacterKey('0'));
        _ = Router.Route(control, Events.Key, CharacterKey('4'));

        // Assert
        control.Value.ShouldBe(new DateTime(2026, 4, 30, 10, 30, 0));
    }

    /// <summary>Verifies the same leap-day clamp exercised via the Up increment key path, a
    /// structurally separate code path from digit entry.</summary>
    [Fact]
    public void Increment_WhenYearIncrementLandsOnNonLeapYear_ClampsFebTwentyNineToTwentyEight()
    {
        // Arrange
        using var control = new DateTimeInput { Value = new DateTime(2024, 2, 29, 10, 30, 0) };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Up));
        _ = Router.Route(control, Events.Key, Key(Code.Up));

        // Assert
        control.Value.ShouldBe(new DateTime(2026, 2, 28, 10, 30, 0));
    }

    /// <summary>Verifies incrementing the year above DateTime's maximum supported year is
    /// silently ignored, leaving the value unchanged.</summary>
    [Fact]
    public void Increment_WhenYearIncrementExceedsMaximumSupportedYear_LeavesValueUnchanged()
    {
        // Arrange
        using var control = new DateTimeInput { Value = DateTime.MaxValue };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Up));

        // Assert
        control.Value.ShouldBe(DateTime.MaxValue);
    }

    /// <summary>Verifies decrementing the year below DateTime's minimum supported year is
    /// silently ignored, leaving the value unchanged.</summary>
    [Fact]
    public void Increment_WhenYearDecrementGoesBelowMinimumSupportedYear_LeavesValueUnchanged()
    {
        // Arrange
        using var control = new DateTimeInput { Value = DateTime.MinValue };

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Down));

        // Assert
        control.Value.ShouldBe(DateTime.MinValue);
    }

    #endregion

    #region Sub-second precision

    /// <summary>Verifies typing a digit on the Month segment preserves the pre-existing
    /// sub-second (millisecond-resolution) remainder instead of silently truncating it, since
    /// <c>ReplaceMonth</c> only ever reconstructs the value from whole hour/minute/second
    /// components.</summary>
    [Fact]
    public void TypeDigit_WhenEditingMonthSegment_PreservesSubSecondPrecision()
    {
        // Arrange
        using var control = new DateTimeInput { Value = new DateTime(2026, 1, 15, 10, 15, 30).AddMilliseconds(450) };

        // Act: Month is the first segment, already active.
        _ = Router.Route(control, Events.Key, CharacterKey('0'));
        _ = Router.Route(control, Events.Key, CharacterKey('4'));

        // Assert
        var value = control.Value.ShouldNotBeNull();
        value.Month.ShouldBe(4);
        value.Millisecond.ShouldBe(450);
    }

    /// <summary>Verifies typing a Month digit that forces <c>ReplaceMonth</c>'s own day-clamp (the
    /// destination month has fewer days than the source day) still preserves the pre-existing
    /// sub-second remainder even though the bound intentionally overrides the day component - the
    /// clamp and the tick-preservation fix must compose rather than the clamp path bypassing
    /// <c>WithSubSecondTicksOf</c>.</summary>
    [Fact]
    public void TypeDigit_WhenMonthChangeClampsDayToShorterMonth_PreservesSubSecondPrecision()
    {
        // Arrange
        using var control = new DateTimeInput { Value = new DateTime(2026, 1, 31, 10, 15, 30).AddMilliseconds(450) };

        // Act: Month is the first segment, already active. February 2026 (non-leap) has 28 days.
        _ = Router.Route(control, Events.Key, CharacterKey('0'));
        _ = Router.Route(control, Events.Key, CharacterKey('2'));

        // Assert
        var value = control.Value.ShouldNotBeNull();
        value.Month.ShouldBe(2);
        value.Day.ShouldBe(28);
        value.Millisecond.ShouldBe(450);
    }

    /// <summary>Verifies typing a digit on the Day segment preserves the pre-existing sub-second
    /// remainder.</summary>
    [Fact]
    public void TypeDigit_WhenEditingDaySegment_PreservesSubSecondPrecision()
    {
        // Arrange
        using var control = new DateTimeInput { Value = new DateTime(2026, 1, 15, 10, 15, 30).AddMilliseconds(450) };
        _ = Router.Route(control, Events.Key, Key(Code.Right));

        // Act
        _ = Router.Route(control, Events.Key, CharacterKey('0'));
        _ = Router.Route(control, Events.Key, CharacterKey('9'));

        // Assert
        var value = control.Value.ShouldNotBeNull();
        value.Day.ShouldBe(9);
        value.Millisecond.ShouldBe(450);
    }

    /// <summary>Verifies typing a digit on the Year segment preserves the pre-existing sub-second
    /// remainder.</summary>
    [Fact]
    public void TypeDigit_WhenEditingYearSegment_PreservesSubSecondPrecision()
    {
        // Arrange
        using var control = new DateTimeInput { Value = new DateTime(2020, 1, 15, 10, 15, 30).AddMilliseconds(450) };
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Right));

        // Act
        _ = Router.Route(control, Events.Key, CharacterKey('2'));
        _ = Router.Route(control, Events.Key, CharacterKey('0'));
        _ = Router.Route(control, Events.Key, CharacterKey('2'));
        _ = Router.Route(control, Events.Key, CharacterKey('6'));

        // Assert
        var value = control.Value.ShouldNotBeNull();
        value.Year.ShouldBe(2026);
        value.Millisecond.ShouldBe(450);
    }

    /// <summary>Verifies typing a digit on the Hour segment preserves the pre-existing sub-second
    /// remainder.</summary>
    [Fact]
    public void TypeDigit_WhenEditingHourSegment_PreservesSubSecondPrecision()
    {
        // Arrange
        using var control = new DateTimeInput { Value = new DateTime(2026, 1, 15, 10, 15, 30).AddMilliseconds(450) };
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Right));

        // Act
        _ = Router.Route(control, Events.Key, CharacterKey('9'));

        // Assert
        var value = control.Value.ShouldNotBeNull();
        value.Hour.ShouldBe(9);
        value.Millisecond.ShouldBe(450);
    }

    /// <summary>Verifies typing a digit on the Minute segment preserves the pre-existing
    /// sub-second remainder.</summary>
    [Fact]
    public void TypeDigit_WhenEditingMinuteSegment_PreservesSubSecondPrecision()
    {
        // Arrange
        using var control = new DateTimeInput { Value = new DateTime(2026, 1, 15, 10, 15, 30).AddMilliseconds(450) };
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Right));

        // Act
        _ = Router.Route(control, Events.Key, CharacterKey('5'));

        // Assert
        var value = control.Value.ShouldNotBeNull();
        value.Minute.ShouldBe(5);
        value.Millisecond.ShouldBe(450);
    }

    /// <summary>Verifies typing a digit on the Second segment preserves the pre-existing
    /// sub-second remainder, proving even the segment that shares the same integer-second
    /// granularity still keeps the finer tick-resolution precision underneath it.</summary>
    [Fact]
    public void TypeDigit_WhenEditingSecondSegment_PreservesSubSecondPrecision()
    {
        // Arrange
        using var control = new DateTimeInput
        {
            Value = new DateTime(2026, 1, 15, 10, 15, 30).AddMilliseconds(450),
            ShowSeconds = true
        };
        for (var index = 0; index < 5; index++)
        {
            _ = Router.Route(control, Events.Key, Key(Code.Right));
        }

        // Act
        _ = Router.Route(control, Events.Key, CharacterKey('5'));

        // Assert
        var value = control.Value.ShouldNotBeNull();
        value.Second.ShouldBe(5);
        value.Millisecond.ShouldBe(450);
    }

    /// <summary>Verifies clearing the Hour segment via Backspace preserves the sub-second
    /// remainder of the still-unedited minute/second portion.</summary>
    [Fact]
    public void ClearSegment_WhenBackspaceOnHourSegment_PreservesSubSecondPrecision()
    {
        // Arrange
        using var control = new DateTimeInput { Value = new DateTime(2026, 1, 15, 10, 15, 30).AddMilliseconds(450) };
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Right));

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Backspace));

        // Assert
        var value = control.Value.ShouldNotBeNull();
        value.Hour.ShouldBe(0);
        value.Minute.ShouldBe(15);
        value.Second.ShouldBe(30);
        value.Millisecond.ShouldBe(450);
    }

    /// <summary>Verifies stepping the Year segment with Up (<c>SafeAddYears</c>, which rebuilds the
    /// value through <c>ReplaceYear</c>'s 7-argument DateTime constructor) preserves the
    /// pre-existing sub-second remainder instead of silently truncating it.</summary>
    [Fact]
    public void Increment_WhenYearIsSteppedViaUp_PreservesSubSecondPrecision()
    {
        // Arrange
        using var control = new DateTimeInput { Value = new DateTime(2026, 1, 15, 10, 15, 30).AddMilliseconds(450) };
        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Right));

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.Up));

        // Assert
        var value = control.Value.ShouldNotBeNull();
        value.Year.ShouldBe(2027);
        value.Millisecond.ShouldBe(450);
    }

    #endregion

    /// <summary>Verifies DateTimeInput's digit and increment segment-editing paths already seed a
    /// value after Value is cleared to null (e.g. via Delete), instead of refusing to act -
    /// confirming existing correct behavior, unlike DateInput's equivalent paths before they were
    /// fixed to do the same.</summary>
    [Fact]
    public void SegmentEdit_WhenValueIsNullAfterClear_SeedsForDigitAndIncrement()
    {
        // Arrange
        using var control = new DateTimeInput { Value = new DateTime(2026, 1, 15, 10, 15, 30) };

        // Act and assert - a digit typed right after a clear lands on a seeded value instead of
        // being silently dropped.
        control.Value = null;
        _ = Router.Route(control, Events.Key, CharacterKey('5'));
        _ = control.Value.ShouldNotBeNull();

        // Act and assert - Up after a clear seeds a value instead of refusing to act.
        control.Value = null;
        _ = Router.Route(control, Events.Key, Key(Code.Up));
        _ = control.Value.ShouldNotBeNull();
    }
}
