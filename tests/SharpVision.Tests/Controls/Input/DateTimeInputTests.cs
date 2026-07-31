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

    #endregion

    #region Rendering

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
