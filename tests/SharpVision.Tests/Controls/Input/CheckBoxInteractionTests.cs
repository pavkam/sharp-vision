// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies every CheckBox keyboard, pointer, access-key, mark-style, and lifecycle interaction
/// through a mounted terminal surface, complementing the appearance-oriented CheckBoxSurfaceTests.</summary>
public sealed class CheckBoxInteractionTests
{
    /// <summary>Verifies Enter advances a three-state CheckBox immediately through every state with the
    /// keyboard cause, raising the state-specific event before StateChanged with exact arguments and
    /// rendering each mark.</summary>
    [Fact]
    public async Task Keyboard_WhenEnterCyclesThreeStates_RaisesOrderedEventsAndRendersEachMarkAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Option", ThreeState = true };
        var events = Record(checkBox);
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(12, 1),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act and assert first transition
        await surface.Keyboard.PressAsync(Code.Enter);
        checkBox.IsChecked.ShouldBe(true);
        surface.ShouldRender("[✓] Option");
        events.ShouldBe(["Checked:False>True:Keyboard", "StateChanged:False>True:Keyboard"]);

        // Act and assert second transition
        events.Clear();
        await surface.Keyboard.PressAsync(Code.Enter);
        checkBox.IsChecked.ShouldBeNull();
        surface.ShouldRender("[─] Option");
        events.ShouldBe(["Indeterminate:True>null:Keyboard", "StateChanged:True>null:Keyboard"]);

        // Act and assert third transition
        events.Clear();
        await surface.Keyboard.PressAsync(Code.Enter);
        checkBox.IsChecked.ShouldBe(false);
        surface.ShouldRender("[ ] Option");
        events.ShouldBe(["Unchecked:null>False:Keyboard", "StateChanged:null>False:Keyboard"]);
    }

    /// <summary>Verifies a Space hold with key-release reporting shows the pressed state without
    /// toggling, ignores repeat, toggles once on release, and a Tab mid-hold cancels the toggle.</summary>
    [Fact]
    public async Task Keyboard_WhenSpaceIsHeldWithReleaseReporting_TogglesOnlyOnCompletedReleaseAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Option" };
        var button = new Button("Next") { Width = Length.Cells(8), Height = Length.Cells(3) };
        var events = Record(checkBox);
        var stack = new Stack { Orientation = Orientation.Vertical, Children = { checkBox, button } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 6),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(checkBox);
        await surface.UpdateAsync(
            () => checkBox.SetCapabilities(TestCapabilities.WithKeyReleases),
            "declare key-release reporting");

        // Act hold, repeat, release
        await surface.Keyboard.PressCharacterAsync(new Rune(' '));
        surface.ShouldHaveState(checkBox, VisualState.Focused | VisualState.Pressed);
        checkBox.IsChecked.ShouldBe(false);
        await surface.SendAsync("\u001b[32;1:2u"u8.ToArray(), "repeat Space");
        surface.ShouldHaveState(checkBox, VisualState.Focused | VisualState.Pressed);
        checkBox.IsChecked.ShouldBe(false);
        await surface.Keyboard.ReleaseCharacterAsync(new Rune(' '));

        // Assert one toggle
        checkBox.IsChecked.ShouldBe(true);
        events.ShouldBe(["Checked:False>True:Keyboard", "StateChanged:False>True:Keyboard"]);
        surface.ShouldHaveState(checkBox, VisualState.Focused);

        // Act hold then Tab away
        events.Clear();
        await surface.Keyboard.PressCharacterAsync(new Rune(' '));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.ReleaseCharacterAsync(new Rune(' '));

        // Assert cancelled without toggling either control
        surface.ShouldHaveFocus(button);
        checkBox.IsChecked.ShouldBe(true);
        checkBox.IsPressed.ShouldBeFalse();
        button.IsPressed.ShouldBeFalse();
        events.ShouldBeEmpty();
    }

    /// <summary>Verifies an activation key carrying an application-command modifier never toggles, while
    /// Shift still does.</summary>
    /// <param name="sequence">The Kitty keyboard sequence for the modified key press.</param>
    /// <param name="expected">The checked value after the press.</param>
    [Theory]
    [InlineData("\u001b[13;5u", false)] // Ctrl+Enter
    [InlineData("\u001b[32;5u", false)] // Ctrl+Space
    [InlineData("\u001b[13;3u", false)] // Alt+Enter
    [InlineData("\u001b[13;2u", true)] // Shift+Enter
    [InlineData("\u001b[32;2u", true)] // Shift+Space
    public async Task Keyboard_WhenActivationKeyCarriesModifier_TogglesOnlyForEligibleChordAsync(
        string sequence,
        bool expected)
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Option" };
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(12, 1),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.SendAsync(Encoding.ASCII.GetBytes(sequence), "press modified activation key");

        // Assert
        checkBox.IsChecked.ShouldBe(expected);
        checkBox.IsPressed.ShouldBeFalse();
    }

    /// <summary>Verifies a held pointer that leaves and releases outside never toggles, while a re-entry
    /// followed by an inside release toggles once, and a three-state pointer cycle reports pointer
    /// causes in committed order.</summary>
    [Fact]
    public async Task Pointer_WhenHeldPointerLeavesOrCyclesStates_TogglesOnlyOnInsideReleaseAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Option", ThreeState = true };
        var events = Record(checkBox);
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(14, 3),
            TestContext.Current.CancellationToken);
        var outside = new Point(13, 2);

        // Act press, drag out, release out
        await surface.Pointer.MoveToAsync(checkBox);
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(checkBox);
        await surface.Pointer.MovePressedToAsync(outside);
        checkBox.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(checkBox);
        await surface.Pointer.ReleaseAsync();

        // Assert untouched
        checkBox.IsChecked.ShouldBe(false);
        events.ShouldBeEmpty();
        surface.ShouldHaveCapture(null);

        // Act press, drag out, drag back, release in
        await surface.Pointer.MoveToAsync(checkBox);
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(outside);
        await surface.Pointer.MovePressedToAsync(await surface.ResolvePointAsync(checkBox));
        checkBox.IsPressed.ShouldBeTrue();
        await surface.Pointer.ReleaseAsync();

        // Assert one toggle
        checkBox.IsChecked.ShouldBe(true);
        events.ShouldBe(["Checked:False>True:Pointer", "StateChanged:False>True:Pointer"]);

        // Act complete the three-state cycle by clicking
        events.Clear();
        await surface.Pointer.ClickAsync(checkBox);
        await surface.Pointer.ClickAsync(checkBox);

        // Assert
        checkBox.IsChecked.ShouldBe(false);
        events.ShouldBe([
            "Indeterminate:True>null:Pointer",
            "StateChanged:True>null:Pointer",
            "Unchecked:null>False:Pointer",
            "StateChanged:null>False:Pointer"
        ]);
    }

    /// <summary>Verifies every mark family renders its unchecked, checked, and indeterminate glyphs and
    /// keeps the caption at the family's documented offset, and that clicking the mark cell toggles.</summary>
    /// <param name="markStyle">The mark family under test.</param>
    /// <param name="unchecked">The rendered unchecked row.</param>
    /// <param name="checked">The rendered checked row.</param>
    /// <param name="indeterminate">The rendered indeterminate row.</param>
    [Theory]
    [InlineData(CheckBoxMarkStyle.Brackets, "[ ] Go", "[✓] Go", "[─] Go")]
    [InlineData(CheckBoxMarkStyle.Tick, "○ Go", "✓ Go", "− Go")]
    [InlineData(CheckBoxMarkStyle.Square, "☐ Go", "☑ Go", "◩ Go")]
    public async Task Pointer_WhenMarkStyleVaries_RendersEachStateAndTogglesFromMarkCellAsync(
        CheckBoxMarkStyle markStyle,
        string @unchecked,
        string @checked,
        string indeterminate)
    {
        // Arrange
        var checkBox = new CheckBox
        {
            Text = "Go",
            ThreeState = true,
            Style = markStyle switch
            {
                CheckBoxMarkStyle.Brackets => CheckBoxStyle.Brackets,
                CheckBoxMarkStyle.Tick => CheckBoxStyle.Tick,
                CheckBoxMarkStyle.Square => CheckBoxStyle.Square,
                _ => throw new ArgumentOutOfRangeException(nameof(markStyle))
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(8, 1),
            TestContext.Current.CancellationToken);

        // Assert initial
        surface.ShouldRender(@unchecked);
        checkBox.ActualStyle.MarkWidth.ShouldBe(markStyle == CheckBoxMarkStyle.Brackets ? 3 : 1);

        // Act and assert clicks on the mark cell
        await surface.Pointer.ClickAsync(checkBox, new Point(0, 0));
        surface.ShouldRender(@checked);
        await surface.Pointer.ClickAsync(checkBox, new Point(0, 0));
        surface.ShouldRender(indeterminate);
        await surface.Pointer.ClickAsync(checkBox, new Point(0, 0));
        surface.ShouldRender(@unchecked);
    }

    /// <summary>Verifies a wrapped multi-line caption is owned by the CheckBox: a click on the second
    /// caption row toggles it.</summary>
    [Fact]
    public async Task Pointer_WhenCaptionWrapsToSecondRow_ClickOnSecondRowTogglesAsync()
    {
        // Arrange
        var checkBox = new CheckBox
        {
            Text = "Include hidden files",
            Width = Length.Cells(12),
            Height = Length.Cells(2)
        };
        checkBox.TextControl.ShouldNotBeNull().Overflow = Overflow.Wrap;
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Assert wrapped layout
        surface.Cell(new Point(4, 0)).Text.ShouldBe("I");
        surface.Cell(new Point(4, 1)).Text.ShouldBe("h");
        surface.Cell(new Point(0, 1)).Text.ShouldBe(" ");

        // Act
        await surface.Pointer.ClickAsync(checkBox, new Point(6, 1));

        // Assert
        checkBox.IsChecked.ShouldBe(true);
        surface.ShouldHaveState(checkBox, VisualState.IsPointerOver | VisualState.Focused);
    }

    /// <summary>Verifies a click on the continuation cell of a wide caption grapheme toggles.</summary>
    [Fact]
    public async Task Pointer_WhenCaptionIsWide_ClickOnContinuationCellTogglesAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "界面" };
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(10, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("[ ] 界面");
        surface.Cell(new Point(5, 0)).Continuation.ShouldBeTrue();

        // Act
        await surface.Pointer.ClickAsync(checkBox, new Point(5, 0));

        // Assert
        checkBox.IsChecked.ShouldBe(true);
        surface.ShouldRender("[✓] 界面");
    }

    /// <summary>Verifies a one-cell-wide CheckBox still clips safely and toggles from a click.</summary>
    [Fact]
    public async Task Pointer_WhenCheckBoxIsOneCellWide_ClipsAndStillTogglesAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Option", Width = Length.Cells(1) };
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(3, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("[");

        // Act
        await surface.Pointer.ClickAsync(checkBox);

        // Assert
        checkBox.IsChecked.ShouldBe(true);
        surface.ShouldRender("[");
    }

    /// <summary>Verifies a secondary click neither presses nor toggles.</summary>
    [Fact]
    public async Task Pointer_WhenSecondaryButtonClicks_DoesNotToggleAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Option" };
        var events = Record(checkBox);
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(12, 1),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.RightClickAsync(checkBox);

        // Assert
        checkBox.IsChecked.ShouldBe(false);
        events.ShouldBeEmpty();
        surface.ShouldHaveState(checkBox, VisualState.IsPointerOver);
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies the pointer-over state and entered/exited events track hover in and out.</summary>
    [Fact]
    public async Task Pointer_WhenPointerEntersAndExits_RaisesEventsAndTogglesStateAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Option" };
        var entered = 0;
        var exited = 0;
        checkBox.PointerEntered += (_, _) => entered++;
        checkBox.PointerExited += (_, _) => exited++;
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(14, 3),
            TestContext.Current.CancellationToken);

        // Act and assert
        await surface.Pointer.MoveToAsync(checkBox, new Point(6, 0));
        surface.ShouldHaveState(checkBox, VisualState.IsPointerOver);
        await surface.Pointer.MoveToAsync(new Point(13, 2));
        surface.ShouldHaveState(checkBox, VisualState.Normal);
        entered.ShouldBe(1);
        exited.ShouldBe(1);
    }

    /// <summary>Verifies hiding the CheckBox mid hold cancels the press and capture without toggling,
    /// and showing it restores pointer toggling.</summary>
    [Fact]
    public async Task Pointer_WhenHiddenDuringHold_CancelsWithoutTogglingAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Option" };
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(12, 1),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(checkBox);
        await surface.Pointer.PressAsync();

        // Act
        await surface.UpdateAsync(() => checkBox.Visibility = Visibility.Hidden, "hide held CheckBox");

        // Assert
        checkBox.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldRender("");
        await surface.Pointer.ReleaseAsync();
        checkBox.IsChecked.ShouldBe(false);

        // Act
        await surface.UpdateAsync(() => checkBox.Visibility = Visibility.Visible, "show CheckBox");
        await surface.Pointer.ClickAsync(checkBox);

        // Assert
        checkBox.IsChecked.ShouldBe(true);
    }

    /// <summary>Verifies a terminal pointer-leave during a hold cancels the press without toggling.</summary>
    [Fact]
    public async Task Pointer_WhenTerminalLeaveArrivesDuringHold_CancelsWithoutTogglingAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Option" };
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(12, 1),
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel },
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(checkBox);
        await surface.Pointer.PressAsync();

        // Act
        await surface.Pointer.LeaveAsync();

        // Assert
        checkBox.IsChecked.ShouldBe(false);
        surface.ShouldHaveState(checkBox, VisualState.Focused);
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies a programmatic state change during a pointer hold is honored: the release then
    /// toggles away from the new state with the pointer cause.</summary>
    [Fact]
    public async Task Pointer_WhenStateChangesProgrammaticallyDuringHold_ReleaseTogglesFromNewStateAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Option" };
        var events = Record(checkBox);
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(12, 1),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(checkBox);
        await surface.Pointer.PressAsync();

        // Act
        await surface.UpdateAsync(() => checkBox.IsChecked = true, "check held CheckBox");
        surface.ShouldRender("[✓] Option");
        surface.ShouldHaveState(checkBox, VisualState.IsPointerOver | VisualState.Focused | VisualState.Pressed);
        await surface.Pointer.ReleaseAsync();

        // Assert
        checkBox.IsChecked.ShouldBe(false);
        surface.ShouldRender("[ ] Option");
        events.ShouldBe([
            "Checked:False>True:Programmatic",
            "StateChanged:False>True:Programmatic",
            "Unchecked:True>False:Pointer",
            "StateChanged:True>False:Pointer"
        ]);
    }

    /// <summary>Verifies swapping the mark style mid hold keeps the press and the release toggles,
    /// rendering the new family's checked glyph.</summary>
    [Fact]
    public async Task Pointer_WhenStyleChangesDuringHold_KeepsPressAndTogglesOnReleaseAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Go" };
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(8, 1),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(checkBox, new Point(1, 0));
        await surface.Pointer.PressAsync();

        // Act
        await surface.UpdateAsync(() => checkBox.Style = CheckBoxStyle.Tick, "restyle held CheckBox");

        // Assert
        surface.ShouldRender("○ Go");
        surface.ShouldHaveState(checkBox, VisualState.IsPointerOver | VisualState.Focused | VisualState.Pressed);
        await surface.Pointer.ReleaseAsync();
        checkBox.IsChecked.ShouldBe(true);
        surface.ShouldRender("✓ Go");
    }

    /// <summary>Verifies the bound command runs after the state events with its parameter, and a
    /// non-executable command never suppresses the toggle.</summary>
    [Fact]
    public async Task Pointer_WhenCommandIsBound_TogglesThenExecutesOnlyWhenExecutableAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Option" };
        var events = Record(checkBox);
        var command = new ProbeCommand { CanExecuteValue = false, Executing = _ => events.Add("execute") };
        checkBox.Command = command;
        checkBox.CommandParameter = 42;
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(12, 1),
            TestContext.Current.CancellationToken);

        // Act with a non-executable command
        await surface.Pointer.ClickAsync(checkBox);

        // Assert the toggle still committed
        checkBox.IsChecked.ShouldBe(true);
        events.ShouldBe(["Checked:False>True:Pointer", "StateChanged:False>True:Pointer"]);
        command.Executions.ShouldBeEmpty();

        // Act with an executable command
        events.Clear();
        command.CanExecuteValue = true;
        await surface.Pointer.ClickAsync(checkBox);

        // Assert ordering and parameter
        events.ShouldBe(["Unchecked:True>False:Pointer", "StateChanged:True>False:Pointer", "execute"]);
        command.Executions.ShouldBe([42]);
    }

    /// <summary>Verifies the access key focuses and toggles the CheckBox from another focus owner, and
    /// a disabled CheckBox declares none.</summary>
    [Fact]
    public async Task Keyboard_WhenAccessKeyIsPressed_FocusesAndTogglesUnlessDisabledAsync()
    {
        // Arrange
        var button = new Button("Next") { Width = Length.Cells(8), Height = Length.Cells(3) };
        var checkBox = new CheckBox { Text = "&Option" };
        var events = Record(checkBox);
        var stack = new Stack { Orientation = Orientation.Vertical, Children = { button, checkBox } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 6),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(button);

        // Act
        await surface.SendAsync("\u001b[111;3:1u"u8.ToArray(), "press Alt+O");

        // Assert
        checkBox.IsChecked.ShouldBe(true);
        events.ShouldBe(["Checked:False>True:Keyboard", "StateChanged:False>True:Keyboard"]);
        surface.ShouldHaveFocus(checkBox);
        surface.Cell(new Point(4, 3)).Text.ShouldBe("O");
        (surface.Cell(new Point(4, 3)).Style.Attributes & TerminalAttributes.Underline).ShouldBe(TerminalAttributes.Underline);

        // Act disabled
        await surface.UpdateAsync(() => checkBox.IsEnabled = false, "disable CheckBox");
        events.Clear();
        await surface.SendAsync("\u001b[111;3:1u"u8.ToArray(), "press Alt+O again");

        // Assert
        checkBox.IsChecked.ShouldBe(true);
        events.ShouldBeEmpty();
    }

    /// <summary>Verifies turning three-state mode off while indeterminate commits false on the mounted
    /// surface and publishes Unchecked then StateChanged with the programmatic cause.</summary>
    [Fact]
    public async Task ThreeState_WhenDisabledWhileIndeterminate_RendersUncheckedAndPublishesAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Option", ThreeState = true, IsChecked = null };
        var events = Record(checkBox);
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(12, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("[─] Option");

        // Act
        await surface.UpdateAsync(() => checkBox.ThreeState = false, "leave three-state mode");

        // Assert
        checkBox.IsChecked.ShouldBe(false);
        surface.ShouldRender("[ ] Option");
        events.ShouldBe(["Unchecked:null>False:Programmatic", "StateChanged:null>False:Programmatic"]);

        // Act: two-state activation now skips the indeterminate state
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        checkBox.IsChecked.ShouldBe(false);
        surface.ShouldRender("[ ] Option");
    }

    /// <summary>Verifies Tab enters, Space toggles, Tab leaves, and Shift+Tab re-enters the CheckBox.</summary>
    [Fact]
    public async Task Keyboard_WhenTabTraverses_TogglesOnlyWhileFocusedAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Option" };
        var button = new Button("Next") { Width = Length.Cells(8), Height = Length.Cells(3) };
        var stack = new Stack { Orientation = Orientation.Vertical, Children = { checkBox, button } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 6),
            TestContext.Current.CancellationToken);

        // Act and assert
        await surface.Keyboard.TypeAsync(" ");
        checkBox.IsChecked.ShouldBe(false);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(checkBox);
        await surface.Keyboard.TypeAsync(" ");
        checkBox.IsChecked.ShouldBe(true);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(button);
        await surface.Keyboard.TypeAsync(" ");
        checkBox.IsChecked.ShouldBe(true);
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(checkBox);
        await surface.Keyboard.TypeAsync(" ");
        checkBox.IsChecked.ShouldBe(false);
    }

    private static List<string> Record(CheckBox checkBox)
    {
        List<string> events = [];
        checkBox.Checked += (_, eventArgs) => events.Add(Describe("Checked", eventArgs));
        checkBox.Unchecked += (_, eventArgs) => events.Add(Describe("Unchecked", eventArgs));
        checkBox.Indeterminate += (_, eventArgs) => events.Add(Describe("Indeterminate", eventArgs));
        checkBox.StateChanged += (_, eventArgs) => events.Add(Describe("StateChanged", eventArgs));
        return events;
    }

    private static string Describe(string name, CheckChangedEventArgs eventArgs) =>
        $"{name}:{eventArgs.Previous?.ToString() ?? "null"}>{eventArgs.Current?.ToString() ?? "null"}:{eventArgs.Cause}";
}
