// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;


/// <summary>Proves the detached public DateInput contract, clamping, culture, and rendering.</summary>
public sealed class DateInputTests
{
    /// <summary>Verifies reentrant value publication suppresses the superseded typed event.</summary>
    [Fact]
    public void Value_WhenPropertyObserverCommitsNewerValue_SuppressesStaleTypedEvent()
    {
        var initial = new DateOnly(2026, 8, 1);
        var first = new DateOnly(2026, 8, 10);
        var second = new DateOnly(2026, 8, 20);
        using var input = new DateInput { Value = initial };
        var observations = new List<(DateOnly? EventValue, DateOnly? LiveValue)>();
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(DateInput.Value) && input.Value == first)
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
        using var input = new DateInput { Value = null };
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(DateInput.AllowNull) && !input.AllowNull)
            {
                input.AllowNull = true;
            }
        };

        input.AllowNull = false;

        input.AllowNull.ShouldBeTrue();
        input.Value.ShouldBeNull();
    }

    /// <summary>Verifies both date endpoints repair the value and retained calendar after a throwing observer.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Bounds_WhenPropertyObserverThrows_StillRepairDependentState(bool minimum)
    {
        // Arrange
        var value = minimum ? new DateOnly(2026, 7, 10) : new DateOnly(2026, 7, 20);
        var bound = minimum ? new DateOnly(2026, 7, 12) : new DateOnly(2026, 7, 18);
        using var control = new DateInput { Value = value };
        var propertyName = minimum ? nameof(DateInput.Minimum) : nameof(DateInput.Maximum);
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
        control.OwnedCalendar.MinimumDate.ShouldBe(control.Minimum);
        control.OwnedCalendar.MaximumDate.ShouldBe(control.Maximum);
    }

    /// <summary>Verifies a newer bound committed from PropertyChanged owns both the public input
    /// and its retained Calendar validation boundary.</summary>
    [Fact]
    public void Minimum_WhenPropertyObserverCommitsNewerBound_SynchronizesCalendarToNewerValue()
    {
        var outer = new DateOnly(2020, 1, 1);
        var nested = new DateOnly(2010, 1, 1);
        using var input = new DateInput();
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(DateInput.Minimum) && input.Minimum == outer)
            {
                input.Minimum = nested;
            }
        };

        input.Minimum = outer;

        input.Minimum.ShouldBe(nested);
        OwnedTree.Find<UiCalendar>(input).ShouldNotBeNull().MinimumDate.ShouldBe(nested);
    }

    #region Properties

    /// <summary>Verifies a null value is accepted when AllowNull is enabled.</summary>
    [Fact]
    public void Properties_WhenValueIsNull_AllowsNullWhenEnabled()
    {
        // Arrange
        using var control = new DateInput { AllowNull = true };

        // Act
        control.Value = null;

        // Assert
        control.Value.ShouldBeNull();
    }

    /// <summary>Verifies setting AllowNull to false repairs a null value to the current date.</summary>
    [Fact]
    public void Properties_WhenAllowNullIsFalse_RejectsNull()
    {
        // Arrange
        using var control = new DateInput();
        control.Value = null;
        control.Value.ShouldBeNull();

        // Act
        control.AllowNull = false;

        // Assert
        _ = control.Value.ShouldNotBeNull();
    }

    /// <summary>Verifies assigning null on a never-yet-read control with AllowNull already disabled
    /// still resolves to a real seeded date instead of latching null: the setter's early return
    /// must not mark the control seeded without ever assigning a real value, or a later read
    /// observes null despite AllowNull being false.</summary>
    [Fact]
    public void Value_WhenNullIsAssignedBeforeFirstReadAndAllowNullIsAlreadyFalse_ResolvesToSeededDate()
    {
        // Arrange
        using var control = new DateInput { AllowNull = false };

        // Act
        control.Value = null;

        // Assert
        _ = control.Value.ShouldNotBeNull();
    }

    /// <summary>Verifies assigning null preserves the committed date when null values are disabled.</summary>
    [Fact]
    public void Value_WhenNullIsAssignedAndAllowNullIsFalse_PreservesValue()
    {
        // Arrange
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 8, 3),
            AllowNull = false
        };

        // Act
        control.Value = null;

        // Assert
        control.Value.ShouldBe(new DateOnly(2026, 8, 3));
    }

    /// <summary>Verifies the value is clamped to the minimum when set below the allowed range.</summary>
    [Fact]
    public void Properties_WhenMinMaxAreSet_ClampsValue()
    {
        // Arrange
        using var control = new DateInput
        {
            Minimum = new DateOnly(2026, 7, 15),
            Maximum = new DateOnly(2026, 7, 25)
        };

        // Act
        control.Value = new DateOnly(2026, 7, 10);

        // Assert
        control.Value.ShouldBe(new DateOnly(2026, 7, 15));
    }

    /// <summary>Verifies Minimum and Maximum default to DateOnly.MinValue and DateOnly.MaxValue.</summary>
    [Fact]
    public void Properties_WhenConstructed_MinimumAndMaximumDefaultToFullRange()
    {
        // Arrange
        using var control = new DateInput();

        // Assert
        control.Minimum.ShouldBe(DateOnly.MinValue);
        control.Maximum.ShouldBe(DateOnly.MaxValue);
    }

    /// <summary>Verifies Minimum rejects a value that exceeds Maximum.</summary>
    [Fact]
    public void Minimum_WhenExceedsMaximum_ThrowsBeforeMutation()
    {
        // Arrange
        using var control = new DateInput { Maximum = new DateOnly(2026, 7, 25) };

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => control.Minimum = new DateOnly(2026, 8, 1));
        control.Minimum.ShouldBe(DateOnly.MinValue);
    }

    /// <summary>Verifies Maximum rejects a value below Minimum.</summary>
    [Fact]
    public void Maximum_WhenBelowMinimum_ThrowsBeforeMutation()
    {
        // Arrange
        using var control = new DateInput { Minimum = new DateOnly(2026, 7, 15) };

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => control.Maximum = new DateOnly(2026, 7, 1));
        control.Maximum.ShouldBe(DateOnly.MaxValue);
    }

    /// <summary>Verifies raising Minimum above the committed value repairs it by clamping and
    /// raises ValueChanged with the exact previous and new committed dates.</summary>
    [Fact]
    public void Minimum_WhenRaisedAboveCommittedValue_RepairsByClampingAndRaisesValueChanged()
    {
        // Arrange
        using var control = new DateInput { Value = new DateOnly(2026, 7, 10) };
        DateInputValueChangedEventArgs? change = null;
        control.ValueChanged += (_, args) => change = args;

        // Act
        control.Minimum = new DateOnly(2026, 7, 15);

        // Assert
        control.Value.ShouldBe(new DateOnly(2026, 7, 15));
        var raised = change.ShouldNotBeNull();
        raised.PreviousValue.ShouldBe(new DateOnly(2026, 7, 10));
        raised.Value.ShouldBe(new DateOnly(2026, 7, 15));
    }

    /// <summary>Verifies lowering Maximum below the committed value repairs it by clamping and
    /// raises ValueChanged with the exact previous and new committed dates.</summary>
    [Fact]
    public void Maximum_WhenLoweredBelowCommittedValue_RepairsByClampingAndRaisesValueChanged()
    {
        // Arrange
        using var control = new DateInput { Value = new DateOnly(2026, 7, 30) };
        DateInputValueChangedEventArgs? change = null;
        control.ValueChanged += (_, args) => change = args;

        // Act
        control.Maximum = new DateOnly(2026, 7, 25);

        // Assert
        control.Value.ShouldBe(new DateOnly(2026, 7, 25));
        var raised = change.ShouldNotBeNull();
        raised.PreviousValue.ShouldBe(new DateOnly(2026, 7, 30));
        raised.Value.ShouldBe(new DateOnly(2026, 7, 25));
    }

    /// <summary>Verifies DropDownHeight defaults to a positive value, round-trips, and rejects a
    /// non-positive assignment.</summary>
    [Fact]
    public void DropDownHeight_WhenConstructed_DefaultsToPositiveValue()
    {
        // Arrange
        using var control = new DateInput();

        // Assert
        control.DropDownHeight.ShouldBe(Length.Cells(10));
    }

    /// <summary>Verifies DropDownHeight round-trips a valid value.</summary>
    [Fact]
    public void DropDownHeight_WhenSet_RoundTrips()
    {
        // Arrange
        using var control = new DateInput();

        // Act
        control.DropDownHeight = Length.Percent(50);

        // Assert
        control.DropDownHeight.ShouldBe(Length.Percent(50));
    }

    /// <summary>Verifies DropDownHeight rejects a non-positive value.</summary>
    [Theory]
    [MemberData(nameof(InvalidDropDownHeights))]
    public void DropDownHeight_WhenInvalid_ThrowsBeforeMutation(Length value)
    {
        // Arrange
        using var control = new DateInput();

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => control.DropDownHeight = value);
        control.DropDownHeight.ShouldBe(Length.Cells(10));
    }

    /// <summary>Provides unsupported or empty responsive caps.</summary>
    public static TheoryData<Length> InvalidDropDownHeights =>
    [
        Length.Cells(0),
        Length.Percent(0),
        Length.Star(1)
    ];

    /// <summary>Verifies a single-character Format outside DateOnly's own specifier set is rejected
    /// by the setter instead of arming a FormatException that would later escape the layout
    /// pass.</summary>
    [Theory]
    [InlineData('t')]
    [InlineData('T')]
    [InlineData('f')]
    [InlineData('F')]
    [InlineData('g')]
    [InlineData('G')]
    [InlineData('U')]
    [InlineData('u')]
    public void Format_WhenSingleSpecifierIsNotSupportedByDateOnly_ThrowsArgumentException(char specifier)
    {
        // Arrange
        using var control = new DateInput();

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => control.Format = specifier.ToString());
        control.Format.ShouldBe("d");
    }

    /// <summary>Verifies a composite Format containing a time specifier is rejected, even though it
    /// is longer than one character.</summary>
    [Theory]
    [InlineData("yyyy-MM-dd HH:mm")]
    [InlineData("hh:mm tt")]
    [InlineData("HH:mm")]
    public void Format_WhenCompositePatternContainsTimeSpecifier_ThrowsArgumentException(string format)
    {
        // Arrange
        using var control = new DateInput();

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => control.Format = format);
        control.Format.ShouldBe("d");
    }

    /// <summary>Verifies patterns DateOnly can actually render are accepted and lay out and render
    /// without throwing.</summary>
    [Theory]
    [InlineData("(dd/MM/yyyy)")]
    [InlineData("dd MMM yyyy")]
    [InlineData("d")]
    [InlineData("D")]
    [InlineData("M")]
    [InlineData("Y")]
    public void Format_WhenPatternIsRenderableByDateOnly_LaysOutAndRenders(string format)
    {
        // Arrange
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 7, 19),
            Culture = CultureInfo.InvariantCulture,
            Format = format
        };
        new LayoutEngine().Layout(control, new Size(30, 3));
        using Frame frame = new(new Size(30, 3));

        // Act and assert
        Should.NotThrow(() => control.Render(frame.Canvas));
    }

    /// <summary>Verifies a culture whose active calendar is not Gregorian is rejected before the
    /// field and its owned Calendar can render or edit the same value under different calendars.</summary>
    [Theory]
    [InlineData("ar-SA")]
    [InlineData("en-SA")]
    public void Culture_WhenCalendarIsNotGregorian_ThrowsAndPreservesCulture(string cultureName)
    {
        // Arrange
        using var control = new DateInput { Value = new DateOnly(2026, 7, 19) };
        var culture = new CultureInfo(cultureName);
        var previous = control.Culture;

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => control.Culture = culture);
        control.Culture.ShouldBeSameAs(previous);
        control.OwnedCalendar.Culture.ShouldBeSameAs(previous);
    }

    /// <summary>Verifies setting an invalid Format while Value is still null does not arm a later
    /// crash: the setter rejects it immediately regardless of the current Value, so there is
    /// nothing left to detonate when Value or Culture changes afterward.</summary>
    [Fact]
    public void Format_WhenSetWhileValueIsNullThenValueIsAssigned_NeverThrowsLater()
    {
        // Arrange
        using var control = new DateInput { AllowNull = true, Value = null };

        // Act and assert: the invalid format is rejected here, not deferred.
        _ = Should.Throw<ArgumentException>(() => control.Format = "HH:mm");

        // A subsequent Value assignment and measure-invalidating change both stay safe.
        _ = Should.NotThrow(() => control.Value = new DateOnly(2026, 7, 19));
        Should.NotThrow(() => new LayoutEngine().Layout(control, new Size(20, 3)));
        _ = Should.NotThrow(() => control.Culture = new CultureInfo("de-DE"));
    }

    /// <summary>Verifies date letters inside a quoted literal do not become editable segments.</summary>
    [Fact]
    public void Input_WhenCustomFormatStartsWithQuotedDateLetters_UpAdjustsFirstVisibleSegment()
    {
        // Arrange
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 7, 19),
            Culture = CultureInfo.InvariantCulture,
            Format = "'date:' MM/dd/yyyy"
        };

        // Act
        _ = Router.Route(control, Events.Key, KeyEvent(Code.Up));

        // Assert
        control.Value.ShouldBe(new DateOnly(2026, 8, 19));
    }

    /// <summary>Verifies segment keys cannot mutate a value when the format exposes no segments.</summary>
    [Fact]
    public void Input_WhenFormatContainsOnlyLiteral_UpDoesNotEditHiddenDate()
    {
        // Arrange
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 7, 19),
            Culture = CultureInfo.InvariantCulture,
            Format = "'choose date'"
        };
        var key = KeyEvent(Code.Up);

        // Act
        _ = Router.Route(control, Events.Key, key);

        // Assert
        control.Value.ShouldBe(new DateOnly(2026, 7, 19));
        key.IsHandled.ShouldBeFalse();
    }

    /// <summary>Verifies changing culture produces different rendered output.</summary>
    [Fact]
    public void Properties_WhenCultureChanges_InvalidatesRender()
    {
        // Arrange
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 7, 19),
            Culture = CultureInfo.InvariantCulture
        };
        new LayoutEngine().Layout(control, new Size(20, 3));
        using Frame before = new(new Size(20, 3));
        control.Render(before.Canvas);
        var rowBefore = Row(before, 1);

        // Act
        control.Culture = new CultureInfo("de-DE");
        new LayoutEngine().Layout(control, new Size(20, 3));
        using Frame after = new(new Size(20, 3));
        control.Render(after.Canvas);
        var rowAfter = Row(after, 1);

        // Assert
        rowBefore.ShouldNotBe(rowAfter);
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
            var control = new DateInput();
            root.Children.Add(control);
            new LayoutEngine().Layout(root, new Size(20, 10));
            root.Attach(dispatcher);
            var notifications = new List<bool>();
            control.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(DateInput.IsOpen))
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
    /// fires when it closes, matching ComboBox's drop-down event shape.</summary>
    [Fact]
    public async Task DropDownOpened_WhenDropDownOpens_FiresEventAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var control = new DateInput();
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

    /// <summary>Verifies an incidental Control modifier on Enter forwarded to the open popup's
    /// calendar does not commit the active date, and leaves the stroke unhandled.</summary>
    [Fact]
    public void Dispatch_WhenOpenAndEnterHasControlModifier_DoesNotCommitAndLeavesUnhandled()
    {
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture,
            IsOpen = true
        };

        _ = Router.Route(control, Events.Key, KeyEvent(Code.Right));
        var enter = KeyEvent(Code.Enter, Modifiers.Control);
        _ = Router.Route(control, Events.Key, enter);

        enter.IsHandled.ShouldBeFalse();
        control.Value.ShouldBe(new DateOnly(2026, 3, 15));
        control.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies Shift-held Enter (a common terminal chord) forwarded to the open popup's
    /// calendar still commits the active date.</summary>
    [Fact]
    public void Dispatch_WhenOpenAndEnterHasShiftModifier_StillCommits()
    {
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture,
            IsOpen = true
        };

        _ = Router.Route(control, Events.Key, KeyEvent(Code.Right));
        var enter = KeyEvent(Code.Enter, Modifiers.Shift);
        _ = Router.Route(control, Events.Key, enter);

        enter.IsHandled.ShouldBeTrue();
        control.Value.ShouldBe(new DateOnly(2026, 3, 16));
        control.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies the repeat report from the Alt+Down opening gesture is consumed instead
    /// of being reinterpreted as calendar navigation after the popup opens.</summary>
    [Fact]
    public void Dispatch_WhenAltDownOpeningGestureRepeats_DoesNotMoveTheCalendarSelection()
    {
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture
        };

        _ = Router.Route(control, Events.Key, KeyEvent(Code.Down, Modifiers.Alt));
        _ = Router.Route(control, Events.Key, KeyEvent(Code.Down, Modifiers.Alt, KeyAction.Repeat));
        _ = Router.Route(control, Events.Key, KeyEvent(Code.Enter));

        control.Value.ShouldBe(new DateOnly(2026, 3, 15));
    }

    /// <summary>Verifies repeat reports cannot originate the Alt+Down or F4 opening transition
    /// without an initial key down accepted by the control.</summary>
    [Theory]
    [InlineData(Code.Down, Modifiers.Alt)]
    [InlineData(Code.F4, Modifiers.None)]
    public void Dispatch_WhenOpeningGestureIsRepeatOnly_DoesNotOpen(Code code, Modifiers modifiers)
    {
        using var control = new DateInput();

        _ = Router.Route(control, Events.Key, KeyEvent(code, modifiers, KeyAction.Repeat));

        control.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies dropdown chords reject every additional command modifier.</summary>
    [Theory]
    [InlineData(Code.Down, Modifiers.Alt | Modifiers.Control)]
    [InlineData(Code.Down, Modifiers.Alt | Modifiers.Shift)]
    [InlineData(Code.Down, Modifiers.Alt | Modifiers.Super)]
    [InlineData(Code.Down, Modifiers.Alt | Modifiers.Meta)]
    [InlineData(Code.Down, Modifiers.Alt | Modifiers.Hyper)]
    [InlineData(Code.F4, Modifiers.Alt)]
    [InlineData(Code.F4, Modifiers.Control)]
    [InlineData(Code.F4, Modifiers.Shift)]
    [InlineData(Code.F4, Modifiers.Super)]
    [InlineData(Code.F4, Modifiers.Meta)]
    [InlineData(Code.F4, Modifiers.Hyper)]
    public void Dispatch_WhenOpeningGestureHasExtraModifier_LeavesInputUnhandled(Code code, Modifiers modifiers)
    {
        using var control = new DateInput();
        var key = KeyEvent(code, modifiers);

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
        using var input = new DateInput { IsOpen = true };
        var key = KeyEvent(Code.Tab, modifiers);

        _ = Router.Route(input, Events.Key, key);

        input.IsOpen.ShouldBeTrue();
        key.IsHandled.ShouldBeFalse();
    }

    /// <summary>Verifies PopupChrome applies to the owned Calendar popup without leaking it, and
    /// ResetPopupChrome returns it to the PopupChrome appearance.</summary>
    [Fact]
    public void PopupStyle_WhenSetAndReset_AppliesToOwnedPopupAndReturnsToThemeDefault()
    {
        // Arrange
        using var control = new DateInput();
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
        var control = new DateInput();
        control.Dispose();

        // Act and assert
        _ = Should.Throw<ObjectDisposedException>(control.ResetPopupChrome);
    }

    /// <summary>Verifies CalendarStyle applies to the owned Calendar without leaking it, and reading
    /// it back reflects the resolved presentation through ActualCalendarStyle.</summary>
    [Fact]
    public void CalendarStyle_WhenSet_AppliesToOwnedCalendar()
    {
        using var control = new DateInput();
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
        using var control = new DateInput();

        // Act
        var value = control.Value;

        // Assert
        _ = value.ShouldNotBeNull();
    }

    /// <summary>Verifies a disposed DateInput rejects every Value read even when a failed lazy seed
    /// attempt has already exercised the getter once.</summary>
    [Fact]
    public void Value_WhenDisposedBeforeSeeding_ThrowsOnEveryRead()
    {
        // Arrange
        var control = new DateInput();
        control.Dispose();

        // Act and assert
        _ = Should.Throw<ObjectDisposedException>(() => _ = control.Value);
        _ = Should.Throw<ObjectDisposedException>(() => _ = control.Value);
    }

    /// <summary>Verifies a disabled DateInput ignores a segment-adjustment key instead of
    /// committing a changed Value, proving the detached OnEvent gate honors EffectiveIsEnabled on
    /// its own - independently of the mounted focus and hit-test pipeline the surface evidence
    /// exercises end-to-end.</summary>
    [Fact]
    public void Input_WhenDisabled_IgnoresSegmentAdjustmentKeys()
    {
        // Arrange
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture,
            IsEnabled = false
        };
        var up = KeyEvent(Code.Up);

        // Act
        _ = Router.Route(control, Events.Key, up);

        // Assert
        control.EffectiveIsEnabled.ShouldBeFalse();
        control.Value.ShouldBe(new DateOnly(2026, 3, 15));
        up.IsHandled.ShouldBeFalse();
    }

    #endregion

    #region Commit

    /// <summary>Verifies the ValueChanged event fires with correct previous and current values.</summary>
    [Fact]
    public void Commit_WhenValueChanges_RaisesValueChanged()
    {
        // Arrange
        using var control = new DateInput { Value = new DateOnly(2026, 7, 10) };
        DateInputValueChangedEventArgs? observed = null;
        control.ValueChanged += (_, eventArgs) => observed = eventArgs;

        // Act
        control.Value = new DateOnly(2026, 7, 19);

        // Assert
        _ = observed.ShouldNotBeNull();
        observed.PreviousValue.ShouldBe(new DateOnly(2026, 7, 10));
        observed.Value.ShouldBe(new DateOnly(2026, 7, 19));
    }

    #endregion

    #region Typing

    /// <summary>Verifies segmented date entry accepts only text-entry modifier states and leaves
    /// command-modified digits unhandled without changing the active segment.</summary>
    [Theory]
    [InlineData(Modifiers.None, true)]
    [InlineData(Modifiers.Shift, true)]
    [InlineData(Modifiers.CapsLock | Modifiers.NumLock, true)]
    [InlineData(Modifiers.Control, false)]
    [InlineData(Modifiers.Alt, false)]
    [InlineData(Modifiers.Super, false)]
    [InlineData(Modifiers.Hyper, false)]
    [InlineData(Modifiers.Meta, false)]
    public void Input_WhenDigitCarriesModifiers_EditsOnlyForTextEntryState(
        Modifiers modifiers,
        bool expectedEdit)
    {
        // Arrange
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture
        };
        var key = CharacterKey('9', modifiers);

        // Act
        _ = Router.Route(control, Events.Key, key);

        // Assert
        control.Value.ShouldBe(expectedEdit ? new DateOnly(2026, 9, 15) : new DateOnly(2026, 3, 15));
        key.IsHandled.ShouldBe(expectedEdit);
    }

    /// <summary>Verifies typing four digits on the Year segment produces a year above 99
    /// instead of committing after two digits and misapplying the rest to Month.</summary>
    [Fact]
    public void TypeDigit_WhenFourDigitsTypedOnYearSegment_ProducesFullYear()
    {
        // Arrange
        using var control = new DateInput
        {
            Value = new DateOnly(2020, 1, 1),
            Culture = CultureInfo.InvariantCulture
        };

        // Act: focus starts on Month (segment 0); InvariantCulture's short date pattern
        // orders segments Month/Day/Year, so two Right presses reach Year.
        PressKey(control, Code.Right);
        PressKey(control, Code.Right);
        TypeCharacter(control, '2');
        TypeCharacter(control, '0');
        TypeCharacter(control, '2');
        TypeCharacter(control, '6');

        // Assert
        control.Value.ShouldNotBeNull().Year.ShouldBe(2026);
    }

    /// <summary>Verifies moving to another segment starts a new digit entry sequence.</summary>
    [Fact]
    public void TypeDigit_WhenSegmentNavigationOccurs_DoesNotCarryPreviousDigit()
    {
        // Arrange
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 8, 15),
            Culture = CultureInfo.InvariantCulture
        };

        // Act
        TypeCharacter(control, '1');
        PressKey(control, Code.Right);
        TypeCharacter(control, '2');

        // Assert
        control.Value.ShouldBe(new DateOnly(2026, 1, 2));
    }

    /// <summary>Verifies moving to an edge segment starts a new digit entry sequence.</summary>
    [Fact]
    public void TypeDigit_WhenEdgeNavigationOccurs_DoesNotCarryPreviousDigit()
    {
        // Arrange
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 8, 15),
            Culture = CultureInfo.InvariantCulture
        };

        // Act
        TypeCharacter(control, '1');
        PressKey(control, Code.End);
        TypeCharacter(control, '2');

        // Assert
        control.Value.ShouldBe(new DateOnly(2, 1, 15));
    }

    /// <summary>Verifies adjusting a segment starts a new digit entry sequence.</summary>
    [Fact]
    public void TypeDigit_WhenSegmentIsAdjusted_DoesNotCarryPreviousDigit()
    {
        // Arrange
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 8, 15),
            Culture = CultureInfo.InvariantCulture
        };

        // Act
        TypeCharacter(control, '1');
        PressKey(control, Code.Up);
        TypeCharacter(control, '2');

        // Assert
        control.Value.ShouldBe(new DateOnly(2026, 2, 15));
    }

    private static void TypeCharacter(DateInput control, char digit) =>
        Router.Route(
            control,
            Events.Key,
            CharacterKey(digit, Modifiers.None));

    private static KeyEventArgs CharacterKey(char character, Modifiers modifiers) => new(new Stroke(
        Code.Character,
        new Rune(character),
        nativeCode: 0,
        modifiers,
        KeyAction.Press));

    private static void PressKey(DateInput control, Code code) =>
        Router.Route(
            control,
            Events.Key,
            new KeyEventArgs(new Stroke(
                code,
                character: null,
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press)));

    /// <summary>Verifies pointer hit-testing measures each rendered segment under the tree's
    /// ambient ambiguous-width policy instead of always Narrow, so a click on a rendered column
    /// resolves to the segment actually occupying it.</summary>
    [Fact]
    public void SegmentAtColumn_WhenAmbiguousWidthIsWideAndFormatHasAmbiguousSeparators_ResolvesRenderedColumnToCorrectSegment()
    {
        // Arrange — "dd·MM·yyyy" places an ambiguous-width "·" separator (1 cell under Narrow, 2
        // under Wide) between every field. Under Wide, "15·03·2026" renders Day at content
        // columns 0-1, the separator at 2-3, and Month at 4-5 - column 5 is the last rendered
        // column of Month. A hit-test that still measures separators under Narrow undercounts by
        // one cell per separator crossed, so this same column would resolve past Month to Year.
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 3, 15),
            Culture = CultureInfo.InvariantCulture,
            Format = "dd·MM·yyyy"
        };
        control.SetCellPolicy(new UnicodePolicy(Ambiguous.Wide));
        new LayoutEngine().Layout(control, new Size(20, 3));
        var content = control.ContentBounds;
        var eventArgs = new PointerEventArgs(new Pointer(
            new Point(content.X + 5, content.Y),
            pixels: null,
            Buttons.Primary,
            PointerAction.Press,
            wheelX: 0,
            wheelY: 0,
            Modifiers.None,
            isMotion: false,
            isCellPositionInferred: false));

        // Act
        _ = Router.Route(control, Events.Pointer, eventArgs);
        PressKey(control, Code.Up);

        // Assert — the Month field incremented, proving the click activated Month, not Year.
        control.Value.ShouldBe(new DateOnly(2026, 4, 15));
    }

    /// <summary>Verifies a focus callback may dispose the input without the pointer path handling
    /// the obsolete segment action afterward.</summary>
    [Fact]
    public async Task Dispatch_WhenGotFocusDisposesDateInput_StopsPointerContinuationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var control = new DateInput
            {
                Value = new DateOnly(2026, 3, 15),
                Bounds = new Rect(0, 0, 12, 1)
            };
            control.Attach(dispatcher);
            using FocusManager focus = new(control);
            using PointerManager pointer = new(control);
            control.GotFocus += (_, _) => control.Dispose();
            var press = new Pointer(
                new Point(1, 0),
                pixels: null,
                Buttons.Primary,
                PointerAction.Press,
                wheelX: 0,
                wheelY: 0,
                Modifiers.None,
                isMotion: false,
                isCellPositionInferred: false);

            _ = pointer.Dispatch(press);

            control.IsDisposed.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    #endregion

    #region Segment clamp arithmetic

    /// <summary>Verifies typing a non-leap year over a Feb 29 value clamps the day to Feb 28
    /// instead of throwing from an invalid DateOnly construction.</summary>
    [Fact]
    public void TypeDigit_WhenYearEntryLandsOnNonLeapYear_ClampsFebTwentyNineToTwentyEight()
    {
        // Arrange
        using var control = new DateInput
        {
            Value = new DateOnly(2024, 2, 29),
            Culture = CultureInfo.InvariantCulture
        };

        // Act: two Right presses reach the Year segment (Month/Day/Year ordering).
        PressKey(control, Code.Right);
        PressKey(control, Code.Right);
        TypeCharacter(control, '2');
        TypeCharacter(control, '0');
        TypeCharacter(control, '2');
        TypeCharacter(control, '6');

        // Assert
        control.Value.ShouldBe(new DateOnly(2026, 2, 28));
    }

    /// <summary>Verifies typing a 30-day month over a day-31 value clamps the day to 30.</summary>
    [Fact]
    public void TypeDigit_WhenMonthEntryLandsOnThirtyDayMonth_ClampsDayThirtyOneToThirty()
    {
        // Arrange
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 1, 31),
            Culture = CultureInfo.InvariantCulture
        };

        // Act: Month is the first segment, already active.
        TypeCharacter(control, '0');
        TypeCharacter(control, '4');

        // Assert
        control.Value.ShouldBe(new DateOnly(2026, 4, 30));
    }

    /// <summary>Verifies the same leap-day clamp exercised via the Up increment key path, a
    /// structurally separate code path from digit entry.</summary>
    [Fact]
    public void Increment_WhenYearIncrementLandsOnNonLeapYear_ClampsFebTwentyNineToTwentyEight()
    {
        // Arrange
        using var control = new DateInput
        {
            Value = new DateOnly(2024, 2, 29),
            Culture = CultureInfo.InvariantCulture
        };

        // Act
        PressKey(control, Code.Right);
        PressKey(control, Code.Right);
        PressKey(control, Code.Up);
        PressKey(control, Code.Up);

        // Assert
        control.Value.ShouldBe(new DateOnly(2026, 2, 28));
    }

    /// <summary>Verifies incrementing the year above DateOnly's maximum supported year is
    /// silently ignored - not clamped into range - leaving the value unchanged.</summary>
    [Fact]
    public void Increment_WhenYearIncrementExceedsMaximumSupportedYear_LeavesValueUnchanged()
    {
        // Arrange
        using var control = new DateInput
        {
            Value = DateOnly.MaxValue,
            Culture = CultureInfo.InvariantCulture
        };

        // Act
        PressKey(control, Code.Right);
        PressKey(control, Code.Right);
        PressKey(control, Code.Up);

        // Assert
        control.Value.ShouldBe(DateOnly.MaxValue);
    }

    /// <summary>Verifies decrementing the year below DateOnly's minimum supported year is
    /// silently ignored - not clamped into range - leaving the value unchanged.</summary>
    [Fact]
    public void Increment_WhenYearDecrementGoesBelowMinimumSupportedYear_LeavesValueUnchanged()
    {
        // Arrange
        using var control = new DateInput
        {
            Value = DateOnly.MinValue,
            Culture = CultureInfo.InvariantCulture
        };

        // Act
        PressKey(control, Code.Right);
        PressKey(control, Code.Right);
        PressKey(control, Code.Down);

        // Assert
        control.Value.ShouldBe(DateOnly.MinValue);
    }

    #endregion

    #region Rendering

    /// <summary>Verifies a custom Format starting with a literal character (not a bare
    /// standard specifier) renders instead of crashing GetAllDateTimePatterns.</summary>
    [Fact]
    public void Render_WhenFormatStartsWithLiteralCharacter_DoesNotThrow()
    {
        // Arrange
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 7, 19),
            Culture = CultureInfo.InvariantCulture,
            Format = "(dd/MM/yyyy)"
        };
        new LayoutEngine().Layout(control, new Size(24, 3));
        using Frame frame = new(new Size(24, 3));

        // Act and assert
        Should.NotThrow(() => control.Render(frame.Canvas));
        Row(frame, 1).ShouldContain("19");
        Row(frame, 1).ShouldContain("07");
        Row(frame, 1).ShouldContain("2026");
    }

    /// <summary>Verifies a set date is rendered as formatted text inside the border.</summary>
    [Fact]
    public void Render_WhenValueIsSet_DrawsFormattedDate()
    {
        // Arrange
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 7, 19),
            Culture = CultureInfo.InvariantCulture
        };
        new LayoutEngine().Layout(control, new Size(20, 3));
        using Frame frame = new(new Size(20, 3));

        // Act
        control.Render(frame.Canvas);

        // Assert
        Row(frame, 1).ShouldContain("07");
        Row(frame, 1).ShouldContain("19");
        Row(frame, 1).ShouldContain("2026");
    }

    /// <summary>Verifies a null value renders the placeholder dashes inside the border.</summary>
    [Fact]
    public void Render_WhenValueIsNull_DrawsPlaceholder()
    {
        // Arrange
        using var control = new DateInput
        {
            AllowNull = true,
            Culture = CultureInfo.InvariantCulture
        };
        control.Value = null;
        new LayoutEngine().Layout(control, new Size(20, 3));
        using Frame frame = new(new Size(20, 3));

        // Act
        control.Render(frame.Canvas);

        // Assert
        Row(frame, 1).ShouldContain("--");
    }

    /// <summary>Verifies a null placeholder follows the configured date format.</summary>
    [Fact]
    public void Render_WhenValueIsNullAndFormatIsCustom_DrawsPlaceholderInConfiguredFormat()
    {
        // Arrange
        using var control = new DateInput
        {
            AllowNull = true,
            Culture = CultureInfo.InvariantCulture,
            Format = "yyyy.MM.dd",
            Value = null
        };
        new LayoutEngine().Layout(control, new Size(20, 3));
        using Frame frame = new(new Size(20, 3));

        // Act
        control.Render(frame.Canvas);

        // Assert
        Row(frame, 1).ShouldContain("----.--.--");
    }

    /// <summary>Verifies quoted date letters remain literal text in a null placeholder.</summary>
    [Fact]
    public void Render_WhenNullFormatContainsQuotedDateLetters_PreservesLiteralText()
    {
        // Arrange
        using var control = new DateInput
        {
            AllowNull = true,
            Culture = CultureInfo.InvariantCulture,
            Format = "'date:' MM/dd/yyyy",
            Value = null
        };
        new LayoutEngine().Layout(control, new Size(30, 3));
        using Frame frame = new(new Size(30, 3));

        // Act
        control.Render(frame.Canvas);

        // Assert
        Row(frame, 1).ShouldContain("date: --/--/----");
    }

    /// <summary>Verifies a leading literal is not mistaken for the focused month segment.</summary>
    [Fact]
    public void Render_WhenFocusedFormatStartsWithQuotedLiteral_HighlightsFirstEditableSegment()
    {
        // Arrange
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 7, 19),
            Culture = CultureInfo.InvariantCulture,
            Format = "'date:' MM/dd/yyyy"
        };
        control.SetFocused(true);
        new LayoutEngine().Layout(control, new Size(30, 3));
        using Frame frame = new(new Size(30, 3));

        // Act
        control.Render(frame.Canvas);

        // Assert
        (frame.GetCell(new Point(1, 1)).Style.Attributes & TerminalAttributes.Reverse)
            .ShouldBe(TerminalAttributes.None);
        (frame.GetCell(new Point(7, 1)).Style.Attributes & TerminalAttributes.Reverse)
            .ShouldBe(TerminalAttributes.Reverse);
    }

    /// <summary>Verifies literal private-use characters cannot collide with internal span markers.</summary>
    [Fact]
    public void Render_WhenQuotedLiteralContainsMarkerCharacters_PreservesLiteralText()
    {
        // Arrange
        const char literalStart = '\uE000';
        const char literalEnd = '\uE001';
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 7, 19),
            Culture = CultureInfo.InvariantCulture,
            Format = $"'x{literalStart}y{literalEnd}' MM/dd/yyyy"
        };
        new LayoutEngine().Layout(control, new Size(30, 3));
        using Frame frame = new(new Size(30, 3));

        // Act
        control.Render(frame.Canvas);

        // Assert
        Row(frame, 1).ShouldContain($"x{literalStart}y{literalEnd} 07/19/2026");
    }

    #endregion

    #region Affixes

    /// <summary>Verifies desired width grows by exactly one reserved column per set affix, plus
    /// the shared theme gap, over an equivalent affix-less DateInput.</summary>
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
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 7, 19),
            Culture = CultureInfo.InvariantCulture,
            StartAffix = hasStart ? new Affix("!") : null,
            EndAffix = hasEnd ? new Affix("!") : null
        };
        using var bare = new DateInput { Value = new DateOnly(2026, 7, 19), Culture = CultureInfo.InvariantCulture };

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
        using var control = new DateInput { Value = new DateOnly(2026, 7, 19), Culture = CultureInfo.InvariantCulture };
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
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 7, 19),
            Culture = CultureInfo.InvariantCulture,
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
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 7, 19),
            Culture = CultureInfo.InvariantCulture,
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
        using var control = new DateInput
        {
            Value = new DateOnly(2026, 7, 19),
            Culture = CultureInfo.InvariantCulture,
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
        using var bare = new DateInput
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Value = new DateOnly(2026, 7, 19),
            Culture = CultureInfo.InvariantCulture,
            Format = "MM/dd/yyyy"
        };
        using var affixed = new DateInput
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Value = new DateOnly(2026, 7, 19),
            Culture = CultureInfo.InvariantCulture,
            Format = "MM/dd/yyyy",
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
        // (differently sized) bounds, and the affix glyphs never appear on the indicator's column.
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
        using var control = new DateInput
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(5),
            Height = Length.Cells(3),
            Value = new DateOnly(2026, 7, 19),
            Culture = CultureInfo.InvariantCulture,
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

    private static KeyEventArgs KeyEvent(
        Code code,
        Modifiers modifiers = Modifiers.None,
        KeyAction action = KeyAction.Press) => new(new Stroke(
        code,
        default,
        nativeCode: 0,
        modifiers,
        action));

    #endregion
}
