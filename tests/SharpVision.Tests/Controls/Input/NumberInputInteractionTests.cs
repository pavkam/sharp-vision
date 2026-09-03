// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using SharpVision.Tests.Support;

/// <summary>Verifies NumberInput's typed-buffer editing, commit, revert, and stepping through a
/// mounted surface, asserting the rendered digits and cursor alongside the committed value.</summary>
public sealed class NumberInputInteractionTests
{
    private static Task<ComponentSurface> MountAsync(ControlBase control, int width = 12, int height = 1) =>
        ComponentSurface.MountAsync(
            control,
            new Size(width, height),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

    private static string Row(ComponentSurface surface, int width, int y = 0)
    {
        var row = new StringBuilder();

        for (var x = 0; x < width; x++)
        {
            _ = row.Append(surface.Cell(new Point(x, y)).Text);
        }

        return row.ToString().TrimEnd();
    }

    /// <summary>Verifies every character the partial-number grammar rejects - letters, a misplaced
    /// sign, a second decimal separator, whitespace - leaves the buffer, cells, and value untouched,
    /// while the accepted digits still commit on Enter.</summary>
    [Fact]
    public async Task Keyboard_WhenInvalidCharactersAreTyped_RejectsEachWithoutChangingTheBufferAsync()
    {
        // Arrange
        var input = new NumberInput();
        var raised = 0;
        input.ValueChanged += (_, _) => raised++;
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act and assert - accepted prefix
        await surface.Keyboard.TypeAsync("1.");
        Row(surface, 12).ShouldBe("1.");
        surface.ShouldHaveCursor(new Point(2, 0), visible: true);

        // Act and assert - each rejected character
        foreach (var rejected in new[] { "a", "-", ".", " ", "+", "," })
        {
            await surface.Keyboard.TypeAsync(rejected);
            Row(surface, 12).ShouldBe("1.", $"'{rejected}' should have been rejected");
            surface.ShouldHaveCursor(new Point(2, 0), visible: true);
        }

        input.Value.ShouldBeNull();
        raised.ShouldBe(0);

        // Act and assert - the accepted digits still commit
        await surface.Keyboard.TypeAsync("5");
        await surface.Keyboard.PressAsync(Code.Enter);
        input.Value.ShouldBe(1.5m);
        raised.ShouldBe(1);
        Row(surface, 12).ShouldBe("1.50");
        surface.ShouldHaveCursor(new Point(4, 0), visible: true);
    }

    /// <summary>Verifies an out-of-range typed value is shown raw while typing and clamped into
    /// Minimum/Maximum only when Enter commits it.</summary>
    [Fact]
    public async Task Keyboard_WhenTypedValueIsOutOfRange_ShowsRawThenClampsOnCommitAsync()
    {
        // Arrange
        var input = new NumberInput { Minimum = 0m, Maximum = 100m };
        var changes = new List<(decimal? Previous, decimal? Value)>();
        input.ValueChanged += (_, eventArgs) => changes.Add((eventArgs.PreviousValue, eventArgs.Value));
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act and assert - above the maximum
        await surface.Keyboard.TypeAsync("500");
        Row(surface, 12).ShouldBe("500");
        input.Value.ShouldBeNull();
        await surface.Keyboard.PressAsync(Code.Enter);
        input.Value.ShouldBe(100m);
        Row(surface, 12).ShouldBe("100.00");
        surface.ShouldHaveCursor(new Point(6, 0), visible: true);

        // Act and assert - below the minimum, replacing the reloaded buffer
        for (var i = 0; i < 6; i++)
        {
            await surface.Keyboard.PressAsync(Code.Backspace);
        }

        await surface.Keyboard.TypeAsync("-5");
        Row(surface, 12).ShouldBe("-5");
        await surface.Keyboard.PressAsync(Code.Enter);
        input.Value.ShouldBe(0m);
        Row(surface, 12).ShouldBe("0.00");
        changes.ShouldBe([(null, 100m), (100m, 0m)]);
    }

    /// <summary>Verifies PageUp, PageDown, and the wheel are not numeric commands: the value and
    /// buffer stay untouched and the keys bubble unhandled.</summary>
    [Fact]
    public async Task Keyboard_WhenPageKeysOrWheelArrive_LeaveValueAndBufferUntouchedAsync()
    {
        // Arrange
        var input = new NumberInput { Value = 5m };
        var unhandled = new List<Code>();
        input.KeyDown += (_, eventArgs) =>
        {
            if (!eventArgs.IsHandled)
            {
                unhandled.Add(eventArgs.Stroke.Code);
            }
        };
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("1");
        Row(surface, 12).ShouldBe("5.001");
        unhandled.Clear();

        // Act
        await surface.Keyboard.PressAsync(Code.PageUp);
        await surface.Keyboard.PressAsync(Code.PageDown);
        await surface.Pointer.WheelAsync(input, default, wheelY: 1);
        await surface.Pointer.WheelAsync(input, default, wheelY: -1);

        // Assert
        input.Value.ShouldBe(5m);
        Row(surface, 12).ShouldBe("5.001");
        unhandled.ShouldBe([Code.PageUp, Code.PageDown]);
    }

    /// <summary>Verifies committing an emptied buffer clears the value when null is allowed and
    /// renders blank, but reverts to the prior value when null is not allowed.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Keyboard_WhenEmptiedBufferIsCommitted_ClearsOrRevertsPerNullPolicyAsync(bool allowNull)
    {
        // Arrange
        var input = new NumberInput { Value = 5m, DecimalPlaces = 0, AllowNull = allowNull };
        var changes = new List<(decimal? Previous, decimal? Value)>();
        input.ValueChanged += (_, eventArgs) => changes.Add((eventArgs.PreviousValue, eventArgs.Value));
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Backspace);
        Row(surface, 12).ShouldBe(string.Empty);
        surface.ShouldHaveCursor(new Point(0, 0), visible: true);

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        if (allowNull)
        {
            input.Value.ShouldBeNull();
            Row(surface, 12).ShouldBe(string.Empty);
            changes.ShouldBe([(5m, null)]);
        }
        else
        {
            input.Value.ShouldBe(5m);
            Row(surface, 12).ShouldBe("5");
            changes.ShouldBeEmpty();
        }
    }

    /// <summary>Verifies Left and Right move the caret inside the buffer (clamped at both ends),
    /// the cursor follows, and a digit typed mid-buffer inserts at the caret.</summary>
    [Fact]
    public async Task Keyboard_WhenLeftAndRightMoveTheCaret_RendersCursorAndInsertsThereAsync()
    {
        // Arrange
        var input = new NumberInput { Value = 12m, DecimalPlaces = 0 };
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveCursor(new Point(2, 0), visible: true);

        // Act and assert - Left then insert
        await surface.Keyboard.PressAsync(Code.Left);
        surface.ShouldHaveCursor(new Point(1, 0), visible: true);
        await surface.Keyboard.TypeAsync("9");
        Row(surface, 12).ShouldBe("192");
        surface.ShouldHaveCursor(new Point(2, 0), visible: true);

        // Act and assert - Left clamps at the start, Right clamps at the end
        for (var i = 0; i < 5; i++)
        {
            await surface.Keyboard.PressAsync(Code.Left);
        }

        surface.ShouldHaveCursor(new Point(0, 0), visible: true);
        await surface.Keyboard.PressAsync(Code.Delete);
        Row(surface, 12).ShouldBe("92");

        for (var i = 0; i < 5; i++)
        {
            await surface.Keyboard.PressAsync(Code.Right);
        }

        surface.ShouldHaveCursor(new Point(2, 0), visible: true);
        await surface.Keyboard.PressAsync(Code.Enter);
        input.Value.ShouldBe(92m);
    }

    /// <summary>Verifies a typed value with more fractional digits than DecimalPlaces rounds on
    /// commit under the configured midpoint mode.</summary>
    [Theory]
    [InlineData(MidpointRounding.AwayFromZero, "1.005", "1.01")]
    [InlineData(MidpointRounding.ToEven, "1.005", "1.00")]
    [InlineData(MidpointRounding.ToZero, "1.999", "1.99")]
    public async Task Keyboard_WhenTypedValueNeedsRounding_RoundsToDecimalPlacesOnCommitAsync(
        MidpointRounding mode,
        string typed,
        string expected)
    {
        // Arrange
        var input = new NumberInput { DecimalPlaces = 2, RoundingMode = mode };
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.TypeAsync(typed);
        Row(surface, 12).ShouldBe(typed);
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        input.Value.ShouldBe(decimal.Parse(expected, CultureInfo.InvariantCulture));
        Row(surface, 12).ShouldBe(expected);
    }

    /// <summary>Verifies assigning Value while focused with uncommitted typing discards the buffer,
    /// reloads the formatted value with the caret at its end, and raises ValueChanged once.</summary>
    [Fact]
    public async Task Value_WhenAssignedWhileFocused_ReloadsBufferAndCursorAsync()
    {
        // Arrange
        var input = new NumberInput();
        var changes = new List<(decimal? Previous, decimal? Value)>();
        input.ValueChanged += (_, eventArgs) => changes.Add((eventArgs.PreviousValue, eventArgs.Value));
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("12");

        // Act
        await surface.UpdateAsync(() => input.Value = 7m, "assign value while typing");

        // Assert
        Row(surface, 12).ShouldBe("7.00");
        surface.ShouldHaveCursor(new Point(4, 0), visible: true);
        changes.ShouldBe([(null, 7m)]);

        // Act and assert - Enter commits the reloaded buffer, not the discarded typing
        await surface.Keyboard.PressAsync(Code.Enter);
        input.Value.ShouldBe(7m);
        changes.Count.ShouldBe(1);
    }

    /// <summary>Verifies disabling the control mid-edit drops focus, which commits the buffer like
    /// any other focus loss, hides the cursor, and ignores further typing.</summary>
    [Fact]
    public async Task IsEnabled_WhenClearedMidEdit_CommitsOnFocusLossAndIgnoresTypingAsync()
    {
        // Arrange
        var input = new NumberInput { DecimalPlaces = 0 };
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("42");
        input.Value.ShouldBeNull();

        // Act
        await surface.UpdateAsync(() => input.IsEnabled = false, "disable mid-edit");

        // Assert
        surface.ShouldHaveFocus(null);
        input.Value.ShouldBe(42m);
        Row(surface, 12).ShouldBe("42");
        surface.ShouldHaveCursor(default, visible: false);
        surface.ShouldHaveState(input, VisualState.Disabled);
        await surface.Keyboard.TypeAsync("9");
        input.Value.ShouldBe(42m);
        Row(surface, 12).ShouldBe("42");
    }

    /// <summary>Verifies German separators type, commit, and render grouped under de-DE, and that
    /// AllowGrouping only affects display.</summary>
    [Fact]
    public async Task Culture_WhenGermanSeparatorsAreTyped_ParsesAndRendersGroupedAsync()
    {
        // Arrange
        var input = new NumberInput { Culture = CultureInfo.GetCultureInfo("de-DE") };
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.TypeAsync("1234,5");
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        input.Value.ShouldBe(1234.5m);
        Row(surface, 12).ShouldBe("1.234,50");

        // Act and assert - a typed '.' is the group separator, accepted and stripped
        await surface.Keyboard.PressAsync(Code.Escape);
        for (var i = 0; i < 8; i++)
        {
            await surface.Keyboard.PressAsync(Code.Backspace);
        }

        await surface.Keyboard.TypeAsync("2.000");
        await surface.Keyboard.PressAsync(Code.Enter);
        input.Value.ShouldBe(2000m);

        // Act and assert - AllowGrouping changes only the display: the live buffer keeps its
        // loaded text until the next reload, which Escape forces
        await surface.UpdateAsync(() => input.AllowGrouping = false, "disable grouping");
        Row(surface, 12).ShouldBe("2.000,00");
        await surface.Keyboard.PressAsync(Code.Escape);
        Row(surface, 12).ShouldBe("2000,00");
        input.Value.ShouldBe(2000m);
    }

    /// <summary>Verifies clicking an affix cell places the caret at the buffer start and clicking
    /// past the value places it at the end.</summary>
    [Fact]
    public async Task Pointer_WhenAffixOrTrailingSpaceIsClicked_ClampsTheCaretAsync()
    {
        // Arrange
        var input = new NumberInput { Value = 12m, DecimalPlaces = 0, Minimum = 0m, Maximum = 99m, StartAffix = new Affix("#") };
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("#");
        var valueX = 0;

        while (surface.Cell(new Point(valueX, 0)).Text != "1")
        {
            valueX++;
        }

        surface.ShouldHaveCursor(new Point(valueX + 2, 0), visible: true);

        // Act and assert - affix cell
        await surface.Pointer.ClickAsync(input, new Point(0, 0));
        surface.ShouldHaveCursor(new Point(valueX, 0), visible: true);
        await surface.Keyboard.TypeAsync("9");
        Row(surface, 12).ShouldBe("# 912");
        surface.ShouldHaveCursor(new Point(valueX + 1, 0), visible: true);
        await surface.Keyboard.PressAsync(Code.Delete);
        Row(surface, 12).ShouldBe("# 92");

        // Act and assert - past the value
        await surface.Pointer.ClickAsync(input, new Point(input.Bounds.Width - 1, 0));
        surface.ShouldHaveCursor(new Point(valueX + 2, 0), visible: true);
    }

    /// <summary>Verifies Up and Down step from zero when the value is null, clamp at the bounds,
    /// and raise ValueChanged with the previous and new values.</summary>
    [Fact]
    public async Task Keyboard_WhenSteppingFromNull_StartsAtZeroAndClampsAsync()
    {
        // Arrange
        var input = new NumberInput { Minimum = -1m, Maximum = 1m, DecimalPlaces = 0 };
        var changes = new List<(decimal? Previous, decimal? Value)>();
        input.ValueChanged += (_, eventArgs) => changes.Add((eventArgs.PreviousValue, eventArgs.Value));
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act and assert
        await surface.Keyboard.PressAsync(Code.Down);
        input.Value.ShouldBe(-1m);
        Row(surface, 12).ShouldBe("-1");
        await surface.Keyboard.PressAsync(Code.Down);
        input.Value.ShouldBe(-1m);
        await surface.Keyboard.PressAsync(Code.Up);
        await surface.Keyboard.PressAsync(Code.Up);
        await surface.Keyboard.PressAsync(Code.Up);
        input.Value.ShouldBe(1m);
        Row(surface, 12).ShouldBe("1");
        changes.ShouldBe([(null, -1m), (-1m, 0m), (0m, 1m)]);

        // Act and assert - a step commits immediately, so Escape has nothing to revert
        await surface.Keyboard.PressAsync(Code.Escape);
        input.Value.ShouldBe(1m);
    }

    /// <summary>Verifies only one leading sign is accepted and a sign typed anywhere else is
    /// rejected, before and after a commit reloads the buffer.</summary>
    [Fact]
    public async Task Keyboard_WhenSignIsTyped_AcceptsOnlyOneLeadingSignAsync()
    {
        // Arrange
        var input = new NumberInput { DecimalPlaces = 0 };
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act and assert
        await surface.Keyboard.TypeAsync("-");
        Row(surface, 12).ShouldBe("-");
        await surface.Keyboard.TypeAsync("-");
        Row(surface, 12).ShouldBe("-");
        await surface.Keyboard.TypeAsync("5");
        await surface.Keyboard.TypeAsync("+");
        Row(surface, 12).ShouldBe("-5");
        await surface.Keyboard.PressAsync(Code.Enter);
        input.Value.ShouldBe(-5m);

        // Act and assert - a bare sign never commits
        await surface.Keyboard.PressAsync(Code.Backspace);
        await surface.Keyboard.PressAsync(Code.Backspace);
        await surface.Keyboard.TypeAsync("+");
        await surface.Keyboard.PressAsync(Code.Enter);
        input.Value.ShouldBe(-5m);
        Row(surface, 12).ShouldBe("-5");
    }

    /// <summary>Verifies Integer mode rejects the decimal separator while typing and commits whole
    /// numbers, and that switching to Decimal re-admits it.</summary>
    [Fact]
    public async Task Keyboard_WhenModeIsInteger_RejectsDecimalSeparatorUntilModeChangesAsync()
    {
        // Arrange
        var input = new NumberInput { Mode = NumberInputMode.Integer };
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act and assert
        await surface.Keyboard.TypeAsync("12.5");
        Row(surface, 12).ShouldBe("125");
        await surface.Keyboard.PressAsync(Code.Enter);
        input.Value.ShouldBe(125m);

        await surface.UpdateAsync(() => input.Mode = NumberInputMode.Decimal, "switch to decimal");
        Row(surface, 12).ShouldBe("125.00");
        await surface.Keyboard.TypeAsync("5");
        Row(surface, 12).ShouldBe("125.005");
        await surface.Keyboard.PressAsync(Code.Enter);
        input.Value.ShouldBe(125.01m);
    }

    /// <summary>Verifies a bracketed paste of a grouped decimal is accepted whole and a paste
    /// containing a line break is rejected whole.</summary>
    [Fact]
    public async Task Paste_WhenGroupedDecimalOrBrokenTextIsPasted_AcceptsOrRejectsWholeAsync()
    {
        // Arrange
        var input = new NumberInput();
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act and assert
        await surface.Keyboard.PasteAsync("1,234.5");
        Row(surface, 12).ShouldBe("1,234.5");
        await surface.Keyboard.PasteAsync("9\n9");
        Row(surface, 12).ShouldBe("1,234.5");
        await surface.Keyboard.PressAsync(Code.Enter);
        input.Value.ShouldBe(1234.5m);
        Row(surface, 12).ShouldBe("1,234.50");
    }

    /// <summary>Verifies Tab out of a NumberInput commits and Shift+Tab back in reloads the
    /// committed formatting with the caret at the end.</summary>
    [Fact]
    public async Task Focus_WhenLeavingAndReturningByKeyboard_CommitsThenReloadsAsync()
    {
        // Arrange
        var input = new NumberInput { DecimalPlaces = 1 };
        var button = new Button { Text = "Go", Height = Length.Cells(1) };
        var root = new Stack { Children = { input, button } };
        await using var surface = await MountAsync(root, 12, 2);
        await surface.FocusAsync(input);
        await surface.Keyboard.TypeAsync("3");

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.ShouldHaveFocus(button);
        input.Value.ShouldBe(3m);
        Row(surface, 12).ShouldBe("3.0");
        surface.ShouldHaveCursor(default, visible: false);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);

        // Assert
        surface.ShouldHaveFocus(input);
        surface.ShouldHaveCursor(new Point(3, 0), visible: true);
        await surface.Keyboard.TypeAsync("5");
        Row(surface, 12).ShouldBe("3.05");
    }

    /// <summary>Verifies an auto-sized field whose committed value is as wide as its widest bound
    /// still shows the end-of-buffer caret: the measured width reserves one cell past the widest
    /// formatted bound. Before the fix the caret column fell outside the value box and the cursor
    /// vanished exactly where every focus gain and commit places it.</summary>
    [Fact]
    public async Task Render_WhenValueFillsTheWidestBound_KeepsTheEndCaretVisibleAsync()
    {
        // Arrange
        var input = new NumberInput { Minimum = 0m, Maximum = 99m, Value = 99m, DecimalPlaces = 0 };
        await using var surface = await MountAsync(input);
        input.Bounds.Width.ShouldBe(3);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        Row(surface, 12).ShouldBe("99");
        surface.ShouldHaveCursor(new Point(2, 0), visible: true);
        await surface.Keyboard.PressAsync(Code.Left);
        surface.ShouldHaveCursor(new Point(1, 0), visible: true);
    }
}
