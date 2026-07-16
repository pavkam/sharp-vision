// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies TextInput editing, policy, cursor, and cells through a mounted surface.</summary>
public sealed class TextInputSurfaceTests
{
    /// <summary>Verifies placeholder, focus, raw Unicode typing, wide cells, and committed cursor.</summary>
    [Fact]
    public async Task Keyboard_WhenUnicodeTextIsTyped_ReplacesPlaceholderAndCommitsCursorAsync()
    {
        // Arrange
        var input = new TextInput
        {
            Placeholder = "Name",
            Width = Length.Cells(6),
            Height = Length.Cells(1),
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(6, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("Name");
        (surface.Cell(default).Style.Attributes & Attributes.Dim).ShouldBe(Attributes.Dim);
        surface.ShouldHaveCursor(default, visible: false);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("A\u0301界");

        // Assert
        input.Text.ShouldBe("A\u0301界");
        input.CaretIndex.ShouldBe(3);
        surface.ShouldHaveState(input, State.Focused);
        surface.ShouldRender("A\u0301界");
        surface.Cell(default).Text.ShouldBe("A\u0301");
        surface.Cell(new Point(1, 0)).Text.ShouldBe("界");
        surface.Cell(new Point(2, 0)).IsContinuation.ShouldBeTrue();
        surface.ShouldHaveCursor(new Point(3, 0), visible: true);
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
            Height = Length.Cells(1),
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(6, 1),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Left);
        await surface.Keyboard.PressAsync(Code.Backspace);
        await surface.Keyboard.PressAsync(Code.Delete);

        // Assert
        input.Text.ShouldBe("A\u0301");
        input.CaretIndex.ShouldBe(2);
        surface.ShouldRender("A\u0301");
        surface.Cell(default).Text.ShouldBe("A\u0301");
        surface.ShouldHaveCursor(new Point(1, 0), visible: true);
    }

    /// <summary>Verifies bracketed paste commits once and Shift selects both cells of a wide grapheme.</summary>
    [Fact]
    public async Task Keyboard_WhenPasteIsFollowedByShiftLeft_SelectsWideClusterAtomicallyAsync()
    {
        // Arrange
        var changes = 0;
        var input = new TextInput
        {
            Width = Length.Cells(4),
            Height = Length.Cells(1),
        };
        input.TextChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(4, 1),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PasteAsync("A\u0301界");
        await surface.Keyboard.PressAsync(Code.Left, Modifiers.Shift);

        // Assert
        changes.ShouldBe(1);
        input.Text.ShouldBe("A\u0301界");
        input.SelectedText.ShouldBe("界");
        input.SelectionStart.ShouldBe(2);
        input.SelectionLength.ShouldBe(1);
        (surface.Cell(new Point(1, 0)).Style.Attributes & Attributes.Reverse)
            .ShouldBe(Attributes.Reverse);
        (surface.Cell(new Point(2, 0)).Style.Attributes & Attributes.Reverse)
            .ShouldBe(Attributes.Reverse);
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
            Height = Length.Cells(1),
        };
        single.Submitted += (_, eventArgs) => submitted = eventArgs.Text;
        await using var singleSurface = await ComponentSurface.MountAsync(
            single,
            new Size(4, 1),
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
            Height = Length.Cells(2),
        };
        await using var multiSurface = await ComponentSurface.MountAsync(
            multi,
            new Size(4, 2),
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
            Height = Length.Cells(1),
        };
        await using var passwordSurface = await ComponentSurface.MountAsync(
            password,
            new Size(6, 1),
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
            Height = Length.Cells(1),
        };
        await using var disabledSurface = await ComponentSurface.MountAsync(
            disabled,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        // Act disabled editor
        await disabledSurface.Keyboard.PressAsync(Code.Tab);
        await disabledSurface.Keyboard.TypeAsync("X");

        // Assert disabled editor
        disabled.Text.ShouldBe("Safe");
        disabled.IsFocused.ShouldBeFalse();
        disabledSurface.ShouldHaveState(disabled, State.Disabled);
        disabledSurface.ShouldHaveCursor(default, visible: false);
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
            Height = Length.Cells(1),
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(4, 1),
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
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(3, 2),
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
}
