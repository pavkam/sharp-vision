// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies CurrencyInput's culture-pattern composition, typed-buffer editing, commit,
/// revert, and stepping through a mounted surface, asserting rendered text and cursor.</summary>
public sealed class CurrencyInputInteractionTests
{
    private static Task<ComponentSurface> MountAsync(ControlBase control, int width = 20, int height = 1) =>
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

    private static CultureInfo InvariantWith(int? negativePattern = null, int? positivePattern = null)
    {
        var culture = (CultureInfo) CultureInfo.InvariantCulture.Clone();

        if (negativePattern is { } negative)
        {
            culture.NumberFormat.CurrencyNegativePattern = negative;
        }

        if (positivePattern is { } positive)
        {
            culture.NumberFormat.CurrencyPositivePattern = positive;
        }

        return culture;
    }

    /// <summary>Gets every CurrencyNegativePattern the runtime defines.</summary>
    public static TheoryData<int> NegativePatterns => [.. Enumerable.Range(0, 17)];

    /// <summary>Verifies each of the seventeen negative patterns renders identically idle and
    /// focused - the focused composition must match the runtime's own "C" formatting - and places
    /// the caret directly after the magnitude wherever the pattern puts it.</summary>
    [Theory]
    [MemberData(nameof(NegativePatterns))]
    public async Task Render_WhenNegativePatternVaries_ComposesLikeTheRuntimeAndPlacesCaretAfterMagnitudeAsync(int pattern)
    {
        // Arrange
        var culture = InvariantWith(negativePattern: pattern);
        var expected = (-1234.56m).ToString("C2", culture.NumberFormat);
        const string magnitude = "1,234.56";
        var input = new CurrencyInput { Culture = culture, Value = -1234.56m, DecimalPlaces = 2 };
        await using var surface = await MountAsync(input);

        // Assert - idle
        Row(surface, 20).ShouldBe(expected);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert - focused composition and caret
        Row(surface, 20).ShouldBe(expected);
        var caret = expected.IndexOf(magnitude, StringComparison.Ordinal) + magnitude.Length;
        surface.ShouldHaveCursor(new Point(caret, 0), visible: true);

        // Act and assert - Backspace edits the magnitude in place under the same pattern
        await surface.Keyboard.PressAsync(Code.Backspace);
        var edited = (-1234.5m).ToString("C1", culture.NumberFormat);
        Row(surface, 20).ShouldBe(edited);
        surface.ShouldHaveCursor(new Point(caret - 1, 0), visible: true);
    }

    /// <summary>Verifies each positive pattern composes like the runtime while focused and places
    /// the caret after the magnitude.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Render_WhenPositivePatternVaries_ComposesLikeTheRuntimeAsync(int pattern)
    {
        // Arrange
        var culture = InvariantWith(positivePattern: pattern);
        var expected = 1234.56m.ToString("C2", culture.NumberFormat);
        const string magnitude = "1,234.56";
        var input = new CurrencyInput { Culture = culture, Value = 1234.56m, DecimalPlaces = 2 };
        await using var surface = await MountAsync(input);
        Row(surface, 20).ShouldBe(expected);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        Row(surface, 20).ShouldBe(expected);
        var caret = expected.IndexOf(magnitude, StringComparison.Ordinal) + magnitude.Length;
        surface.ShouldHaveCursor(new Point(caret, 0), visible: true);
    }

    /// <summary>Verifies typing a sign then digits under the parenthesised accounting pattern
    /// composes the parentheses live, keeps the caret inside them, and commits negative.</summary>
    [Fact]
    public async Task Keyboard_WhenNegativeIsTypedUnderAccountingPattern_ComposesParenthesesLiveAsync()
    {
        // Arrange
        var culture = InvariantWith(negativePattern: 0);
        var symbol = culture.NumberFormat.CurrencySymbol;
        var input = new CurrencyInput { Culture = culture, DecimalPlaces = 2 };
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);
        Row(surface, 20).ShouldBe(symbol);
        surface.ShouldHaveCursor(new Point(1, 0), visible: true);

        // Act and assert - sign alone
        await surface.Keyboard.TypeAsync("-");
        Row(surface, 20).ShouldBe($"({symbol})");
        surface.ShouldHaveCursor(new Point(2, 0), visible: true);

        // Act and assert - digits
        await surface.Keyboard.TypeAsync("12");
        Row(surface, 20).ShouldBe($"({symbol}12)");
        surface.ShouldHaveCursor(new Point(4, 0), visible: true);

        // Act and assert - Left keeps the caret inside the core
        await surface.Keyboard.PressAsync(Code.Left);
        surface.ShouldHaveCursor(new Point(3, 0), visible: true);
        await surface.Keyboard.TypeAsync("0");
        Row(surface, 20).ShouldBe($"({symbol}102)");

        // Act and assert - commit
        await surface.Keyboard.PressAsync(Code.Enter);
        input.Value.ShouldBe(-102m);
        Row(surface, 20).ShouldBe($"({symbol}102.00)");
    }

    /// <summary>Verifies the currency symbol, letters, and a second separator are rejected while
    /// typing and never appear in the composed display.</summary>
    [Fact]
    public async Task Keyboard_WhenInvalidCharactersAreTyped_RejectsEachAsync()
    {
        // Arrange
        var input = new CurrencyInput { CurrencyOverride = "$", DecimalPlaces = 2 };
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("1.");
        Row(surface, 20).ShouldBe("$1.");

        // Act and assert
        foreach (var rejected in new[] { "$", "a", ".", "-", " " })
        {
            await surface.Keyboard.TypeAsync(rejected);
            Row(surface, 20).ShouldBe("$1.", $"'{rejected}' should have been rejected");
        }

        await surface.Keyboard.TypeAsync("5");
        await surface.Keyboard.PressAsync(Code.Enter);
        input.Value.ShouldBe(1.5m);
        Row(surface, 20).ShouldBe("$1.50");
    }

    /// <summary>Verifies clicking the symbol places the caret at the core start, clicking a digit
    /// places it before that digit, and clicking past the composed text places it at the end.</summary>
    [Fact]
    public async Task Pointer_WhenComposedCellsAreClicked_MapsIntoTheCoreAsync()
    {
        // Arrange - pattern 2 puts a space between the symbol and the number
        var culture = InvariantWith(positivePattern: 2);
        var input = new CurrencyInput { Culture = culture, Value = 1234m, DecimalPlaces = 0 };
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);
        Row(surface, 20).ShouldBe("¤ 1,234");
        surface.ShouldHaveCursor(new Point(7, 0), visible: true);

        // Act and assert - symbol cell
        await surface.Pointer.ClickAsync(input, new Point(0, 0));
        surface.ShouldHaveCursor(new Point(2, 0), visible: true);
        await surface.Keyboard.TypeAsync("9");
        Row(surface, 20).ShouldBe("¤ 91,234");

        // Act and assert - a digit cell
        await surface.Pointer.ClickAsync(input, new Point(5, 0));
        surface.ShouldHaveCursor(new Point(5, 0), visible: true);

        // Act and assert - past the end
        await surface.Pointer.ClickAsync(input, new Point(15, 0));
        surface.ShouldHaveCursor(new Point(8, 0), visible: true);
        await surface.Keyboard.PressAsync(Code.Enter);
        input.Value.ShouldBe(91234m);
    }

    /// <summary>Verifies switching the currency identity while focused re-composes the display
    /// immediately around the untouched buffer.</summary>
    [Fact]
    public async Task DisplayMode_WhenOverrideChangesWhileFocused_RecomposesAroundTheBufferAsync()
    {
        // Arrange
        var input = new CurrencyInput { Value = 12m, DecimalPlaces = 2 };
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("5");
        Row(surface, 20).ShouldBe("¤12.005");

        // Act
        await surface.UpdateAsync(() => input.CurrencyOverride = "USD", "override the symbol");

        // Assert
        Row(surface, 20).ShouldBe("USD12.005");
        surface.ShouldHaveCursor(new Point(9, 0), visible: true);

        // Act and assert - Custom mode keeps using the override; clearing it under Custom throws
        await surface.UpdateAsync(() => input.DisplayMode = CurrencyDisplayMode.Custom, "custom mode");
        Row(surface, 20).ShouldBe("USD12.005");
        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => surface.UpdateAsync(() => input.CurrencyOverride = null, "clear override under custom"));
        Row(surface, 20).ShouldBe("USD12.005");
        await surface.Keyboard.PressAsync(Code.Enter);
        input.Value.ShouldBe(12.01m);
        Row(surface, 20).ShouldBe("USD12.01");
    }

    /// <summary>Verifies assigning Value while focused with uncommitted typing reloads the buffer
    /// and re-composes with the caret after the magnitude.</summary>
    [Fact]
    public async Task Value_WhenAssignedWhileFocused_ReloadsBufferAndCursorAsync()
    {
        // Arrange
        var input = new CurrencyInput { DecimalPlaces = 2 };
        var changes = new List<(decimal? Previous, decimal? Value)>();
        input.ValueChanged += (_, eventArgs) => changes.Add((eventArgs.PreviousValue, eventArgs.Value));
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("99");

        // Act
        await surface.UpdateAsync(() => input.Value = -7m, "assign while typing");

        // Assert
        Row(surface, 20).ShouldBe("(¤7.00)");
        surface.ShouldHaveCursor(new Point(6, 0), visible: true);
        changes.ShouldBe([(null, -7m)]);
        await surface.Keyboard.PressAsync(Code.Enter);
        input.Value.ShouldBe(-7m);
        changes.Count.ShouldBe(1);
    }

    /// <summary>Verifies committing an emptied buffer clears or reverts per AllowNull, rendering
    /// blank only when cleared.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Keyboard_WhenEmptiedBufferIsCommitted_ClearsOrRevertsPerNullPolicyAsync(bool allowNull)
    {
        // Arrange
        var input = new CurrencyInput { Value = 5m, DecimalPlaces = 0, AllowNull = allowNull };
        var button = new Button { Text = "Go", Height = Length.Cells(1) };
        var root = new Stack { Children = { input, button } };
        await using var surface = await MountAsync(root, 20, 2);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(input);
        await surface.Keyboard.PressAsync(Code.Backspace);
        Row(surface, 20).ShouldBe("¤");

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        if (allowNull)
        {
            input.Value.ShouldBeNull();
            Row(surface, 20).ShouldBe("¤");
            await surface.Keyboard.PressAsync(Code.Tab);
            surface.ShouldHaveFocus(button);
            Row(surface, 20).ShouldBe(string.Empty);
        }
        else
        {
            input.Value.ShouldBe(5m);
            Row(surface, 20).ShouldBe("¤5");
        }
    }

    /// <summary>Verifies disabling mid-edit commits through focus loss, hides the cursor, and
    /// re-enabling then focusing reloads the committed value.</summary>
    [Fact]
    public async Task IsEnabled_WhenClearedMidEdit_CommitsOnFocusLossAsync()
    {
        // Arrange
        var input = new CurrencyInput { DecimalPlaces = 0 };
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("42");

        // Act
        await surface.UpdateAsync(() => input.IsEnabled = false, "disable mid-edit");

        // Assert
        input.Value.ShouldBe(42m);
        Row(surface, 20).ShouldBe("¤42");
        surface.ShouldHaveCursor(default, visible: false);
        await surface.Keyboard.TypeAsync("9");
        input.Value.ShouldBe(42m);

        // Act and assert - re-enable
        await surface.UpdateAsync(() => input.IsEnabled = true, "re-enable");
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(input);
        surface.ShouldHaveCursor(new Point(3, 0), visible: true);
    }

    /// <summary>Verifies PageUp, PageDown, and the wheel never touch the value or buffer.</summary>
    [Fact]
    public async Task Keyboard_WhenPageKeysOrWheelArrive_LeaveValueAndBufferUntouchedAsync()
    {
        // Arrange
        var input = new CurrencyInput { Value = 5m, DecimalPlaces = 0 };
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("1");

        // Act
        await surface.Keyboard.PressAsync(Code.PageUp);
        await surface.Keyboard.PressAsync(Code.PageDown);
        await surface.Pointer.WheelAsync(input, default, wheelY: 1);

        // Assert
        input.Value.ShouldBe(5m);
        Row(surface, 20).ShouldBe("¤51");
    }

    /// <summary>Verifies an out-of-range typed amount clamps on commit and Up/Down step from zero
    /// when null, clamping at the bounds.</summary>
    [Fact]
    public async Task Keyboard_WhenRangeIsBounded_ClampsCommitsAndStepsAsync()
    {
        // Arrange
        var input = new CurrencyInput { Minimum = -1m, Maximum = 10m, DecimalPlaces = 0 };
        var changes = new List<(decimal? Previous, decimal? Value)>();
        input.ValueChanged += (_, eventArgs) => changes.Add((eventArgs.PreviousValue, eventArgs.Value));
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act and assert - step from null
        await surface.Keyboard.PressAsync(Code.Down);
        input.Value.ShouldBe(-1m);
        Row(surface, 20).ShouldBe("(¤1)");
        await surface.Keyboard.PressAsync(Code.Down);
        input.Value.ShouldBe(-1m);

        // Act and assert - typed clamp
        await surface.Keyboard.PressAsync(Code.Backspace);
        await surface.Keyboard.PressAsync(Code.Backspace);
        await surface.Keyboard.TypeAsync("500");
        Row(surface, 20).ShouldBe("¤500");
        await surface.Keyboard.PressAsync(Code.Enter);
        input.Value.ShouldBe(10m);
        Row(surface, 20).ShouldBe("¤10");
        changes.ShouldBe([(null, -1m), (-1m, 10m)]);
    }

    /// <summary>Verifies de-DE typing with a comma decimal separator commits and renders the euro
    /// pattern with the symbol trailing, both idle and focused.</summary>
    [Fact]
    public async Task Culture_WhenGermanAmountIsTyped_RendersTrailingEuroPatternAsync()
    {
        // Arrange
        var culture = CultureInfo.GetCultureInfo("de-DE");
        var input = new CurrencyInput { Culture = culture };
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.TypeAsync("1234,5");
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        input.Value.ShouldBe(1234.5m);
        var expected = 1234.5m.ToString("C2", culture.NumberFormat);
        Row(surface, 20).ShouldBe(expected);
        surface.ShouldHaveCursor(new Point(expected.IndexOf("50", StringComparison.Ordinal) + 2, 0), visible: true);
        await surface.Keyboard.PressAsync(Code.Escape);
        Row(surface, 20).ShouldBe(expected);
    }

    /// <summary>Verifies Escape mid-edit restores the committed composition and cursor without
    /// raising ValueChanged, and a following Enter commits nothing new.</summary>
    [Fact]
    public async Task Keyboard_WhenEscapeIsPressedMidEdit_RestoresCompositionAndCursorAsync()
    {
        // Arrange
        var input = new CurrencyInput { Value = 3m, DecimalPlaces = 2 };
        var raised = 0;
        input.ValueChanged += (_, _) => raised++;
        await using var surface = await MountAsync(input);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("999");
        Row(surface, 20).ShouldBe("¤3.00999");

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        Row(surface, 20).ShouldBe("¤3.00");
        surface.ShouldHaveCursor(new Point(5, 0), visible: true);
        await surface.Keyboard.PressAsync(Code.Enter);
        input.Value.ShouldBe(3m);
        raised.ShouldBe(0);
    }

    /// <summary>Verifies an auto-sized field whose composed value is as wide as its widest bound
    /// still shows the end-of-buffer caret, because the measured width reserves one cell past
    /// the widest formatted bound.</summary>
    [Fact]
    public async Task Render_WhenValueFillsTheWidestBound_KeepsTheEndCaretVisibleAsync()
    {
        // Arrange
        var input = new CurrencyInput { Minimum = 0m, Maximum = 99m, Value = 99m, DecimalPlaces = 0 };
        await using var surface = await MountAsync(input);
        input.Bounds.Width.ShouldBe(4);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        Row(surface, 20).ShouldBe("¤99");
        surface.ShouldHaveCursor(new Point(3, 0), visible: true);
    }
}
