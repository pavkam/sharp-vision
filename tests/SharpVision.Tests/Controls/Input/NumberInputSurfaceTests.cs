// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves NumberInput appearance and interaction through mounted terminal surfaces.</summary>
public sealed class NumberInputSurfaceTests
{
    /// <summary>Verifies a mounted NumberInput renders a bordered field with a formatted value, and
    /// observes hover and focus.</summary>
    [ComponentBehaviorEvidence(
        typeof(NumberInput),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded)]
    [Fact]
    public async Task Render_WhenNumberInputIsMounted_DrawsBorderedFieldWithValueAsync()
    {
        // Arrange
        var input = new NumberInput { Value = 42m, DecimalPlaces = 0 };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Assert - bordered field renders
        input.Bounds.Width.ShouldBeGreaterThan(0);
        input.Bounds.Height.ShouldBeGreaterThan(0);
        surface.Cell(default).Text.ShouldBe("┏");

        // Assert - hover and focus behavior
        await surface.Pointer.MoveToAsync(input);
        input.IsPointerOver.ShouldBeTrue();
        await surface.Keyboard.PressAsync(Code.Tab);
        input.IsFocused.ShouldBeTrue();
    }

    /// <summary>Verifies typing changes the displayed text without touching the committed Value,
    /// and that Tab away to a sibling control commits the buffer.</summary>
    [Fact]
    public async Task Keyboard_WhenTypedThenTabAway_ChangesDisplayThenCommitsAsync()
    {
        // Arrange - a second focusable sibling so Tab genuinely moves focus away instead of
        // cycling back to the same lone control.
        var input = new NumberInput { Value = 5m, DecimalPlaces = 0 };
        var sibling = new NumberInput { Value = 0m, DecimalPlaces = 0 };
        var root = new Stack { Children = { input, sibling } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 2),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(input);

        // Act - type a digit without committing yet; the buffer starts preloaded with the
        // formatted committed value ("5"), so this appends rather than replacing it.
        await surface.Keyboard.TypeAsync("9");

        // Assert - display changed, Value has not
        input.Value.ShouldBe(5m);
        surface.Cell(default).Text.ShouldBe("5");
        surface.Cell(new Point(1, 0)).Text.ShouldBe("9");

        // Act - Tab away to the sibling commits the buffer
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.ShouldHaveFocus(sibling);
        input.Value.ShouldBe(59m);
    }

    /// <summary>Verifies Enter commits the typed buffer while keeping focus.</summary>
    [Fact]
    public async Task Keyboard_WhenEnterIsPressed_CommitsTypedValueAsync()
    {
        // Arrange - AllowNull defaults to true and no Value is assigned, so the buffer starts
        // empty on focus instead of preloaded with a formatted value.
        var input = new NumberInput { DecimalPlaces = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("23");

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        input.Value.ShouldBe(23m);
        input.IsFocused.ShouldBeTrue();
    }

    /// <summary>Verifies Escape discards uncommitted typed edits and reverts the display to the
    /// committed value without raising ValueChanged.</summary>
    [Fact]
    public async Task Keyboard_WhenEscapeIsPressed_RevertsWithoutCommittingAsync()
    {
        // Arrange
        var input = new NumberInput { Value = 7m, DecimalPlaces = 0 };
        var raised = 0;
        input.ValueChanged += (_, _) => raised++;
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("99");

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        input.Value.ShouldBe(7m);
        raised.ShouldBe(0);
        surface.Cell(default).Text.ShouldBe("7");
    }

    /// <summary>Verifies Up increments by the configured step, committing immediately with no
    /// buffer involved.</summary>
    [Fact]
    public async Task Keyboard_WhenUpIsPressed_IncrementsByStepAndCommitsImmediatelyAsync()
    {
        // Arrange
        var input = new NumberInput { Value = 5m, Step = 2m, DecimalPlaces = 0 };
        var events = new List<decimal?>();
        input.ValueChanged += (_, eventArgs) => events.Add(eventArgs.Value);
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert
        input.Value.ShouldBe(7m);
        events.ShouldBe([7m]);
    }

    /// <summary>Verifies Down decrements by the configured step.</summary>
    [Fact]
    public async Task Keyboard_WhenDownIsPressed_DecrementsByStepAsync()
    {
        // Arrange
        var input = new NumberInput { Value = 5m, Step = 2m, DecimalPlaces = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        input.Value.ShouldBe(3m);
    }

    /// <summary>Verifies Home jumps directly to Minimum - a value jump, not a caret jump.</summary>
    [Fact]
    public async Task Keyboard_WhenHomeIsPressed_JumpsToMinimumAsync()
    {
        // Arrange
        var input = new NumberInput { Value = 5m, Minimum = 0m, Maximum = 10m, DecimalPlaces = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Home);

        // Assert
        input.Value.ShouldBe(0m);
    }

    /// <summary>Verifies End jumps directly to Maximum.</summary>
    [Fact]
    public async Task Keyboard_WhenEndIsPressed_JumpsToMaximumAsync()
    {
        // Arrange
        var input = new NumberInput { Value = 5m, Minimum = 0m, Maximum = 10m, DecimalPlaces = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.End);

        // Assert
        input.Value.ShouldBe(10m);
    }

    /// <summary>Verifies Backspace removes the trailing typed digit from the buffer before commit.</summary>
    [Fact]
    public async Task Keyboard_WhenBackspaceIsPressed_RemovesTrailingDigitAsync()
    {
        // Arrange
        var input = new NumberInput { Value = 1m, DecimalPlaces = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("23");

        // Act
        await surface.Keyboard.PressAsync(Code.Backspace);
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        input.Value.ShouldBe(12m);
    }

    /// <summary>Verifies Delete removes the grapheme after the caret.</summary>
    [Fact]
    public async Task Keyboard_WhenDeleteIsPressedAfterHome_RemovesLeadingDigitAsync()
    {
        // Arrange - AllowNull defaults to true and no Value is assigned, so the buffer starts
        // empty on focus; typed buffer becomes "12", then Left moves the caret before the "2".
        var input = new NumberInput { DecimalPlaces = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("12");
        await surface.Keyboard.PressAsync(Code.Left);

        // Act
        await surface.Keyboard.PressAsync(Code.Delete);
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        input.Value.ShouldBe(1m);
    }

    /// <summary>Verifies typing the ASCII hyphen-minus is accepted as the negative sign.</summary>
    [Fact]
    public async Task Keyboard_WhenNegativeSignIsTyped_AcceptsItAsync()
    {
        // Arrange - AllowNull defaults to true and no Value is assigned, so the buffer starts
        // empty on focus instead of preloaded with a formatted value the sign would land after.
        var input = new NumberInput { DecimalPlaces = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("-5");

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        input.Value.ShouldBe(-5m);
    }

    /// <summary>Verifies typing a group separator is accepted and stripped at commit.</summary>
    [Fact]
    public async Task Keyboard_WhenGroupSeparatorIsTyped_AcceptsAndStripsItAsync()
    {
        // Arrange
        var input = new NumberInput { Value = 0m, DecimalPlaces = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("1,234");

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        input.Value.ShouldBe(1234m);
    }

    /// <summary>Verifies typing a decimal separator is accepted while Mode is Decimal.</summary>
    [Fact]
    public async Task Keyboard_WhenDecimalSeparatorIsTyped_AcceptsItAsync()
    {
        // Arrange - AllowNull defaults to true and no Value is assigned, so the buffer starts
        // empty on focus instead of preloaded with a formatted value already containing one.
        var input = new NumberInput { DecimalPlaces = 2 };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("1.5");

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        input.Value.ShouldBe(1.5m);
    }

    /// <summary>Verifies clicking a column places the caret there, proving grapheme-safe pointer hit
    /// testing rather than always landing at the buffer's end.</summary>
    [Fact]
    public async Task Pointer_WhenColumnIsClicked_PlacesCaretThereAsync()
    {
        // Arrange - committed value "12" is loaded into the buffer on focus; clicking column 0
        // then typing inserts before the existing text instead of appending after it.
        var input = new NumberInput { Value = 12m, DecimalPlaces = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(input, new Point(0, 0));
        await surface.Keyboard.TypeAsync("9");
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        input.Value.ShouldBe(912m);
    }

    /// <summary>Verifies pasting a valid whole payload replaces the selection.</summary>
    [Fact]
    public async Task Paste_WhenPayloadIsValid_ReplacesSelectionAsync()
    {
        // Arrange
        var input = new NumberInput { Value = 0m, DecimalPlaces = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Backspace);

        // Act
        await surface.Keyboard.PasteAsync("456");
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        input.Value.ShouldBe(456m);
    }

    /// <summary>Verifies pasting an invalid whole payload is rejected in full, without silently
    /// stripping the offending characters.</summary>
    [Fact]
    public async Task Paste_WhenPayloadIsInvalid_RejectsWholePasteAsync()
    {
        // Arrange
        var input = new NumberInput { Value = 1m, DecimalPlaces = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PasteAsync("12a34");
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert - the paste never applied, so the untouched committed "1" buffer commits unchanged.
        input.Value.ShouldBe(1m);
    }

    /// <summary>Verifies changing Culture mid-edit discards the in-progress transient buffer back to
    /// the committed value's formatting under the new culture, instead of migrating half-parsed
    /// text.</summary>
    [Fact]
    public async Task Culture_WhenChangedMidEdit_DiscardsTransientBufferAsync()
    {
        // Arrange
        var input = new NumberInput { Value = 1m, DecimalPlaces = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("9");

        // Act
        await surface.UpdateAsync(() => input.Culture = new CultureInfo("de-DE"), "change culture mid-edit");

        // Assert - the transient "19" is gone; the display now shows the committed value reformatted.
        surface.Cell(default).Text.ShouldBe("1");

        await surface.Keyboard.PressAsync(Code.Enter);
        input.Value.ShouldBe(1m);
    }

    /// <summary>Verifies changing Mode mid-edit discards the in-progress transient buffer, rather
    /// than migrating a decimal-bearing buffer into Integer mode.</summary>
    [Fact]
    public async Task Mode_WhenChangedMidEdit_DiscardsTransientBufferAsync()
    {
        // Arrange
        var input = new NumberInput { Value = 1m, DecimalPlaces = 2 };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync(".5");

        // Act
        await surface.UpdateAsync(() => input.Mode = NumberInputMode.Integer, "switch to Integer mid-edit");

        // Assert
        await surface.Keyboard.PressAsync(Code.Enter);
        input.Value.ShouldBe(1m);
    }

    /// <summary>Verifies ValueChanged fires exactly once per commit, not once per keystroke.</summary>
    [Fact]
    public async Task Commit_WhenMultipleDigitsAreTypedBeforeEnter_RaisesValueChangedExactlyOnceAsync()
    {
        // Arrange
        var input = new NumberInput { Value = 1m, DecimalPlaces = 0 };
        var raised = 0;
        input.ValueChanged += (_, _) => raised++;
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.TypeAsync("234");
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        raised.ShouldBe(1);
        input.Value.ShouldBe(1234m);
    }

    /// <summary>Verifies measuring and rendering at a tiny bound completes without throwing.</summary>
    [Fact]
    public async Task Layout_WhenBoundsAreTiny_CompletesWithoutThrowingAsync()
    {
        // Arrange
        var input = new NumberInput { Value = 12345m, DecimalPlaces = 0 };

        // Act and assert
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(1, 1),
            TestContext.Current.CancellationToken);
        _ = surface.Cell(default);
    }

    /// <summary>Verifies direct disable, ancestor-inherited disable, geometry stability across a
    /// genuine resize, and re-enable recovery all behave correctly on a mounted NumberInput.</summary>
    [ComponentBehaviorEvidence(typeof(NumberInput), ComponentBehavior.Disabled)]
    [Fact]
    public async Task Enabled_WhenDirectlyAndAncestorDisabledThenReenabled_TracksEffectiveStateAcrossResizeAsync()
    {
        // Arrange
        var input = new NumberInput { Value = 5m, DecimalPlaces = 0 };
        var root = new Stack { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Act and assert - direct disable
        await surface.UpdateAsync(() => input.IsEnabled = false, "disable NumberInput directly");
        input.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(input, VisualState.Disabled);

        // Act and assert - direct re-enable
        await surface.UpdateAsync(() => input.IsEnabled = true, "re-enable NumberInput directly");
        input.EffectiveIsEnabled.ShouldBeTrue();

        // Act and assert - ancestor disable inherits without touching the control's own flag
        await surface.UpdateAsync(() => root.IsEnabled = false, "disable ancestor Stack");
        input.EffectiveIsEnabled.ShouldBeFalse();
        input.IsEnabled.ShouldBeTrue();

        // Act and assert - a genuine resize (not a same-size no-op relayout) preserves geometry
        // sanity and the still-inherited disabled state.
        await surface.ResizeAsync(new Size(24, 5));
        input.EffectiveIsEnabled.ShouldBeFalse();
        input.Bounds.Width.ShouldBeGreaterThan(0);
        input.Bounds.Height.ShouldBeGreaterThan(0);

        // Act and assert - ancestor re-enable recovers effective state
        await surface.UpdateAsync(() => root.IsEnabled = true, "re-enable ancestor Stack");
        input.EffectiveIsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies that when a focused buffer's text exactly fills the content width, the
    /// caret column that would land one cell past it - on the right border's own cell - never
    /// reaches the terminal cursor, pinning the content-box clamp against the wider bordered
    /// canvas bounds.</summary>
    [Fact]
    public async Task Render_WhenBufferExactlyFillsContentWidth_NeverPlacesCursorOnTheBorderAsync()
    {
        // Arrange - a heavy all-side border (the default InputStyle) reserves one column on each
        // side, so a Width of 4 leaves exactly 2 content columns - precisely the length of "12".
        var input = new NumberInput { Value = 12m, DecimalPlaces = 0, Width = Length.Cells(4), Height = Length.Cells(3) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(4, 3),
            TestContext.Current.CancellationToken);

        // Act - focus preloads the buffer with "12" and places the caret at its end, one column
        // past the last content cell and exactly on the right border's own column.
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert - the border column never receives the terminal cursor.
        surface.ShouldHaveCursor(default, visible: false);
    }

    /// <summary>Verifies a mounted NumberInput with no affixes set starts drawing the value
    /// immediately inboard of the left border, with no reserved gap.</summary>
    [Fact]
    public async Task Render_WhenNoAffixesAreSet_ValueStartsImmediatelyAfterTheBorderAsync()
    {
        // Arrange
        var input = new NumberInput { Value = 5m, DecimalPlaces = 0, Width = Length.Cells(6), Height = Length.Cells(3) };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(6, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(1, 1)).Text.ShouldBe("5");
    }

    /// <summary>Verifies both affixes and the value each land at their exact, gap-separated
    /// columns when the content box is wide enough to hold all of them.</summary>
    [Fact]
    public async Task Render_WhenBothAffixesAreSetAndContentFits_RendersAffixesAndValueAtExactColumnsAsync()
    {
        // Arrange - content is 5 columns wide (Width 7 minus the 2-column border); each one-cell
        // affix reserves 2 columns (itself plus the theme's 1-cell AffixGap), leaving exactly 1
        // column for the single-digit value in the middle.
        var input = new NumberInput
        {
            Value = 5m,
            DecimalPlaces = 0,
            StartAffix = new Affix("S"),
            EndAffix = new Affix("E"),
            Width = Length.Cells(7),
            Height = Length.Cells(3)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(7, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(1, 1)).Text.ShouldBe("S");
        surface.Cell(new Point(3, 1)).Text.ShouldBe("5");
        surface.Cell(new Point(5, 1)).Text.ShouldBe("E");
    }

    /// <summary>Verifies that when the content box is too narrow to hold both affixes, the end
    /// affix drops whole before the start affix does, matching RenderAffixes' documented priority
    /// order.</summary>
    [Fact]
    public async Task Render_WhenContentIsTooNarrowForBothAffixes_DropsEndAffixFirstAsync()
    {
        // Arrange - content is exactly 1 column wide (Width 3 minus the 2-column border), enough
        // for the start affix alone but not for both.
        var input = new NumberInput
        {
            Value = 0m,
            DecimalPlaces = 0,
            StartAffix = new Affix("S"),
            EndAffix = new Affix("E"),
            Width = Length.Cells(3),
            Height = Length.Cells(3)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(3, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(1, 1)).Text.ShouldBe("S");
    }
}
