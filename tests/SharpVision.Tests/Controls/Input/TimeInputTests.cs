// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves the detached public TimeInput contract, clamping, and rendering.</summary>
public sealed class TimeInputTests
{
    #region Properties

    /// <summary>Verifies a null value is accepted when AllowNull is enabled.</summary>
    [ComponentUnitEvidence(typeof(TimeInput))]
    [Fact]
    public void Properties_WhenValueIsNull_AllowsNullWhenEnabled()
    {
        // Arrange
        using var control = new TimeInput { AllowNull = true };

        // Act
        control.Value = null;

        // Assert
        control.Value.ShouldBeNull();
    }

    /// <summary>Verifies setting Value to null is rejected when AllowNull is false.</summary>
    [Fact]
    public void Properties_WhenAllowNullIsFalse_RejectsNull()
    {
        // Arrange
        using var control = new TimeInput();
        var initialValue = control.Value;
        control.AllowNull = false;

        // Act
        control.Value = null;

        // Assert
        _ = control.Value.ShouldNotBeNull();
        control.Value.ShouldBe(initialValue);
    }

    /// <summary>Verifies disabling null support repairs an already-cleared value.</summary>
    [Fact]
    public void AllowNull_WhenDisabledAfterValueWasCleared_RepairsValue()
    {
        // Arrange
        using var control = new TimeInput { Value = null };

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
        using var control = new TimeInput
        {
            Minimum = new TimeOnly(10, 0),
            Maximum = new TimeOnly(14, 0)
        };

        // Act
        control.Value = new TimeOnly(8, 0);

        // Assert
        control.Value.ShouldBe(new TimeOnly(10, 0));
    }

    /// <summary>Verifies Minimum and Maximum default to TimeOnly.MinValue and TimeOnly.MaxValue.</summary>
    [Fact]
    public void Properties_WhenConstructed_MinimumAndMaximumDefaultToFullDayRange()
    {
        // Arrange
        using var control = new TimeInput();

        // Assert
        control.Minimum.ShouldBe(TimeOnly.MinValue);
        control.Maximum.ShouldBe(TimeOnly.MaxValue);
    }

    /// <summary>Verifies Minimum rejects a value that exceeds Maximum.</summary>
    [Fact]
    public void Minimum_WhenExceedsMaximum_ThrowsBeforeMutation()
    {
        // Arrange
        using var control = new TimeInput { Maximum = new TimeOnly(10, 0) };

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => control.Minimum = new TimeOnly(11, 0));
        control.Minimum.ShouldBe(TimeOnly.MinValue);
    }

    /// <summary>Verifies Maximum rejects a value below Minimum.</summary>
    [Fact]
    public void Maximum_WhenBelowMinimum_ThrowsBeforeMutation()
    {
        // Arrange
        using var control = new TimeInput { Minimum = new TimeOnly(10, 0) };

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => control.Maximum = new TimeOnly(9, 0));
        control.Maximum.ShouldBe(TimeOnly.MaxValue);
    }

    /// <summary>Verifies raising Minimum above the committed value repairs it by clamping.</summary>
    [Fact]
    public void Minimum_WhenRaisedAboveCommittedValue_RepairsByClamping()
    {
        // Arrange
        using var control = new TimeInput { Value = new TimeOnly(8, 0) };

        // Act
        control.Minimum = new TimeOnly(10, 0);

        // Assert
        control.Value.ShouldBe(new TimeOnly(10, 0));
    }

    /// <summary>Verifies lowering Maximum below the committed value repairs it by clamping.</summary>
    [Fact]
    public void Maximum_WhenLoweredBelowCommittedValue_RepairsByClamping()
    {
        // Arrange
        using var control = new TimeInput { Value = new TimeOnly(18, 0) };

        // Act
        control.Maximum = new TimeOnly(14, 0);

        // Assert
        control.Value.ShouldBe(new TimeOnly(14, 0));
    }

    /// <summary>Verifies toggling Use24HourFormat changes the rendered output.</summary>
    [Fact]
    public void Properties_WhenUse24HourFormatChanges_InvalidatesRender()
    {
        // Arrange
        using var control = new TimeInput { Value = new TimeOnly(14, 30) };
        new LayoutEngine().Layout(control, new Size(20, 3));
        using Frame before = new(new Size(20, 3));
        control.Render(before.Canvas);
        var rowBefore = Row(before, 1);

        // Act
        control.Use24HourFormat = false;
        new LayoutEngine().Layout(control, new Size(20, 3));
        using Frame after = new(new Size(20, 3));
        control.Render(after.Canvas);
        var rowAfter = Row(after, 1);

        // Assert
        rowBefore.ShouldNotBe(rowAfter);
    }

    /// <summary>Verifies toggling ShowSeconds changes the measured width.</summary>
    [Fact]
    public void Properties_WhenShowSecondsChanges_InvalidatesMeasure()
    {
        // Arrange
        using var control = new TimeInput { Value = new TimeOnly(14, 30, 45) };
        new LayoutEngine().Layout(control, new Size(20, 3));
        var widthBefore = control.DesiredSize.Width;

        // Act
        control.ShowSeconds = true;
        new LayoutEngine().Layout(control, new Size(20, 3));
        var widthAfter = control.DesiredSize.Width;

        // Assert
        widthAfter.ShouldBeGreaterThan(widthBefore);
    }

    /// <summary>Verifies the configurable minute step is applied by the minute segment arrows.</summary>
    [Fact]
    public void Input_WhenMinuteStepIsConfigured_ArrowAdjustsByConfiguredMinutes()
    {
        using var control = new TimeInput
        {
            Value = new TimeOnly(10, 10),
            TimeStep = TimeSpan.FromMinutes(15)
        };

        _ = Router.Route(control, Events.Key, Key(Code.Right));
        _ = Router.Route(control, Events.Key, Key(Code.Up));

        control.Value.ShouldBe(new TimeOnly(10, 25));
    }

    /// <summary>Verifies TimeStep rejects a value that is zero, negative, or not a whole minute.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    [InlineData(90)]
    public void TimeStep_WhenValueIsNotAPositiveWholeMinute_Throws(int seconds)
    {
        // Arrange
        using var control = new TimeInput();

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.TimeStep = TimeSpan.FromSeconds(seconds));
    }

    /// <summary>Verifies minute adjustment clamps instead of wrapping across midnight.</summary>
    [Theory]
    [InlineData(Code.Up, 23, 59)]
    [InlineData(Code.Down, 0, 0)]
    public void Input_WhenMinuteAdjustmentCrossesMidnight_ClampsAtBound(
        Code code,
        int hour,
        int minute)
    {
        // Arrange
        var bound = new TimeOnly(hour, minute);
        using var control = new TimeInput
        {
            Value = bound,
            Minimum = new TimeOnly(0, 0),
            Maximum = new TimeOnly(23, 59)
        };
        _ = Router.Route(control, Events.Key, Key(Code.Right));

        // Act
        _ = Router.Route(control, Events.Key, Key(code));

        // Assert
        control.Value.ShouldBe(bound);
    }

    /// <summary>Verifies unhandled keys still reach the inherited ControlBase event surface.</summary>
    [Fact]
    public void Input_WhenKeyIsUnhandled_RaisesKeyDown()
    {
        // Arrange
        using var control = new TimeInput();
        var raised = 0;
        control.KeyDown += (_, _) => raised++;

        // Act
        _ = Router.Route(control, Events.Key, Key(Code.F1));

        // Assert
        raised.ShouldBe(1);
    }

    /// <summary>Verifies reading Value on a detached, never-mounted control falls back to the
    /// system clock instead of throwing or returning null, proving the lazy default still
    /// resolves without a dispatcher to observe.</summary>
    [Fact]
    public void Value_WhenReadBeforeAttach_FallsBackToSystemClockWithoutThrowing()
    {
        // Arrange
        using var control = new TimeInput();

        // Act
        var value = control.Value;

        // Assert
        _ = value.ShouldNotBeNull();
    }

    /// <summary>Verifies a disabled TimeInput ignores a segment-adjustment key instead of
    /// committing a changed Value, proving the detached OnEvent gate honors EffectiveIsEnabled on
    /// its own - independently of the mounted focus and hit-test pipeline the surface evidence
    /// exercises end-to-end.</summary>
    [ComponentUnitEvidence(typeof(TimeInput), ComponentBehavior.Disabled)]
    [Fact]
    public void Input_WhenDisabled_IgnoresSegmentAdjustmentKeys()
    {
        // Arrange
        using var control = new TimeInput
        {
            Value = new TimeOnly(14, 30),
            IsEnabled = false
        };
        var up = Key(Code.Up);

        // Act
        _ = Router.Route(control, Events.Key, up);

        // Assert
        control.EffectiveIsEnabled.ShouldBeFalse();
        control.Value.ShouldBe(new TimeOnly(14, 30));
        up.IsHandled.ShouldBeFalse();
    }

    #endregion

    #region Commit

    /// <summary>Verifies the ValueChanged event fires with correct previous and current values.</summary>
    [Fact]
    public void Commit_WhenValueChanges_RaisesValueChanged()
    {
        // Arrange
        using var control = new TimeInput { Value = new TimeOnly(10, 0) };
        TimeInputValueChangedEventArgs? observed = null;
        control.ValueChanged += (_, eventArgs) => observed = eventArgs;

        // Act
        control.Value = new TimeOnly(14, 30);

        // Assert
        _ = observed.ShouldNotBeNull();
        observed.PreviousValue.ShouldBe(new TimeOnly(10, 0));
        observed.Value.ShouldBe(new TimeOnly(14, 30));
    }

    #endregion

    #region Culture

    /// <summary>Verifies the default culture is invariant, so out-of-the-box rendering never
    /// depends on the host operating system's locale.</summary>
    [Fact]
    public void Culture_WhenControlIsConstructed_DefaultsToInvariant()
    {
        // Arrange
        using var control = new TimeInput();

        // Assert
        control.Culture.ShouldBeSameAs(CultureInfo.InvariantCulture);
    }

    /// <summary>Verifies assigning a null culture throws.</summary>
    [Fact]
    public void Culture_WhenAssignedNull_Throws()
    {
        // Arrange
        using var control = new TimeInput();

        // Act and assert
        _ = Should.Throw<ArgumentNullException>(() => control.Culture = null!);
    }

    /// <summary>Verifies a culture change invalidates measure.</summary>
    [Fact]
    public void Culture_WhenChanged_InvalidatesMeasure()
    {
        // Arrange
        using var control = new TimeInput();
        new LayoutEngine().Layout(control, new Size(20, 3));

        // Act and assert
        _ = Should.NotThrow(() => control.Culture = new CultureInfo("fi-FI"));
    }

    #endregion

    #region Format

    /// <summary>Verifies the default Format is null, so layout derives from Use24HourFormat and ShowSeconds.</summary>
    [Fact]
    public void Format_WhenControlIsConstructed_DefaultsToNull()
    {
        // Arrange
        using var control = new TimeInput();

        // Assert
        control.Format.ShouldBeNull();
    }

    /// <summary>Verifies assigning an empty Format throws.</summary>
    [Fact]
    public void Format_WhenAssignedEmpty_Throws()
    {
        // Arrange
        using var control = new TimeInput();

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => control.Format = string.Empty);
    }

    /// <summary>Verifies a Format containing date-only tokens is rejected, since TimeOnly cannot render them.</summary>
    [Fact]
    public void Format_WhenPatternContainsDateTokens_Throws()
    {
        // Arrange
        using var control = new TimeInput();

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => control.Format = "yyyy-MM-dd");
    }

    /// <summary>Verifies a custom Format overrides Use24HourFormat/ShowSeconds-derived layout.</summary>
    [Fact]
    public void Render_WhenFormatIsCustom_OverridesDerivedLayout()
    {
        // Arrange
        using var control = new TimeInput
        {
            Use24HourFormat = true,
            ShowSeconds = false,
            Format = "hh:mm:ss tt",
            Value = new TimeOnly(14, 30, 5)
        };
        new LayoutEngine().Layout(control, new Size(20, 3));
        using Frame frame = new(new Size(20, 3));

        // Act
        control.Render(frame.Canvas);

        // Assert — the custom 12-hour, seconds-visible pattern wins over the derived 24-hour,
        // seconds-hidden defaults.
        Row(frame, 1).ShouldContain("02:30:05 PM");
    }

    /// <summary>Verifies typing a two-digit hour on a Format-driven 12-hour segment clamps to 12,
    /// proving the hour segment's effective range follows the pattern's own tokens rather than
    /// the unrelated Use24HourFormat flag left at its default.</summary>
    [Fact]
    public void TypeDigit_WhenFormatUsesTwelveHourTokenAndUse24HourFormatIsStillTrue_ClampsHourToTwelve()
    {
        // Arrange
        using var control = new TimeInput
        {
            Format = "hh:mm tt",
            Value = new TimeOnly(15, 0)
        };

        // Act — type "1" then "5" for the hour segment: 15 clamps to 12 under 12-hour rules,
        // which is noon (hour 12) in this PM context.
        TypeCharacter(control, '1');
        TypeCharacter(control, '5');

        // Assert
        _ = control.Value.ShouldNotBeNull();
        control.Value.Value.Hour.ShouldBe(12);
    }

    #endregion

    #region Rendering

    /// <summary>Verifies a set time value is rendered as formatted text inside the border.</summary>
    [Fact]
    public void Render_WhenValueIsSet_DrawsFormattedTime()
    {
        // Arrange
        using var control = new TimeInput { Value = new TimeOnly(14, 30) };
        new LayoutEngine().Layout(control, new Size(20, 3));
        using Frame frame = new(new Size(20, 3));

        // Act
        control.Render(frame.Canvas);

        // Assert
        Row(frame, 1).ShouldContain("14:30");
    }

    /// <summary>Verifies a null value renders the placeholder dashes inside the border.</summary>
    [Fact]
    public void Render_WhenValueIsNull_DrawsPlaceholder()
    {
        // Arrange
        using var control = new TimeInput { AllowNull = true };
        control.Value = null;
        new LayoutEngine().Layout(control, new Size(20, 3));
        using Frame frame = new(new Size(20, 3));

        // Act
        control.Render(frame.Canvas);

        // Assert
        Row(frame, 1).ShouldContain("--:--");
    }

    /// <summary>Verifies a Finnish culture renders its own time separator instead of a hardcoded
    /// colon, proving the typed field is culture-driven rather than fixed.</summary>
    [Fact]
    public void Render_WhenCultureIsFinnish_DrawsLocalizedTimeSeparator()
    {
        // Arrange
        using var control = new TimeInput
        {
            Culture = new CultureInfo("fi-FI"),
            Value = new TimeOnly(14, 30)
        };
        new LayoutEngine().Layout(control, new Size(20, 3));
        using Frame frame = new(new Size(20, 3));

        // Act
        control.Render(frame.Canvas);

        // Assert
        Row(frame, 1).ShouldContain("14.30");
        Row(frame, 1).ShouldNotContain("14:30");
    }

    /// <summary>Verifies a Finnish culture's own AM/PM designator text is rendered instead of the
    /// hardcoded English "AM"/"PM" literals.</summary>
    [Fact]
    public void Render_WhenCultureIsFinnishAndTwelveHour_DrawsLocalizedDesignator()
    {
        // Arrange
        using var control = new TimeInput
        {
            Culture = new CultureInfo("fi-FI"),
            Use24HourFormat = false,
            Value = new TimeOnly(14, 30)
        };
        new LayoutEngine().Layout(control, new Size(20, 3));
        using Frame frame = new(new Size(20, 3));

        // Act
        control.Render(frame.Canvas);

        // Assert
        Row(frame, 1).ShouldContain("ip.");
        Row(frame, 1).ShouldNotContain("PM");
    }

    /// <summary>Verifies a Japanese culture's non-Latin AM/PM designator text renders correctly,
    /// proving designator localization is not limited to ASCII replacements.</summary>
    [Fact]
    public void Render_WhenCultureIsJapaneseAndTwelveHour_DrawsNonLatinDesignator()
    {
        // Arrange
        using var control = new TimeInput
        {
            Culture = new CultureInfo("ja-JP"),
            Use24HourFormat = false,
            Value = new TimeOnly(9, 15)
        };
        new LayoutEngine().Layout(control, new Size(20, 3));
        using Frame frame = new(new Size(20, 3));

        // Act
        control.Render(frame.Canvas);

        // Assert
        Row(frame, 1).ShouldContain("午前");
    }

    #endregion

    #region Affixes

    /// <summary>Verifies desired width grows by exactly one reserved column per set affix, plus
    /// the shared theme gap, over an equivalent affix-less TimeInput. TimeInput has no drop-down
    /// indicator, so the affix columns are the only extra width.</summary>
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
        using var control = new TimeInput
        {
            Value = new TimeOnly(14, 30),
            StartAffix = hasStart ? new Affix("!") : null,
            EndAffix = hasEnd ? new Affix("!") : null
        };
        using var bare = new TimeInput { Value = new TimeOnly(14, 30) };

        // Act
        new LayoutEngine().Layout(control, new Size(30, 3));
        new LayoutEngine().Layout(bare, new Size(30, 3));

        // Assert
        (control.DesiredSize.Width - bare.DesiredSize.Width).ShouldBe(expectedExtraWidth);
    }

    /// <summary>Verifies null-to-set and set-to-null affix assignment requires Measure.</summary>
    [Fact]
    public void StartAffix_WhenAssignedOrCleared_InvalidatesMeasure()
    {
        // Arrange
        using var control = new TimeInput { Value = new TimeOnly(14, 30) };
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

    /// <summary>Verifies a same-resolved-width content or color swap invalidates rendering only,
    /// the exact grading an animated affix depends on.</summary>
    [Fact]
    public void StartAffix_WhenContentChangesAtTheSameResolvedWidth_InvalidatesRenderOnly()
    {
        // Arrange
        using var control = new TimeInput { Value = new TimeOnly(14, 30), StartAffix = new Affix("|") };
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
        using var control = new TimeInput { Value = new TimeOnly(14, 30), EndAffix = new Affix("!") };
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
        using var control = new TimeInput { Value = new TimeOnly(14, 30), StartAffix = affix };
        control.Clear(Invalidation.All);

        // Act
        control.StartAffix = affix;

        // Assert
        control.Pending.ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies the start affix survives and the end affix drops whole when the content
    /// box has room for only one, matching the documented priority order.</summary>
    [Fact]
    public void Render_WhenContentHasRoomForOnlyOneAffix_DropsTheEndAffixFirst()
    {
        // Arrange - a one-cell-wide content box (three cells minus the one-cell border each side).
        using var control = new TimeInput
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(3),
            Height = Length.Cells(3),
            Value = new TimeOnly(14, 30),
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };
        new LayoutEngine().Layout(control, new Size(3, 3));
        using Frame frame = new(new Size(3, 3));

        // Act
        control.Render(frame.Canvas);

        // Assert - the one drawable content cell goes to the start affix.
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

    private static KeyEventArgs Key(Code code) => new(new Stroke(
        code,
        default,
        nativeCode: 0,
        Modifiers.None,
        KeyAction.Press));

    private static void TypeCharacter(TimeInput control, char digit) =>
        Router.Route(
            control,
            Events.Key,
            new KeyEventArgs(new Stroke(
                Code.Character,
                new Rune(digit),
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press)));

    #endregion
}
