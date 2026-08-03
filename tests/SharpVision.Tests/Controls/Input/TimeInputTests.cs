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
            MinimumTime = new TimeOnly(10, 0),
            MaximumTime = new TimeOnly(14, 0)
        };

        // Act
        control.Value = new TimeOnly(8, 0);

        // Assert
        control.Value.ShouldBe(new TimeOnly(10, 0));
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
            MinimumTime = new TimeOnly(0, 0),
            MaximumTime = new TimeOnly(23, 59)
        };
        _ = Router.Route(control, Events.Key, Key(Code.Right));

        // Act
        _ = Router.Route(control, Events.Key, Key(code));

        // Assert
        control.Value.ShouldBe(bound);
    }

    /// <summary>Verifies unhandled keys still reach the inherited Control event surface.</summary>
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

    #endregion
}
