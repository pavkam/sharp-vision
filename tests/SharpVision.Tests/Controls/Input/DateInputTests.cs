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
            MinimumDate = new DateOnly(2026, 7, 15),
            MaximumDate = new DateOnly(2026, 7, 25)
        };

        // Act
        control.Value = new DateOnly(2026, 7, 10);

        // Assert
        control.Value.ShouldBe(new DateOnly(2026, 7, 15));
    }

    /// <summary>Verifies a single-character Format outside DateOnly's own specifier set is rejected
    /// by the setter instead of arming a FormatException that would later escape the layout pass
    /// (see #182).</summary>
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
    /// is longer than one character (see #182).</summary>
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
    /// without throwing (see #182).</summary>
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

    /// <summary>Verifies a culture whose default calendar cannot represent DateOnly.MaxValue is still
    /// assignable: the probe date must be representable by every supported calendar, not just the
    /// Gregorian range, since UmAlQuraCalendar's max supported date (2077-11-16) is exceeded by
    /// DateOnly.MaxValue (9999-12-31), which previously rejected these otherwise-valid cultures
    /// with an internal ArgumentOutOfRangeException instead of laying out correctly (see #182).</summary>
    [Theory]
    [InlineData("ar-SA")]
    [InlineData("en-SA")]
    public void Culture_WhenDefaultCalendarCannotRepresentDateOnlyMaxValue_IsAssignable(string cultureName)
    {
        // Arrange
        using var control = new DateInput { Value = new DateOnly(2026, 7, 19) };
        var culture = new CultureInfo(cultureName);

        // Act and assert
        _ = Should.NotThrow(() => control.Culture = culture);
        control.Culture.ShouldBeSameAs(culture);
        new LayoutEngine().Layout(control, new Size(30, 3));
        using Frame frame = new(new Size(30, 3));
        Should.NotThrow(() => control.Render(frame.Canvas));
    }

    /// <summary>Verifies a time-bearing Format is still rejected under a culture whose default
    /// calendar is UmAlQura, and that the exception surfaced is the documented ArgumentException -
    /// this culture's own probe boundary must not change which patterns are valid, only which
    /// culture assignments succeed (see #182).</summary>
    [Fact]
    public void Format_WhenCultureUsesUmAlQuraCalendarAndPatternHasTimeSpecifier_ThrowsArgumentException()
    {
        // Arrange
        using var control = new DateInput { Culture = new CultureInfo("ar-SA") };

        // Act and assert
        var thrown = Should.Throw<ArgumentException>(() => control.Format = "HH:mm");
        thrown.ShouldNotBeOfType<ArgumentOutOfRangeException>();
        control.Format.ShouldBe("d");
    }

    /// <summary>Verifies setting an invalid Format while Value is still null does not arm a later
    /// crash: the setter rejects it immediately regardless of the current Value, so there is
    /// nothing left to detonate when Value or Culture changes afterward (see #182).</summary>
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
    /// only republishing the private Popup's Closed notification (see #191).</summary>
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
