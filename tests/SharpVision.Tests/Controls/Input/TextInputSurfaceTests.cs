// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies TextInput editing, policy, cursor, and cells through a mounted surface.</summary>
public sealed class TextInputSurfaceTests
{
    /// <summary>Verifies TextInput retains its semantic preference while an unsupported terminal keeps its shape.</summary>
    [Fact]
    public async Task Render_WhenCursorShapeIsConfigured_CommitsSemanticCursorShapeAsync()
    {
        var input = new TextInput
        {
            CursorShape = CursorShape.Bar,
            Width = Length.Cells(2),
            Height = Length.Cells(1)
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(2, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        await surface.Keyboard.PressAsync(Code.Tab);

        input.CursorShape.ShouldBe(CursorShape.Bar);
        surface.ShouldHaveCursor(default, visible: true, CursorShape.Block);
    }

    /// <summary>Verifies a supported TextInput shape crosses control, frame, profile bytes, and screen model.</summary>
    [Fact]
    public async Task Render_WhenCursorShapeIsSupported_ReachesTerminalScreenAsync()
    {
        var input = new TextInput
        {
            CursorShape = CursorShape.Bar,
            Width = Length.Cells(2),
            Height = Length.Cells(1)
        };
        var profile = new TerminalProfile(
            new Description("cursor-shape", DescriptionOrigin.Explicit, Suitability.Usable),
            TerminalCapabilities.Conservative,
            new Programs(new Dictionary<string, DescriptionProgram>
            {
                ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
                ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
                ["clear"] = new DescriptionProgram("\u001b[2J"u8),
                ["civis"] = new DescriptionProgram("\u001b[?25l"u8),
                ["cnorm"] = new DescriptionProgram("\u001b[?25h"u8),
                ["Ss"] = new DescriptionProgram("\u001b[%p1%d q"u8),
                ["Se"] = new DescriptionProgram("\u001b[0 q"u8)
            }),
            KeyMap.Empty);
        var options = TerminalOptions.Minimal with { Profile = profile };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(2, 1),
            options,
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        await surface.Keyboard.PressAsync(Code.Tab);

        surface.ShouldHaveCursor(default, visible: true, CursorShape.Bar);
    }

    /// <summary>Verifies placeholder, focus, raw Unicode typing, wide cells, and committed cursor.</summary>
    [Fact]
    public async Task Keyboard_WhenUnicodeTextIsTyped_ReplacesPlaceholderAndCommitsCursorAsync()
    {
        // Arrange
        var input = new TextInput { Placeholder = "Name", Width = Length.Cells(6), Height = Length.Cells(3) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(6, 3),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
                             ┏━━━━┓
                             ┃Name┃
                             ┗━━━━┛
                             """);
        input.GetResolvedAppearance(VisualState.Normal).BackgroundMode.ShouldBe(BackgroundMode.Opaque);
        surface.Cell(new Point(1, 1)).Style.Background.ShouldBe(ReferenceColors.Get(0));
        (surface.Cell(new Point(1, 1)).Style.Attributes & TerminalAttributes.Dim).ShouldBe(TerminalAttributes.Dim);
        surface.ShouldHaveCursor(default, visible: false);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("A\u0301界");

        // Assert
        input.Text.ShouldBe("A\u0301界");
        input.IsPressed.ShouldBeFalse();
        input.CaretIndex.ShouldBe(3);
        surface.ShouldHaveState(input, VisualState.Focused);
        surface.ShouldRender("""
                             ┏━━━━┓
                             ┃Á界 ┃
                             ┗━━━━┛
                             """);
        surface.Cell(new Point(1, 1)).Text.ShouldBe("A\u0301");
        surface.Cell(new Point(2, 1)).Text.ShouldBe("界");
        surface.Cell(new Point(3, 1)).Continuation.ShouldBeTrue();
        surface.ShouldHaveCursor(new Point(4, 1), visible: true);
    }

    /// <summary>Verifies dark-theme keyboard focus preserves a restrained transparent editor face.</summary>
    [Fact]
    public async Task Focus_WhenDarkThemeIsActive_RetainsRestrainedEditorBackgroundAsync()
    {
        // Arrange
        var input = new TextInput
        {
            AcceptsReturn = true,
            Text = "Readable text",
            Width = Length.Cells(16),
            Height = Length.Cells(3)
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(16, 3),
            TestContext.Current.CancellationToken);
        var theme = ThemeCatalog.Load("default-dark");
        await surface.UpdateAsync(() => surface.Application.Theme = theme, "apply default dark theme");
        var focusedForeground = ThemeColorHelper.FocusedForeground(theme);
        surface.Cell(new Point(1, 1)).Style.Background.ShouldBe(ReferenceColors.Get(0));

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.ShouldHaveState(input, VisualState.Focused);
        var expectedFocusedFg = TerminalPalette.Project(ThemeColorHelper.FocusedForeground(theme), ColorDepth.Basic16);
        surface.Cell(new Point(1, 1)).Style.Foreground.ShouldBe(expectedFocusedFg);
        surface.Cell(new Point(1, 1)).Style.Background.ShouldBe(ReferenceColors.Get(0));
    }

    /// <summary>Verifies navigation, Backspace, and Delete remove complete grapheme clusters.</summary>
    [Fact]
    public async Task Keyboard_WhenUnicodeClustersAreDeleted_NeverSplitsAGraphemeAsync()
    {
        // Arrange
        var input = new TextInput
        {
            Text = "A\u0301界B",
            Width = Length.Cells(6),
            Height = Length.Cells(1)
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(6, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Left);
        await surface.Keyboard.PressAsync(Code.Backspace);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Left);
        await surface.Keyboard.PressAsync(Code.Delete);

        // Assert
        input.Text.ShouldBe("A\u0301");
        input.CaretIndex.ShouldBe(2);
        surface.ShouldRender("A\u0301");
        surface.Cell(default).Text.ShouldBe("A\u0301");
        surface.ShouldHaveCursor(new Point(1, 0), visible: true);
    }

    /// <summary>Verifies Home and End remain on complete boundaries of the active logical line.</summary>
    [Fact]
    public async Task Keyboard_WhenHomeAndEndArePressed_MovesAcrossTheCurrentUnicodeLineAsync()
    {
        // Arrange
        var input = new TextInput
        {
            AcceptsReturn = true,
            Text = "A\u0301界\nXYZ",
            Width = Length.Cells(5),
            Height = Length.Cells(2)
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(5, 2),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act Home
        await surface.Keyboard.PressAsync(Code.Home);

        // Assert Home
        input.CaretIndex.ShouldBe(4);
        surface.ShouldHaveCursor(new Point(0, 1), visible: true);

        // Act End
        await surface.Keyboard.PressAsync(Code.End);

        // Assert End
        input.CaretIndex.ShouldBe(7);
        surface.ShouldHaveCursor(new Point(3, 1), visible: true);
    }

    /// <summary>Verifies both affixes reserve their own cell column pinned flush to the border,
    /// inboard of it and outboard of the caret/selection viewport.</summary>
    [Fact]
    public async Task Render_WhenTextInputHasBothAffixes_PinsThemInboardOfTheBorderAsync()
    {
        // Arrange
        var input = new TextInput
        {
            Text = "Hi",
            Width = Length.Cells(10),
            Height = Length.Cells(3),
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(10, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(1, 1)).Text.ShouldBe(">");
        surface.Cell(new Point(8, 1)).Text.ShouldBe("<");
        surface.Cell(new Point(3, 1)).Text.ShouldBe("H");
        surface.Cell(new Point(4, 1)).Text.ShouldBe("i");
    }

    /// <summary>Verifies affixes stay fixed at the border while the text viewport they were
    /// deflated away from scrolls underneath them - the deflation happens once in ArrangeChrome,
    /// before any scroll/viewport math runs, so the affix columns never travel with the caret.</summary>
    [Fact]
    public async Task Render_WhenCaretScrollsTheViewport_KeepsAffixesFixedAsync()
    {
        // Arrange - content wider than the deflated viewport, with scrollbars suppressed so the
        // single content row stays available regardless of overflow.
        var input = new TextInput
        {
            Text = "abcdefghij",
            Width = Length.Cells(10),
            Height = Length.Cells(3),
            ScrollBars = ScrollBars.None,
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(10, 3),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(1, 1)).Text.ShouldBe(">");
        surface.Cell(new Point(8, 1)).Text.ShouldBe("<");
        var firstVisibleCharBeforeFocus = surface.Cell(new Point(3, 1)).Text;

        // Act - focusing chases the caret, which Text already placed at the end, into view.
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert - the viewport actually scrolled, but the affix columns did not move.
        input.HorizontalOffset.ShouldBeGreaterThan(0);
        surface.Cell(new Point(3, 1)).Text.ShouldNotBe(firstVisibleCharBeforeFocus);
        surface.Cell(new Point(1, 1)).Text.ShouldBe(">");
        surface.Cell(new Point(8, 1)).Text.ShouldBe("<");
    }

    /// <summary>Verifies bracketed paste commits once and Shift selects both cells of a wide grapheme.</summary>
    [Fact]
    public async Task Keyboard_WhenPasteIsFollowedByShiftLeft_SelectsWideClusterAtomicallyAsync()
    {
        // Arrange
        var changes = 0;
        var input = new TextInput { Width = Length.Cells(4), Height = Length.Cells(1) };
        input.TextChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(4, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PasteAsync("A\u0301界");
        await surface.Keyboard.PressAsync(Code.Left, Modifiers.Shift);

        // Assert
        changes.ShouldBe(1);
        input.Text.ShouldBe("A\u0301界");
        (await surface.Application.Dispatcher.InvokeAsync(
            () => input.SelectedText,
            TestContext.Current.CancellationToken)).ShouldBe("界");
        input.SelectionStart.ShouldBe(2);
        input.SelectionLength.ShouldBe(1);
        (surface.Cell(new Point(1, 0)).Style.Attributes & TerminalAttributes.Reverse)
            .ShouldBe(TerminalAttributes.Reverse);
        (surface.Cell(new Point(2, 0)).Style.Attributes & TerminalAttributes.Reverse)
            .ShouldBe(TerminalAttributes.Reverse);
        surface.ShouldHaveCursor(new Point(1, 0), visible: true);
    }

    /// <summary>Verifies Enter submits single-line text and inserts LF only in multiline mode.</summary>
    [Fact]
    public async Task Keyboard_WhenEnterIsPressed_SubmitsOrInsertsAccordingToPolicyAsync()
    {
        // Arrange single-line editor
        string? submitted = null;
        var single = new TextInput
        {
            Text = "Go",
            Width = Length.Cells(4),
            Height = Length.Cells(1)
        };
        single.Submitted += (_, eventArgs) => submitted = eventArgs.Text;
        await using var singleSurface = await ComponentSurface.MountAsync(
            single,
            new Size(4, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await singleSurface.Keyboard.PressAsync(Code.Tab);

        // Act single-line editor
        await singleSurface.Keyboard.PressAsync(Code.Enter);

        // Assert single-line editor
        single.Text.ShouldBe("Go");
        submitted.ShouldBe("Go");

        // Arrange multiline editor
        var multi = new TextInput
        {
            AcceptsReturn = true,
            Width = Length.Cells(4),
            Height = Length.Cells(2)
        };
        await using var multiSurface = await ComponentSurface.MountAsync(
            multi,
            new Size(4, 2),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await multiSurface.Keyboard.PressAsync(Code.Tab);

        // Act multiline editor
        await multiSurface.Keyboard.PressAsync(Code.Enter);

        // Assert multiline editor
        multi.Text.ShouldBe("\n");
        multi.CaretIndex.ShouldBe(1);
        multiSurface.ShouldHaveCursor(new Point(0, 1), visible: true);
    }

    /// <summary>Verifies password source is absent from cells and disabled input refuses focus and mutation.</summary>
    [Fact]
    public async Task Render_WhenPasswordOrDisabled_PreservesSecurityAndAvailabilityPolicyAsync()
    {
        // Arrange password editor
        var password = new TextInput
        {
            Text = "Ae\u0301👩‍💻",
            PasswordCharacter = new Rune('*'),
            Width = Length.Cells(6),
            Height = Length.Cells(1)
        };
        await using var passwordSurface = await ComponentSurface.MountAsync(
            password,
            new Size(6, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        // Act and assert password editor
        await passwordSurface.Keyboard.PressAsync(Code.Tab);
        passwordSurface.ShouldRender("***");
        passwordSurface.ShouldHaveCursor(new Point(3, 0), visible: true);

        // Arrange disabled editor
        var disabled = new TextInput
        {
            Text = "Safe",
            IsEnabled = false,
            Width = Length.Cells(4),
            Height = Length.Cells(1)
        };
        await using var disabledSurface = await ComponentSurface.MountAsync(
            disabled,
            new Size(4, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        // Act disabled editor
        await disabledSurface.Keyboard.PressAsync(Code.Tab);
        await disabledSurface.Keyboard.TypeAsync("X");

        // Assert disabled editor
        disabled.Text.ShouldBe("Safe");
        disabled.IsFocused.ShouldBeFalse();
        disabledSurface.ShouldHaveState(disabled, VisualState.Disabled);
        disabledSurface.ShouldHaveCursor(default, visible: false);

        // Act re-enable and resume interaction
        await disabledSurface.UpdateAsync(() => disabled.IsEnabled = true, "re-enable TextInput");
        disabledSurface.ShouldHaveState(disabled, VisualState.Normal);
        await disabledSurface.Keyboard.PressAsync(Code.Tab);
        await disabledSurface.Keyboard.TypeAsync("X");

        // Assert normal interaction resumes
        disabled.Text.ShouldBe("SafeX");
        disabled.IsFocused.ShouldBeTrue();
    }

    /// <summary>Verifies disabling mid-drag clears pointer capture and focus and shows the disabled
    /// state, instead of only proving the ctor-disabled case.</summary>
    [Fact]
    public async Task Input_WhenDisabledDuringSelectionDrag_ClearsCaptureAndFocusAsync()
    {
        // Arrange
        var input = new TextInput
        {
            Text = "A界éZ",
            Width = Length.Cells(8),
            Height = Length.Cells(1)
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(8, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        // Act begin a selection drag and hold it
        await surface.Pointer.MoveToAsync(input, default);
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(input);
        surface.ShouldHaveFocus(input);

        // Act disable while the drag is held
        await surface.UpdateAsync(() => input.IsEnabled = false, "disable TextInput during drag");

        // Assert capture and focus are cleared and the disabled state shows
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
        input.IsFocused.ShouldBeFalse();
        surface.ShouldHaveState(input, VisualState.Disabled);
    }

    /// <summary>Verifies a TextInput inherits disabled state from an ancestor and keeps stable
    /// geometry across a genuine resize while disabled, matching an independently-mounted enabled
    /// instance arranged at the same size.</summary>
    [Fact]
    public async Task Input_WhenAncestorDisablesTextInputAndResized_InheritsStateAndPreservesGeometryAsync()
    {
        // Arrange a TextInput disabled only through its ancestor
        var input = new TextInput
        {
            Text = "Safe",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var overlay = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { input }
        };
        await using var surface = await ComponentSurface.MountAsync(
            overlay,
            new Size(8, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        // Act disable the ancestor, not the TextInput itself
        await surface.UpdateAsync(() => overlay.IsEnabled = false, "disable TextInput's ancestor");

        // Assert the disabled state is inherited
        input.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(input, VisualState.Disabled);

        // Act resize to a genuinely different size while disabled
        await surface.ResizeAsync(new Size(16, 3));

        // Assert geometry matches an independently-mounted enabled instance at the same size
        var reference = new TextInput
        {
            Text = "Safe",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var referenceSurface = await ComponentSurface.MountAsync(
            reference,
            new Size(16, 3),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        input.Bounds.ShouldBe(reference.Bounds);
        input.DesiredSize.ShouldBe(reference.DesiredSize);
    }

    /// <summary>Verifies read-only input remains focusable and selectable without accepting mutations.</summary>
    [Fact]
    public async Task Keyboard_WhenReadOnly_AllowsNavigationButRejectsEveryMutationAsync()
    {
        // Arrange
        var input = new TextInput
        {
            Text = "Read",
            IsReadOnly = true,
            Width = Length.Cells(4),
            Height = Length.Cells(1)
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(4, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Left);
        await surface.Keyboard.PressAsync(Code.Backspace);
        await surface.Keyboard.PressAsync(Code.Delete);
        await surface.Keyboard.TypeAsync("X");
        await surface.Keyboard.PasteAsync("Y");

        // Assert
        input.Text.ShouldBe("Read");
        input.CaretIndex.ShouldBe(3);
        input.IsFocused.ShouldBeTrue();
        input.HorizontalOffset.ShouldBe(1);
        surface.ShouldRender("ead");
        surface.ShouldHaveCursor(new Point(2, 0), visible: true);
    }

    /// <summary>Verifies a real cell drag selects complete graphemes including both cells of a wide rune.</summary>
    [Fact]
    public async Task Pointer_WhenUnicodeTextIsDragged_SelectsOnlyCompleteRenderedClustersAsync()
    {
        // Arrange
        var input = new TextInput
        {
            Text = "A界e\u0301Z",
            Width = Length.Cells(8),
            Height = Length.Cells(1)
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(8, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.DragAsync(input, default, new Point(4, 0));

        // Assert
        input.SelectionStart.ShouldBe(0);
        input.SelectionLength.ShouldBe(4);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => input.SelectedText,
            TestContext.Current.CancellationToken)).ShouldBe("A界e\u0301");
        surface.ShouldHaveState(input, VisualState.IsPointerOver | VisualState.Focused);
        (surface.Cell(default).Style.Attributes & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.Reverse);
        (surface.Cell(new Point(1, 0)).Style.Attributes & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.Reverse);
        (surface.Cell(new Point(2, 0)).Style.Attributes & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.Reverse);
        (surface.Cell(new Point(3, 0)).Style.Attributes & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.Reverse);
        surface.ShouldHaveCursor(new Point(4, 0), visible: true);
    }

    /// <summary>Verifies clicking either cell of a wide grapheme maps to the nearer caret boundary,
    /// not always the leading edge.</summary>
    [Fact]
    public async Task Pointer_WhenWideClusterIsClicked_MapsEachCellToItsNearerBoundaryAsync()
    {
        // Arrange
        var input = new TextInput
        {
            Text = "A界B",
            Width = Length.Cells(8),
            Height = Length.Cells(1)
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(8, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        // Act and assert - leading cell of 界 (cell 1) caret before it
        await surface.Pointer.ClickAsync(input, new Point(1, 0));
        input.SelectionStart.ShouldBe(1);
        input.SelectionLength.ShouldBe(0);

        // Act and assert - trailing cell of 界 (cell 2) caret after it
        await surface.Pointer.ClickAsync(input, new Point(2, 0));
        input.SelectionStart.ShouldBe(2);
        input.SelectionLength.ShouldBe(0);
    }

    /// <summary>Verifies horizontal and vertical wheel reports scroll the rendered editor viewport.</summary>
    [Fact]
    public async Task Pointer_WhenOverflowingEditorIsWheeled_UpdatesOffsetsAndVisibleCellsAsync()
    {
        // Arrange
        var input = new TextInput
        {
            AcceptsReturn = true,
            Text = "abcdef\none\ntwo\nthree",
            CaretIndex = 0,
            Width = Length.Cells(4),
            Height = Length.Cells(2)
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(4, 2),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.WheelAsync(input, default, wheelY: -1);
        await surface.Pointer.WheelAsync(input, default, wheelX: 1);

        // Assert
        input.HorizontalOffset.ShouldBe(1);
        input.VerticalOffset.ShouldBe(1);
        surface.ShouldRender("""
                             ne ▓
                             ◀▓▶
                             """);
        surface.ShouldHaveState(input, VisualState.IsPointerOver);
        surface.ShouldHaveCursor(default, visible: false);
    }

    /// <summary>Verifies scrolling rightward through wide-cluster content never lands the viewport
    /// mid-cluster: the leftward branch of the offset arithmetic always assigns the caret's own
    /// cell x (inherently a cluster start), but the rightward branch's viewport-relative arithmetic
    /// (caret - viewport + 1) could land inside a two-cell cluster, blanking both edge columns since
    /// the canvas drops a half-covered cluster with no substitute glyph.</summary>
    [Fact]
    public async Task Keyboard_WhenWideClusterTextScrollsRight_NeverBlanksAColumnAsync()
    {
        // Arrange
        var input = new TextInput
        {
            Text = "一二三四五六",
            Width = Length.Cells(4),
            Height = Length.Cells(1),
            ScrollBars = ScrollBars.None
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(4, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Home);

        // Act and assert
        for (var step = 0; step < 6; step++)
        {
            await surface.Keyboard.PressAsync(Code.Right);

            for (var x = 0; x < 4; x++)
            {
                var cell = surface.Cell(new Point(x, 0));
                (cell.Text != " " || cell.Width != 1)
                    .ShouldBeTrue($"column {x} was blank after rightward step {step + 1}");
            }

            (input.HorizontalOffset % 2).ShouldBe(0, $"offset was misaligned after rightward step {step + 1}");
        }

        input.HorizontalOffset.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies the cached-boundary rewrite of the cluster-start snap still snaps
    /// correctly on a row other than the first, since the cache-backed lookup scopes its scan to the
    /// target row via the cached row array rather than always starting from the document's first
    /// character.</summary>
    [Fact]
    public async Task Keyboard_WhenWideClusterTextOnASecondRowScrollsRight_NeverBlanksAColumnAsync()
    {
        // Arrange
        var input = new TextInput
        {
            AcceptsReturn = true,
            Text = "first\n一二三四五六",
            Width = Length.Cells(4),
            Height = Length.Cells(2),
            ScrollBars = ScrollBars.None
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(4, 2),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Home);

        // Act and assert
        for (var step = 0; step < 6; step++)
        {
            await surface.Keyboard.PressAsync(Code.Right);

            for (var x = 0; x < 4; x++)
            {
                var cell = surface.Cell(new Point(x, 1));
                (cell.Text != " " || cell.Width != 1)
                    .ShouldBeTrue($"column {x} was blank after rightward step {step + 1}");
            }

            (input.HorizontalOffset % 2).ShouldBe(0, $"offset was misaligned after rightward step {step + 1}");
        }

        input.HorizontalOffset.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies resize clamps automatic editor offsets and exposes complete content.</summary>
    [Fact]
    public async Task ResizeAsync_WhenEditorViewportGrows_ClampsOffsetsAndRepositionsCursorAsync()
    {
        // Arrange
        var input = new TextInput
        {
            AcceptsReturn = true,
            Text = "123456\nabcdef\nXYZ",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(3, 2),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        input.HorizontalOffset.ShouldBe(2);
        input.VerticalOffset.ShouldBe(2);
        surface.ShouldHaveCursor(new Point(1, 0), visible: true);

        // Act
        await surface.ResizeAsync(new Size(10, 5));

        // Assert
        input.HorizontalOffset.ShouldBe(0);
        input.VerticalOffset.ShouldBe(0);
        surface.ShouldRender("""
                             123456
                             abcdef
                             XYZ


                             """);
        surface.ShouldHaveCursor(new Point(3, 2), visible: true);
    }

    /// <summary>Verifies an auto-sized editor (no explicit Width) reserves a cell for the
    /// end-of-text caret so arrange-time caret-reveal never scrolls the leading character out of
    /// view — without the reservation, a viewport that exactly matches the content width scrolls
    /// column 0 away the moment the caret sits at the end of the text.</summary>
    [Fact]
    public async Task Render_WhenAutoSizedEditorHasShortText_KeepsLeadingCharacterInViewAsync()
    {
        // Arrange
        var input = new TextInput { Text = "1.0" };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(6, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        // Assert
        input.HorizontalOffset.ShouldBe(0);
        surface.ShouldRender("1.0");
    }

    /// <summary>Verifies a single-character auto-sized editor renders that character instead of a
    /// blank cell, the most visible symptom of an unreserved caret cell.</summary>
    [Fact]
    public async Task Render_WhenAutoSizedEditorHasOneCharacter_RendersItInsteadOfBlankAsync()
    {
        // Arrange
        var input = new TextInput { Text = "X" };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(4, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        // Assert
        input.HorizontalOffset.ShouldBe(0);
        surface.ShouldRender("X");
    }

    /// <summary>Verifies a never-focused editor with an explicit narrow Width renders its
    /// leading character instead of scrolling to reveal a caret nobody has seen, mirroring
    /// TableSurfaceTests's auto-column coverage of the same rule for auto-sized editors: the
    /// caret-reveal chase never runs while unfocused, so content naturally starts at the first
    /// character exactly as it was assigned.</summary>
    [Fact]
    public async Task Render_WhenNeverFocusedNarrowWidthHoldsLongerText_RendersLeadingCharacterAsync()
    {
        // Arrange
        var input = new TextInput
        {
            Text = "abcdefghij",
            Width = Length.Cells(4),
            Height = Length.Cells(1),
            ScrollBars = ScrollBars.None
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(4, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        // Assert
        input.IsFocused.ShouldBeFalse();
        input.HorizontalOffset.ShouldBe(0);
        surface.ShouldRender("abcd");
    }

    /// <summary>Verifies pointer hover recolors every physical edge of TextInput's flat border,
    /// not just the logical resolved value.</summary>
    [Fact]
    public async Task Pointer_WhenMovedOverTextInput_RecolorsEveryBorderEdgeAsync()
    {
        // Arrange
        var input = new TextInput
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3)
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(10, 5),
            TestContext.Current.CancellationToken);
        var theme = input.Theme.ShouldNotBeNull();
        AssertFlatEdges(input, ThemeColorHelper.Border(theme));

        // Act
        await surface.Pointer.MoveToAsync(input);

        // Assert - every edge, not just the two bezel corners, turns the same authored flat color.
        surface.ShouldHaveState(input, VisualState.IsPointerOver);
        AssertFlatEdges(input, ThemeColorHelper.HoveredBorder(theme));

        // Act - move away: TextInput's InputStyle relief default is Flat (IntrinsicBorderSurfaceTests.cs:58
        // is the suite's sole surviving non-Flat specimen), so the hover cue is authored per-state
        // feedback, not a fixed accident of relief substitution.
        await surface.Pointer.MoveToAsync(new Point(20, 20));

        // Assert - the normal flat border returns once the authored color no longer applies.
        surface.ShouldHaveState(input, VisualState.Normal);
        AssertFlatEdges(input, ThemeColorHelper.Border(theme));
    }

    /// <summary>Verifies keyboard focus recolors every physical edge of TextInput's border, not just
    /// the logical resolved value - the focus-cue sibling of
    /// <see cref="Pointer_WhenMovedOverTextInput_RecolorsEveryBorderEdgeAsync"/>, exercising
    /// "input.focused.border.foreground" instead of "input.pointerOver.border.foreground".</summary>
    [Fact]
    public async Task Keyboard_WhenTextInputReceivesFocus_RecolorsEveryBorderEdgeAsync()
    {
        // Arrange
        var input = new TextInput
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3)
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(10, 5),
            TestContext.Current.CancellationToken);
        var theme = input.Theme.ShouldNotBeNull();
        AssertFlatEdges(input, ThemeColorHelper.Border(theme));

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert - every edge, not just the two bezel corners, turns the same authored flat color.
        input.IsFocused.ShouldBeTrue();
        surface.ShouldHaveState(input, VisualState.Focused);
        AssertFlatEdges(input, ThemeColorHelper.FocusedBorder(theme));
    }

    private static void AssertFlatEdges(TextInput input, Color expected)
    {
        var styles = input.ResolveBorderStyles(input.GetAppearanceState());
        styles.Top.Foreground.ShouldBe(expected);
        styles.Right.Foreground.ShouldBe(expected);
        styles.Bottom.Foreground.ShouldBe(expected);
        styles.Left.Foreground.ShouldBe(expected);
    }
}
