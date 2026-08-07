// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Verifies reusable mounted-surface mutation, state, and keyboard contracts.</summary>
public sealed class ComponentSurfaceTests
{
    /// <summary>Verifies dispatcher-affine mutation settles layout, rendering, and terminal output.</summary>
    [Fact]
    public async Task UpdateAsync_WhenMountedControlChanges_SettlesTheRenderedMutationAsync()
    {
        // Arrange
        var text = new ControlText("Before")
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(6, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("Before");

        // Act
        await surface.UpdateAsync(() => text.Content = "After", "change Text content");

        // Assert
        surface.ShouldRender("After");
    }

    /// <summary>Verifies state assertions accept descendants owned by the mounted component.</summary>
    [Fact]
    public async Task ShouldHaveState_WhenOwnedDescendantIsPassed_ObservesItsStateAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Choice" };
        var stack = new Stack { Children = { checkBox } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(10, 1),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        checkBox.Focused.ShouldBeTrue();
        surface.ShouldHaveState(checkBox, VisualState.Focused);
    }

    /// <summary>Verifies a complete Kitty character action drives PressableBase press and release behavior.</summary>
    [Fact]
    public async Task Keyboard_WhenSpaceCompletes_EmitsPressAndReleaseAsync()
    {
        // Arrange
        ActivationCause? cause = null;
        var checkBox = new CheckBox { Text = "Choice" };
        checkBox.Checked += (_, eventArgs) => cause = eventArgs.Cause;
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(10, 1),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));

        // Assert
        checkBox.IsChecked.ShouldBe(true);
        cause.ShouldBe(ActivationCause.Keyboard);
        surface.ShouldHaveState(checkBox, VisualState.Focused);
    }

    /// <summary>Verifies distinct Kitty actions expose held and released keyboard state.</summary>
    [Fact]
    public async Task Keyboard_WhenSpacePressAndReleaseAreSeparate_ExposesBothStatesAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Choice" };
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(10, 1),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        // Hold-then-release semantics require a terminal that reports releases.
        await surface.UpdateAsync(
            () => checkBox.SetCapabilities(TestCapabilities.WithKeyReleases),
            "declare key-release reporting");

        // Act and assert held state
        await surface.Keyboard.PressCharacterAsync(new Rune(' '));
        checkBox.IsChecked.ShouldBe(false);
        surface.ShouldHaveState(checkBox, VisualState.Focused | VisualState.Pressed);

        // Act and assert released state
        await surface.Keyboard.ReleaseCharacterAsync(new Rune(' '));
        checkBox.IsChecked.ShouldBe(true);
        surface.ShouldHaveState(checkBox, VisualState.Focused);
    }

    /// <summary>Verifies terminal resize commits new geometry before the settled frame is exposed.</summary>
    [Fact]
    public async Task ResizeAsync_WhenSurfaceChanges_ReflowsMountedTextAsync()
    {
        // Arrange
        var text = new ControlText("abcd")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Overflow = Overflow.WrapAnywhere
        };
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(2, 2),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
                             ab
                             cd
                             """);

        // Act
        await surface.ResizeAsync(new Size(4, 1));

        // Assert
        text.Bounds.ShouldBe(new Rect(0, 0, 4, 1));
        surface.ShouldRender("abcd");
    }

    /// <summary>Verifies raw text, navigation, deletion, paste, and cursor state use the terminal path.</summary>
    [Fact]
    public async Task Keyboard_WhenEditorReceivesTextAndPaste_CommitsGraphemesAndCursorAsync()
    {
        // Arrange
        var input = new TextInput { Width = Length.Cells(4), Height = Length.Cells(1) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(4, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        surface.ShouldHaveCursor(default, visible: false);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.TypeAsync("A\u0301界");
        await surface.Keyboard.PressAsync(Code.Left);
        await surface.Keyboard.PressAsync(Code.Backspace);
        await surface.Keyboard.PasteAsync("e\u0301");

        // Assert
        input.Text.ShouldBe("e\u0301界");
        input.CaretIndex.ShouldBe(2);
        surface.ShouldRender("e\u0301界");
        surface.ShouldHaveCursor(new Point(1, 0), visible: true);
    }

    /// <summary>Verifies a Shift-modified navigation sequence extends selection through decoding.</summary>
    [Fact]
    public async Task Keyboard_WhenShiftLeftIsPressed_SelectsOneCompleteWideGraphemeAsync()
    {
        // Arrange
        var input = new TextInput
        {
            Text = "A界",
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
        await surface.Keyboard.PressAsync(Code.Left, Modifiers.Shift);

        // Assert
        input.SelectionStart.ShouldBe(1);
        input.SelectionLength.ShouldBe(1);
        input.SelectedText.ShouldBe("界");
        surface.ShouldHaveCursor(new Point(1, 0), visible: true);
    }

    /// <summary>Verifies relative click, wheel, and captured drag actions traverse real pointer decoding.</summary>
    [Fact]
    public async Task Pointer_WhenRelativeActionsDriveScrollBar_CommitsCausesAndReleasesDragAsync()
    {
        // Arrange
        var causes = new List<ScrollCause>();
        var bar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Style = ScrollBarStyle.FullBlock,
            Maximum = 100,
            Width = Length.Cells(12),
            Height = Length.Cells(1)
        };
        bar.ValueChanged += (_, eventArgs) => causes.Add(eventArgs.Cause);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(12, 1),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(bar, new Point(11, 0));
        bar.Value.ShouldBe(1);
        await surface.Pointer.WheelAsync(bar, new Point(6, 0), wheelX: 1);
        bar.Value.ShouldBe(2);
        await surface.Pointer.DragAsync(bar, new Point(1, 0), new Point(10, 0));

        // Assert
        bar.Value.ShouldBe(100);
        bar.Pressed.ShouldBeFalse();
        causes.ShouldBe([ScrollCause.Pointer, ScrollCause.Wheel, ScrollCause.Pointer]);
        surface.ShouldHaveState(bar, VisualState.Focused | VisualState.PointerOver);
    }

    /// <summary>Verifies invalid relative pointer requests fail before any terminal action is emitted.</summary>
    [Fact]
    public async Task Pointer_WhenRelativeActionIsInvalid_RejectsTheRequestBeforeInputAsync()
    {
        // Arrange
        var bar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Width = Length.Cells(4),
            Height = Length.Cells(1)
        };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        // Act and assert
        _ = await Should.ThrowAsync<ArgumentException>(() => surface.Pointer.MoveToAsync(new Button(), default));
        _ = await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            surface.Pointer.MoveToAsync(bar, new Point(-1, 0)));
        _ = await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            surface.Pointer.MoveToAsync(bar, new Point(4, 0)));
        _ = await Should.ThrowAsync<ArgumentException>(() => surface.Pointer.WheelAsync(bar, default));
        _ = await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            surface.Pointer.WheelAsync(bar, default, wheelY: 2));
        _ = await Should.ThrowAsync<InvalidOperationException>(() => surface.Pointer.MovePressedToAsync(bar, default));

        bar.Value.ShouldBe(0);
        surface.ShouldHaveState(bar, VisualState.Normal);
    }

    /// <summary>Verifies input that changes no control state still crosses the routed dispatcher fence.</summary>
    [Fact]
    public async Task Pointer_WhenDisabledControlIgnoresRelease_SettlesWithoutInvalidationAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Disabled", Enabled = false };
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(12, 1),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(checkBox);

        // Assert
        checkBox.IsChecked.ShouldBe(false);
        surface.ShouldHaveState(checkBox, VisualState.Disabled);
        surface.ShouldRender("[ ] Disabled");
    }

    /// <summary>Verifies one decoded Shift+Tab moves backward through the mounted tree.</summary>
    [Fact]
    public async Task Keyboard_WhenShiftTabIsPressed_MovesFocusBackwardThroughMountedRootAsync()
    {
        // Arrange
        var first = new Button { Text = "First" };
        var second = new Button { Text = "Second" };
        var root = new Stack { Children = { first, second } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(16, 4),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);

        // Assert
        surface.ShouldHaveFocus(first);
    }

    /// <summary>Verifies terminal leave clears hover, press, and capture without clearing logical focus.</summary>
    [Fact]
    public async Task Pointer_WhenTerminalLeaveArrives_ClearsHoverHeldStateAndCaptureAsync()
    {
        // Arrange
        var button = new Button { Text = "Leave" };
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();

        // Act
        await surface.Pointer.LeaveAsync();

        // Assert
        surface.ShouldHaveState(button, VisualState.Focused);
        surface.ShouldHaveFocus(button);
        surface.ShouldHaveCapture(null);
    }
}
