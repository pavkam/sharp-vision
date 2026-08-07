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
            Minimum = new DateOnly(2026, 7, 15),
            Maximum = new DateOnly(2026, 7, 25)
        };

        // Act
        control.Value = new DateOnly(2026, 7, 10);

        // Assert
        control.Value.ShouldBe(new DateOnly(2026, 7, 15));
    }

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

    /// <summary>Verifies a culture whose default calendar cannot represent DateOnly.MaxValue is still
    /// assignable: the probe date must be representable by every supported calendar, not just the
    /// Gregorian range, since UmAlQuraCalendar's max supported date (2077-11-16) is exceeded by
    /// DateOnly.MaxValue (9999-12-31), which previously rejected these otherwise-valid cultures
    /// with an internal ArgumentOutOfRangeException instead of laying out correctly.</summary>
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
    /// culture assignments succeed.</summary>
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
        key.Handled.ShouldBeFalse();
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

    /// <summary>Verifies Opened publishes PropertyChanged on open as well as on close, instead of
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
                if (eventArgs.PropertyName == nameof(DateInput.Opened))
                {
                    notifications.Add(control.Opened);
                }
            };

            control.Opened = true;
            control.Opened = false;

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

            control.Opened = true;

            opened.ShouldBe(1);
            closed.ShouldBe(0);

            control.Opened = false;

            opened.ShouldBe(1);
            closed.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
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

    private static KeyEventArgs KeyEvent(Code code) => new(new Stroke(
        code,
        default,
        nativeCode: 0,
        Modifiers.None,
        KeyAction.Press));

    #endregion
}
