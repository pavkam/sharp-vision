// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves CurrencyInput appearance and interaction through mounted terminal surfaces.</summary>
public sealed class CurrencyInputSurfaceTests
{
    /// <summary>Verifies a mounted CurrencyInput renders a bordered field with a formatted value,
    /// and observes hover and focus.</summary>
    [Fact]
    public async Task Render_WhenCurrencyInputIsMounted_DrawsBorderedFieldWithValueAsync()
    {
        // Arrange
        var input = new CurrencyInput { Value = 42m, DecimalPlaces = 0 };

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
        // cycling back to the same lone control. Invariant currency identity is "¤", a single
        // narrow cell, so the leading affix cell is predictable.
        var input = new CurrencyInput { Value = 5m, DecimalPlaces = 0 };
        var sibling = new CurrencyInput { Value = 0m, DecimalPlaces = 0 };
        var root = new Stack { Children = { input, sibling } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 2),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(input);

        // Act - type a digit without committing yet; the buffer starts preloaded with the
        // formatted committed core ("5"), so this appends after it, and the affix stays "¤".
        await surface.Keyboard.TypeAsync("9");

        // Assert - display changed, Value has not
        input.Value.ShouldBe(5m);
        surface.Cell(default).Text.ShouldBe("¤");
        surface.Cell(new Point(1, 0)).Text.ShouldBe("5");
        surface.Cell(new Point(2, 0)).Text.ShouldBe("9");

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
        var input = new CurrencyInput { DecimalPlaces = 0 };
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
        var input = new CurrencyInput { Value = 7m, DecimalPlaces = 0 };
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
        surface.Cell(new Point(1, 0)).Text.ShouldBe("7");
    }

    /// <summary>Verifies Up increments by the configured step, committing immediately with no
    /// buffer involved.</summary>
    [Fact]
    public async Task Keyboard_WhenUpIsPressed_IncrementsByStepAndCommitsImmediatelyAsync()
    {
        // Arrange
        var input = new CurrencyInput { Value = 5m, Step = 2m, DecimalPlaces = 0 };
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
        var input = new CurrencyInput { Value = 5m, Step = 2m, DecimalPlaces = 0 };
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
        var input = new CurrencyInput { Value = 5m, Minimum = 0m, Maximum = 10m, DecimalPlaces = 0 };
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
        var input = new CurrencyInput { Value = 5m, Minimum = 0m, Maximum = 10m, DecimalPlaces = 0 };
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
        var input = new CurrencyInput { Value = 1m, DecimalPlaces = 0 };
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
    public async Task Keyboard_WhenDeleteIsPressedAfterLeft_RemovesLeadingDigitAsync()
    {
        // Arrange - AllowNull defaults to true and no Value is assigned, so the buffer starts
        // empty on focus; typed buffer becomes "12", then Left moves the caret before the "2".
        var input = new CurrencyInput { DecimalPlaces = 0 };
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

    /// <summary>Verifies clicking a column places the caret there, mapped through the affix-composed
    /// display back into the buffer's own numeric core, rather than always landing at its end.</summary>
    [Fact]
    public async Task Pointer_WhenColumnIsClicked_PlacesCaretThereAsync()
    {
        // Arrange - committed value "12" is loaded into the buffer on focus, and invariant's
        // Symbol identity "¤" occupies the leading cell. Clicking the first digit column, then
        // typing, inserts before the existing digits instead of appending after them.
        var input = new CurrencyInput { Value = 12m, DecimalPlaces = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(12, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        // Act - column 1 is the first digit cell, right after the "¤" affix at column 0.
        await surface.Pointer.ClickAsync(input, new Point(1, 0));
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
        var input = new CurrencyInput { Value = 0m, DecimalPlaces = 0 };
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
        var input = new CurrencyInput { Value = 1m, DecimalPlaces = 0 };
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
    /// text, and re-derives the effective decimal places.</summary>
    [Fact]
    public async Task Culture_WhenChangedMidEdit_DiscardsTransientBufferAsync()
    {
        // Arrange
        var input = new CurrencyInput { Value = 1m, DecimalPlaces = 0 };
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
        // de-DE's positive pattern is "n $" (digit, space, symbol), so the reformatted digit lands
        // at column 0, not after a leading symbol as under the default invariant "$n" pattern.
        surface.Cell(new Point(0, 0)).Text.ShouldBe("1");

        await surface.Keyboard.PressAsync(Code.Enter);
        input.Value.ShouldBe(1m);
    }

    /// <summary>Verifies ValueChanged fires exactly once per commit, not once per keystroke.</summary>
    [Fact]
    public async Task Commit_WhenMultipleDigitsAreTypedBeforeEnter_RaisesValueChangedExactlyOnceAsync()
    {
        // Arrange
        var input = new CurrencyInput { Value = 1m, DecimalPlaces = 0 };
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
        var input = new CurrencyInput { Value = 12345m, DecimalPlaces = 0 };

        // Act and assert
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(1, 1),
            TestContext.Current.CancellationToken);
        _ = surface.Cell(default);
    }

    /// <summary>Verifies direct disable, ancestor-inherited disable, geometry stability across a
    /// genuine resize, and re-enable recovery all behave correctly on a mounted CurrencyInput.</summary>
    [Fact]
    public async Task Enabled_WhenDirectlyAndAncestorDisabledThenReenabled_TracksEffectiveStateAcrossResizeAsync()
    {
        // Arrange
        var input = new CurrencyInput { Value = 5m, DecimalPlaces = 0 };
        var root = new Stack { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Act and assert - direct disable
        await surface.UpdateAsync(() => input.IsEnabled = false, "disable CurrencyInput directly");
        input.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(input, VisualState.Disabled);

        // Act and assert - direct re-enable
        await surface.UpdateAsync(() => input.IsEnabled = true, "re-enable CurrencyInput directly");
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

    #region Culture-aware rendering

    /// <summary>Verifies the idle display under Swiss German composes CurrencyNegativePattern 2
    /// ("$-n") with no separating space between the symbol and the sign, using the culture's own
    /// symbol and sign as the oracle instead of a hardcoded formatted literal.</summary>
    [Fact]
    public async Task Render_WhenCultureIsSwissGermanAndValueIsNegative_ComposesPatternWithNoSpacesAsync()
    {
        // Arrange
        var culture = new CultureInfo("de-CH");
        culture.NumberFormat.CurrencyNegativePattern.ShouldBe(2);
        var symbol = culture.NumberFormat.CurrencySymbol;
        var input = new CurrencyInput { Culture = culture, Value = -1234.5m };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        // Assert - the symbol renders first, cell by cell,
        for (var i = 0; i < symbol.Length; i++)
        {
            surface.Cell(new Point(i, 0)).Text.ShouldBe(symbol[i].ToString());
        }

        // Assert - and the sign directly follows it with no separating space cell.
        surface.Cell(new Point(symbol.Length, 0)).Text.ShouldBe(culture.NumberFormat.NegativeSign);
    }

    /// <summary>Verifies the idle display under Indian English groups digits using the [3, 2]
    /// lakh/crore CurrencyGroupSizes sequence rather than a hardcoded modulo-three rule, comparing
    /// against an oracle string computed through the same NumberFormatInfo-driven "C" formatting
    /// path rather than a hardcoded culture-formatted literal.</summary>
    [Fact]
    public async Task Render_WhenCultureIsIndianEnglish_GroupsUsingLakhCroreSizesAsync()
    {
        // Arrange
        var culture = new CultureInfo("en-IN");
        culture.NumberFormat.CurrencyGroupSizes.ShouldBe([3, 2]);
        var input = new CurrencyInput { Culture = culture, Value = 1234567m, DecimalPlaces = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        var oracle = (NumberFormatInfo) culture.NumberFormat.Clone();
        var expected = 1234567m.ToString("C0", oracle);

        // Assert
        for (var i = 0; i < expected.Length; i++)
        {
            surface.Cell(new Point(i, 0)).Text.ShouldBe(expected[i].ToString());
        }
    }

    /// <summary>Verifies the idle display under German resolving CurrencyDisplayMode.IsoCode through
    /// RegionInfo renders the three-letter ISO code instead of the euro symbol, comparing against an
    /// oracle string computed the same way the control itself would format it.</summary>
    [Fact]
    public async Task Render_WhenDisplayModeIsIsoCodeUnderGermanCulture_RendersResolvedIsoCodeAsync()
    {
        // Arrange
        var culture = new CultureInfo("de-DE");
        var input = new CurrencyInput
        {
            Culture = culture,
            DisplayMode = CurrencyDisplayMode.IsoCode,
            Value = 5m,
            DecimalPlaces = 0
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        var oracle = (NumberFormatInfo) culture.NumberFormat.Clone();
        oracle.CurrencySymbol = new RegionInfo(culture.Name).ISOCurrencySymbol;
        var expected = 5m.ToString("C0", oracle);

        // Assert
        for (var i = 0; i < expected.Length; i++)
        {
            surface.Cell(new Point(i, 0)).Text.ShouldBe(expected[i].ToString());
        }
    }

    /// <summary>Verifies a mounted CurrencyInput measures and renders its ambiguous-width currency
    /// symbol at two cells under an ambient Wide policy, instead of assuming every symbol is one
    /// cell wide. The euro sign is East Asian Ambiguous width, unlike most currency symbols.</summary>
    [Fact]
    public async Task Render_WhenCurrencySymbolIsAmbiguousWidthUnderWidePolicy_AccountsForDoubleWidthAsync()
    {
        // Arrange - de-DE's euro symbol ("€") is a single East Asian Ambiguous-width character,
        // rendered as two cells under Ambiguous.Wide.
        var input = new CurrencyInput { Culture = new CultureInfo("de-DE"), Value = 5m, DecimalPlaces = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 3),
            TerminalOptions.Minimal with
            {
                Capabilities = TerminalCapabilities.Conservative with { AmbiguousWidth = Ambiguous.Wide }
            },
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        // Assert - de-DE's positive pattern is "n $" (digit, space, symbol), so the euro sign
        // starts two columns after the single digit and its own leading space.
        surface.Cell(new Point(0, 0)).Text.ShouldBe("5");
        surface.Cell(new Point(1, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(2, 0)).Text.ShouldBe("€");

        // Assert - the symbol's second wide cell leaves no room for anything else at column 3
        // within this measured width, proving the two-cell glyph was actually accounted for.
        input.Bounds.Width.ShouldBeGreaterThanOrEqualTo(4);
    }

    #endregion

    #region Cursor and affix rendering

    /// <summary>Verifies that when a focused buffer's composed core exactly fills the content
    /// width, the caret column that would land one cell past it - on the right border's own cell -
    /// never reaches the terminal cursor, pinning the content-box clamp against the wider bordered
    /// canvas bounds.</summary>
    [Fact]
    public async Task Render_WhenComposedDisplayExactlyFillsContentWidth_NeverPlacesCursorOnTheBorderAsync()
    {
        // Arrange - a heavy all-side border reserves one column on each side, so a Width of 5
        // leaves exactly 3 content columns - precisely the length of the invariant-culture
        // composed display "¤12" ("$n" pattern, one-cell "¤").
        var input = new CurrencyInput { Value = 12m, DecimalPlaces = 0, Width = Length.Cells(5), Height = Length.Cells(3) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(5, 3),
            TestContext.Current.CancellationToken);

        // Act - focus preloads the buffer's core with "12" and places the caret at its end, one
        // column past the last content cell and exactly on the right border's own column.
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert - the border column never receives the terminal cursor.
        surface.ShouldHaveCursor(default, visible: false);
    }

    /// <summary>Verifies a mounted CurrencyInput with no affixes set starts drawing the composed
    /// value immediately inboard of the left border, with no reserved gap.</summary>
    [Fact]
    public async Task Render_WhenNoAffixesAreSet_ValueStartsImmediatelyAfterTheBorderAsync()
    {
        // Arrange
        var input = new CurrencyInput { Value = 5m, DecimalPlaces = 0, Width = Length.Cells(6), Height = Length.Cells(3) };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(6, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(1, 1)).Text.ShouldBe("¤");
        surface.Cell(new Point(2, 1)).Text.ShouldBe("5");
    }

    /// <summary>Verifies both affixes and the composed value each land at their exact,
    /// gap-separated columns when the content box is wide enough to hold all of them.</summary>
    [Fact]
    public async Task Render_WhenBothAffixesAreSetAndContentFits_RendersAffixesAndValueAtExactColumnsAsync()
    {
        // Arrange - content is 6 columns wide (Width 8 minus the 2-column border); each one-cell
        // affix reserves 2 columns (itself plus the theme's 1-cell AffixGap), leaving exactly 2
        // columns for the composed "¤5" value in the middle.
        var input = new CurrencyInput
        {
            Value = 5m,
            DecimalPlaces = 0,
            StartAffix = new Affix("S"),
            EndAffix = new Affix("E"),
            Width = Length.Cells(8),
            Height = Length.Cells(3)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(1, 1)).Text.ShouldBe("S");
        surface.Cell(new Point(3, 1)).Text.ShouldBe("¤");
        surface.Cell(new Point(4, 1)).Text.ShouldBe("5");
        surface.Cell(new Point(6, 1)).Text.ShouldBe("E");
    }

    /// <summary>Verifies that when the content box is too narrow to hold both affixes, the end
    /// affix drops whole before the start affix does, matching RenderAffixes' documented priority
    /// order.</summary>
    [Fact]
    public async Task Render_WhenContentIsTooNarrowForBothAffixes_DropsEndAffixFirstAsync()
    {
        // Arrange - content is exactly 1 column wide (Width 3 minus the 2-column border), enough
        // for the start affix alone but not for both.
        var input = new CurrencyInput
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

    #endregion
}
