// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves the detached public DateInput contract, clamping, culture, and rendering.</summary>
public sealed class DateInputTests
{
    #region Properties

    /// <summary>Verifies a null value is accepted when AllowNull is enabled.</summary>
    [ComponentUnitEvidence(typeof(DateInput))]
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

    /// <summary>Verifies the value is clamped to the minimum when set below the allowed range.</summary>
    [Fact]
    public void Properties_WhenMinMaxAreSet_ClampsValue()
    {
        // Arrange
        using var control = new DateInput
        {
            MinimumDate = new DateOnly(2026, 7, 15),
            MaximumDate = new DateOnly(2026, 7, 25)
        };

        // Act
        control.Value = new DateOnly(2026, 7, 10);

        // Assert
        control.Value.ShouldBe(new DateOnly(2026, 7, 15));
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
        new Engine().Layout(control, new Size(20, 3));
        using Frame before = new(new Size(20, 3));
        control.Render(before.Canvas);
        var rowBefore = Row(before, 1);

        // Act
        control.Culture = new CultureInfo("de-DE");
        new Engine().Layout(control, new Size(20, 3));
        using Frame after = new(new Size(20, 3));
        control.Render(after.Canvas);
        var rowAfter = Row(after, 1);

        // Assert
        rowBefore.ShouldNotBe(rowAfter);
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

    private static void TypeCharacter(DateInput control, char digit) =>
        Router.Route(
            control,
            Events.Key,
            new KeyEventArgs(new Stroke(
                Code.Character,
                new Rune(digit),
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press)));

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
        new Engine().Layout(control, new Size(24, 3));
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
        new Engine().Layout(control, new Size(20, 3));
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
        new Engine().Layout(control, new Size(20, 3));
        using Frame frame = new(new Size(20, 3));

        // Act
        control.Render(frame.Canvas);

        // Assert
        Row(frame, 1).ShouldContain("--");
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

    #endregion
}
