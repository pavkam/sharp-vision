// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies every Button keyboard, pointer, access-key, and lifecycle interaction through a
/// mounted terminal surface, complementing the appearance-oriented ButtonSurfaceTests.</summary>
public sealed class ButtonInteractionTests
{
    /// <summary>Verifies a Space hold on a release-reporting terminal shows the pressed state, ignores
    /// key repeat while held, and activates exactly once on the paired release.</summary>
    [Fact]
    public async Task Keyboard_WhenSpaceIsHeldWithReleaseReporting_ActivatesOnceOnReleaseAsync()
    {
        // Arrange
        var button = NewButton("Save");
        List<ActivationCause> causes = [];
        button.Click += (_, eventArgs) => causes.Add(eventArgs.Cause);
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(12, 5),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.UpdateAsync(
            () => button.SetCapabilities(TestCapabilities.WithKeyReleases),
            "declare key-release reporting");

        // Act press
        await surface.Keyboard.PressCharacterAsync(new Rune(' '));

        // Assert held
        surface.ShouldHaveState(button, VisualState.Focused | VisualState.Pressed);
        causes.ShouldBeEmpty();

        // Act repeat while held
        await SendAsync(surface, "\u001b[32;1:2u", "repeat Space");

        // Assert repeat neither activates nor drops the hold
        surface.ShouldHaveState(button, VisualState.Focused | VisualState.Pressed);
        causes.ShouldBeEmpty();

        // Act release
        await surface.Keyboard.ReleaseCharacterAsync(new Rune(' '));

        // Assert
        surface.ShouldHaveState(button, VisualState.Focused);
        causes.ShouldBe([ActivationCause.Keyboard]);
    }

    /// <summary>Verifies moving focus away with Tab during a Space hold cancels the press without
    /// activating, and the orphaned release neither activates the old nor the new focus owner.</summary>
    [Fact]
    public async Task Keyboard_WhenFocusLeavesDuringSpaceHold_CancelsWithoutActivatingAsync()
    {
        // Arrange
        var save = NewButton("Save");
        var other = NewButton("Other");
        var saveClicks = 0;
        var otherClicks = 0;
        save.Click += (_, _) => saveClicks++;
        other.Click += (_, _) => otherClicks++;
        var stack = new Stack { Orientation = Orientation.Vertical, Children = { save, other } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 8),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(save);
        await surface.UpdateAsync(
            () => stack.SetCapabilities(TestCapabilities.WithKeyReleases),
            "declare key-release reporting");
        await surface.Keyboard.PressCharacterAsync(new Rune(' '));
        surface.ShouldHaveState(save, VisualState.Focused | VisualState.Pressed);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert focus moved and the hold was cancelled
        surface.ShouldHaveFocus(other);
        save.IsPressed.ShouldBeFalse();
        saveClicks.ShouldBe(0);

        // Act orphaned release
        await surface.Keyboard.ReleaseCharacterAsync(new Rune(' '));

        // Assert nothing activates
        saveClicks.ShouldBe(0);
        otherClicks.ShouldBe(0);
        other.IsPressed.ShouldBeFalse();
    }

    /// <summary>Verifies disabling the Button mid Space hold clears the press and focus, the orphaned
    /// release does not activate, and re-enabling restores ordinary keyboard activation.</summary>
    [Fact]
    public async Task Keyboard_WhenDisabledDuringSpaceHold_CancelsThenRecoversOnReenableAsync()
    {
        // Arrange
        var button = NewButton("Save");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(12, 5),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.UpdateAsync(
            () => button.SetCapabilities(TestCapabilities.WithKeyReleases),
            "declare key-release reporting");
        await surface.Keyboard.PressCharacterAsync(new Rune(' '));

        // Act
        await surface.UpdateAsync(() => button.IsEnabled = false, "disable held Button");

        // Assert cancelled
        surface.ShouldHaveState(button, VisualState.Disabled);
        surface.ShouldHaveFocus(null);
        await surface.Keyboard.ReleaseCharacterAsync(new Rune(' '));
        clicks.ShouldBe(0);

        // Act recover
        await surface.UpdateAsync(() => button.IsEnabled = true, "re-enable Button");
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));

        // Assert
        clicks.ShouldBe(1);
        surface.ShouldHaveState(button, VisualState.Focused);
    }

    /// <summary>Verifies Enter activates only on its initial press: a held-key repeat and the release
    /// are consumed without activating again.</summary>
    [Fact]
    public async Task Keyboard_WhenEnterRepeatsAndReleases_ActivatesOnlyOnInitialPressAsync()
    {
        // Arrange
        var button = NewButton("Save");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(12, 5),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);
        await SendAsync(surface, "\u001b[13;1:2u", "repeat Enter");
        await SendAsync(surface, "\u001b[13;1:3u", "release Enter");

        // Assert
        clicks.ShouldBe(1);
        surface.ShouldHaveState(button, VisualState.Focused);
    }

    /// <summary>Verifies an activation key carrying an application-command modifier never activates,
    /// while a text-producing Shift (or lock) modifier still does.</summary>
    /// <param name="sequence">The Kitty keyboard sequence for the modified key press.</param>
    /// <param name="expectedClicks">The number of activations the press must produce.</param>
    [Theory]
    [InlineData("\u001b[13;5u", 0)] // Ctrl+Enter
    [InlineData("\u001b[13;3u", 0)] // Alt+Enter
    [InlineData("\u001b[13;9u", 0)] // Super+Enter
    [InlineData("\u001b[13;2u", 1)] // Shift+Enter
    [InlineData("\u001b[13;65u", 1)] // CapsLock+Enter
    [InlineData("\u001b[32;5u", 0)] // Ctrl+Space
    [InlineData("\u001b[32;3u", 0)] // Alt+Space
    [InlineData("\u001b[32;2u", 1)] // Shift+Space
    public async Task Keyboard_WhenActivationKeyCarriesModifier_ActivatesOnlyForEligibleChordAsync(
        string sequence,
        int expectedClicks)
    {
        // Arrange
        var button = NewButton("Save");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(12, 5),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await SendAsync(surface, sequence, "press modified activation key");

        // Assert
        clicks.ShouldBe(expectedClicks);
        button.IsPressed.ShouldBeFalse();
        surface.ShouldHaveFocus(button);
    }

    /// <summary>Verifies Tab and Shift+Tab traverse between buttons, and activation keys reach only the
    /// focused button - never a hovered but unfocused one.</summary>
    [Fact]
    public async Task Keyboard_WhenFocusTraversesButtons_ActivationFollowsFocusNotHoverAsync()
    {
        // Arrange
        var save = NewButton("Save");
        var other = NewButton("Other");
        var saveClicks = 0;
        var otherClicks = 0;
        save.Click += (_, _) => saveClicks++;
        other.Click += (_, _) => otherClicks++;
        var stack = new Stack { Orientation = Orientation.Vertical, Children = { save, other } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 8),
            TestContext.Current.CancellationToken);

        // Act and assert: nothing focused yet, so Enter activates nothing
        await surface.Keyboard.PressAsync(Code.Enter);
        saveClicks.ShouldBe(0);
        otherClicks.ShouldBe(0);

        // Act and assert traversal
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(save);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(other);
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(save);

        // Act: hover the unfocused button and activate with the keyboard
        await surface.Pointer.MoveToAsync(other);
        surface.ShouldHaveState(other, VisualState.IsPointerOver);
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.TypeAsync(" ");

        // Assert: only the focused button activated
        saveClicks.ShouldBe(2);
        otherClicks.ShouldBe(0);
    }

    /// <summary>Verifies a held pointer that leaves the face drops the pressed state while keeping
    /// capture, a re-entry restores it, and only a release inside the face activates.</summary>
    [Fact]
    public async Task Pointer_WhenHeldPointerLeavesAndReentersFace_ActivatesOnlyOnInsideReleaseAsync()
    {
        // Arrange
        var button = NewButton("Save");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(12, 5),
            TestContext.Current.CancellationToken);
        var outside = new Point(10, 4);

        // Act press and drag out
        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();
        surface.ShouldHaveState(button, VisualState.IsPointerOver | VisualState.Focused | VisualState.Pressed);
        await surface.Pointer.MovePressedToAsync(outside);

        // Assert pressed dropped, capture retained
        button.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(button);

        // Act drag back in and release
        await surface.Pointer.MovePressedToAsync(await surface.ResolvePointAsync(button));
        button.IsPressed.ShouldBeTrue();
        await surface.Pointer.ReleaseAsync();

        // Assert activated once
        clicks.ShouldBe(1);
        surface.ShouldHaveCapture(null);

        // Act press, drag out, release out
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(outside);
        await surface.Pointer.ReleaseAsync();

        // Assert no activation, clean state
        clicks.ShouldBe(1);
        button.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(button);
    }

    /// <summary>Verifies a Tab keystroke arriving during a pointer hold moves focus, which cancels the
    /// press and releases capture, so the later pointer release activates nothing.</summary>
    [Fact]
    public async Task Pointer_WhenFocusMovesDuringPointerHold_CancelsPressAndReleasesCaptureAsync()
    {
        // Arrange
        var save = NewButton("Save");
        var other = NewButton("Other");
        var clicks = 0;
        save.Click += (_, _) => clicks++;
        other.Click += (_, _) => clicks++;
        var stack = new Stack { Orientation = Orientation.Vertical, Children = { save, other } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 8),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(save);
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(save);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.ShouldHaveFocus(other);
        save.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);

        // Act release at the original point
        await surface.Pointer.ReleaseAsync();

        // Assert
        clicks.ShouldBe(0);
    }

    /// <summary>Verifies a secondary-button click neither presses, focuses, captures, nor activates.</summary>
    [Fact]
    public async Task Pointer_WhenSecondaryButtonClicks_DoesNotPressOrActivateAsync()
    {
        // Arrange
        var button = NewButton("Save");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(12, 5),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.RightClickAsync(button);

        // Assert
        clicks.ShouldBe(0);
        surface.ShouldHaveState(button, VisualState.IsPointerOver);
        surface.ShouldHaveCapture(null);
        button.IsFocused.ShouldBeFalse();
    }

    /// <summary>Verifies hiding the Button mid hold cancels the press and capture, the orphaned release
    /// activates nothing, and showing it again restores pointer activation.</summary>
    [Fact]
    public async Task Pointer_WhenHiddenDuringHold_CancelsThenRecoversWhenShownAsync()
    {
        // Arrange
        var button = NewButton("Save");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(12, 5),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();

        // Act
        await surface.UpdateAsync(() => button.Visibility = Visibility.Hidden, "hide held Button");

        // Assert
        button.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldRender("");
        await surface.Pointer.ReleaseAsync();
        clicks.ShouldBe(0);

        // Act show and click
        await surface.UpdateAsync(() => button.Visibility = Visibility.Visible, "show Button");
        await surface.Pointer.ClickAsync(button);

        // Assert
        clicks.ShouldBe(1);
    }

    /// <summary>Verifies disabling an ancestor mid hold cancels the press through the effective
    /// enabled state exactly like disabling the Button itself.</summary>
    [Fact]
    public async Task Pointer_WhenAncestorIsDisabledDuringHold_CancelsPressAsync()
    {
        // Arrange
        var button = NewButton("Save");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        var stack = new Stack { Children = { button } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 5),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();

        // Act
        await surface.UpdateAsync(() => stack.IsEnabled = false, "disable Button ancestor");

        // Assert
        surface.ShouldHaveState(button, VisualState.Disabled);
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
        await surface.Pointer.ReleaseAsync();
        clicks.ShouldBe(0);
    }

    /// <summary>Verifies removing the Button from its parent mid hold releases capture and clears the
    /// press, and the orphaned release neither throws nor activates.</summary>
    [Fact]
    public async Task Pointer_WhenDetachedDuringHold_ReleasesCaptureWithoutActivatingAsync()
    {
        // Arrange
        var button = NewButton("Save");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        var stack = new Stack { Children = { button } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 5),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();

        // Act
        await surface.UpdateAsync(() => stack.Children.Remove(button).ShouldBeTrue(), "detach held Button");

        // Assert
        button.IsPressed.ShouldBeFalse();
        button.Parent.ShouldBeNull();
        surface.ShouldHaveCapture(null);
        surface.ShouldRender("");
        await surface.Pointer.ReleaseAsync();
        clicks.ShouldBe(0);
    }

    /// <summary>Verifies disposing the Button mid hold releases capture, and the orphaned release does
    /// not throw.</summary>
    [Fact]
    public async Task Pointer_WhenDisposedDuringHold_ReleasesCaptureWithoutThrowingAsync()
    {
        // Arrange
        var button = NewButton("Save");
        var stack = new Stack { Children = { button } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 5),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(button);

        // Act
        await surface.UpdateAsync(button.Dispose, "dispose held Button");

        // Assert
        button.IsDisposed.ShouldBeTrue();
        surface.ShouldHaveCapture(null);
        surface.ShouldRender("");
        await surface.Pointer.ReleaseAsync();
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies swapping the local style mid hold keeps the pressed state, and the release still
    /// activates against the restyled (shadow-translated) face.</summary>
    [Fact]
    public async Task Pointer_WhenStyleChangesDuringHold_KeepsPressAndActivatesOnReleaseAsync()
    {
        // Arrange
        var button = NewButton("Save");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(12, 5),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();

        // Act
        await surface.UpdateAsync(() => button.Style = ButtonStyle.Filled, "restyle held Button");

        // Assert the hold survived the restyle
        button.ActualStyle.ShouldBe(ButtonStyle.Filled);
        surface.ShouldHaveState(button, VisualState.IsPointerOver | VisualState.Focused | VisualState.Pressed);
        surface.ShouldHaveCapture(button);

        // Act
        await surface.Pointer.ReleaseAsync();

        // Assert
        clicks.ShouldBe(1);
        button.IsPressed.ShouldBeFalse();
    }

    /// <summary>Verifies the pointer-over state and its entered/exited events track hover in, out, and
    /// back in.</summary>
    [Fact]
    public async Task Pointer_WhenPointerEntersAndExits_RaisesEventsAndTogglesStateAsync()
    {
        // Arrange
        var button = NewButton("Save");
        var entered = 0;
        var exited = 0;
        button.PointerEntered += (_, _) => entered++;
        button.PointerExited += (_, _) => exited++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(12, 5),
            TestContext.Current.CancellationToken);

        // Act and assert enter
        await surface.Pointer.MoveToAsync(button);
        surface.ShouldHaveState(button, VisualState.IsPointerOver);
        entered.ShouldBe(1);
        exited.ShouldBe(0);

        // Act and assert exit
        await surface.Pointer.MoveToAsync(new Point(10, 4));
        surface.ShouldHaveState(button, VisualState.Normal);
        entered.ShouldBe(1);
        exited.ShouldBe(1);

        // Act and assert re-enter
        await surface.Pointer.MoveToAsync(button, new Point(0, 0));
        surface.ShouldHaveState(button, VisualState.IsPointerOver);
        entered.ShouldBe(2);
        exited.ShouldBe(1);
    }

    /// <summary>Verifies a two-by-one Button still focuses and activates from a click even though its
    /// chrome cannot fit.</summary>
    [Fact]
    public async Task Pointer_WhenButtonIsTiny_StillActivatesFromClickAsync()
    {
        // Arrange
        var button = new Button("Save")
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(2),
            Height = Length.Cells(1)
        };
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(4, 2),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(button, new Point(1, 0));

        // Assert
        button.Bounds.ShouldBe(new Rect(0, 0, 2, 1));
        clicks.ShouldBe(1);
        surface.ShouldHaveFocus(button);
    }

    /// <summary>Verifies a pointer click raises Click before executing the bound command with its
    /// parameter, a non-executable command suppresses both, and a CanExecuteChanged notification on
    /// the dispatcher schedules a repaint.</summary>
    [Fact]
    public async Task Pointer_WhenCommandIsBound_RaisesClickThenExecutesAndTracksExecutabilityAsync()
    {
        // Arrange
        List<string> order = [];
        var command = new ProbeCommand { Executing = _ => order.Add("execute") };
        var button = NewButton("Save");
        button.Command = command;
        button.CommandParameter = "parameter";
        button.Click += (_, eventArgs) => order.Add($"click:{eventArgs.Cause}");
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(12, 5),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(button);

        // Assert
        order.ShouldBe(["click:Pointer", "execute"]);
        command.Executions.ShouldBe(["parameter"]);

        // Act with a non-executable command
        command.CanExecuteValue = false;
        order.Clear();
        await surface.Pointer.ClickAsync(button);

        // Assert neither Click nor Execute ran
        order.ShouldBeEmpty();
        command.Executions.Count.ShouldBe(1);

        // Act: executability notification on the dispatcher thread
        var pending = Invalidation.None;
        await surface.UpdateAsync(
            () =>
            {
                command.RaiseCanExecuteChanged();
                pending = button.Pending;
            },
            "raise CanExecuteChanged");

        // Assert a repaint was requested
        (pending & Invalidation.Render).ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies a GotFocus handler that disables the Button during a pointer press stops the
    /// press before it is armed: no pressed state, no capture, and no activation on release.</summary>
    [Fact]
    public async Task Pointer_WhenFocusCallbackDisablesButton_StopsPressBeforeArmingAsync()
    {
        // Arrange
        var button = NewButton("Save");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        button.GotFocus += (_, _) => button.IsEnabled = false;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(12, 5),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();

        // Assert
        button.IsEnabled.ShouldBeFalse();
        button.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
        await surface.Pointer.ReleaseAsync();
        clicks.ShouldBe(0);
    }

    /// <summary>Verifies a pressed-state observer that disables the Button during a press-only Space
    /// stroke stops the activation that would otherwise complete on that same press.</summary>
    [Fact]
    public async Task Keyboard_WhenPressedObserverDisablesButtonOnPressOnlySpace_DoesNotActivateAsync()
    {
        // Arrange
        var button = NewButton("Save");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        button.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Button.IsPressed) && button.IsPressed)
            {
                button.IsEnabled = false;
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(12, 5),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act: a bare space byte, the press-only terminal path
        await surface.Keyboard.TypeAsync(" ");

        // Assert
        clicks.ShouldBe(0);
        surface.ShouldHaveState(button, VisualState.Disabled);
        surface.ShouldHaveFocus(null);
    }

    /// <summary>Verifies an Alt access key pressed while another control owns focus moves focus to the
    /// Button and activates it with the keyboard cause.</summary>
    [Fact]
    public async Task Keyboard_WhenAccessKeyIsPressedFromAnotherFocusOwner_FocusesAndActivatesAsync()
    {
        // Arrange
        var first = new CheckBox { Text = "&First" };
        var save = NewButton("&Save");
        List<ActivationCause> causes = [];
        save.Click += (_, eventArgs) => causes.Add(eventArgs.Cause);
        var stack = new Stack { Orientation = Orientation.Vertical, Children = { first, save } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 6),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);

        // Act
        await SendAsync(surface, "\u001b[115;3:1u", "press Alt+S");

        // Assert
        causes.ShouldBe([ActivationCause.Keyboard]);
        surface.ShouldHaveFocus(save);
        first.IsChecked.ShouldBe(false);
    }

    /// <summary>Verifies the mnemonic marker is not drawn, the marked grapheme is underlined, and the
    /// access key works from the neutral host focus.</summary>
    [Fact]
    public async Task Render_WhenCaptionDeclaresAccessKey_UnderlinesOnlyTheMarkedGraphemeAsync()
    {
        // Arrange
        var button = NewButton("&Save");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 4),
            TestContext.Current.CancellationToken);

        // Assert rendering
        surface.ShouldRender("""
                             ┏━━━━━━┓
                             ┃ Save ┃
                             ┗━━━━━━┛
                             """);
        IsUnderlined(surface.Cell(new Point(2, 1))).ShouldBeTrue();
        IsUnderlined(surface.Cell(new Point(3, 1))).ShouldBeFalse();
        IsUnderlined(surface.Cell(new Point(5, 1))).ShouldBeFalse();

        // Act
        await SendAsync(surface, "\u001b[115;3:1u", "press Alt+S");

        // Assert
        clicks.ShouldBe(1);
        surface.ShouldHaveFocus(button);
    }

    /// <summary>Verifies UseMnemonic=false renders the ampersand literally and declares no access key.</summary>
    [Fact]
    public async Task Keyboard_WhenUseMnemonicIsOff_RendersAmpersandAndIgnoresAltKeyAsync()
    {
        // Arrange
        var button = new Button("&Save")
        {
            UseMnemonic = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(9),
            Height = Length.Cells(3)
        };
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 4),
            TestContext.Current.CancellationToken);

        // Assert rendering
        surface.ShouldRender("""
                             ┏━━━━━━━┓
                             ┃ &Save ┃
                             ┗━━━━━━━┛
                             """);
        IsUnderlined(surface.Cell(new Point(3, 1))).ShouldBeFalse();

        // Act
        await SendAsync(surface, "\u001b[115;3:1u", "press Alt+S");

        // Assert
        clicks.ShouldBe(0);
        button.IsFocused.ShouldBeFalse();
    }

    /// <summary>Verifies an access key declared by a disabled Button is skipped in favor of the next
    /// enabled match, and a chord with Ctrl is not an access key at all.</summary>
    [Fact]
    public async Task Keyboard_WhenAccessKeyTargetIsDisabledOrChordHasControl_ActivatesOnlyEligibleMatchAsync()
    {
        // Arrange
        var apply = NewButton("&Apply");
        apply.IsEnabled = false;
        var again = NewButton("&Again");
        var applyClicks = 0;
        var againClicks = 0;
        apply.Click += (_, _) => applyClicks++;
        again.Click += (_, _) => againClicks++;
        var stack = new Stack { Orientation = Orientation.Vertical, Children = { apply, again } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 8),
            TestContext.Current.CancellationToken);

        // Act Ctrl+Alt+A
        await SendAsync(surface, "\u001b[97;7:1u", "press Ctrl+Alt+A");

        // Assert
        applyClicks.ShouldBe(0);
        againClicks.ShouldBe(0);

        // Act Alt+A
        await SendAsync(surface, "\u001b[97;3:1u", "press Alt+A");

        // Assert
        applyClicks.ShouldBe(0);
        againClicks.ShouldBe(1);
        surface.ShouldHaveFocus(again);
    }

    /// <summary>Verifies duplicate access keys cycle: each press activates the match after the currently
    /// focused one, wrapping around.</summary>
    [Fact]
    public async Task Keyboard_WhenAccessKeysAreDuplicated_CyclesThroughMatchesAfterFocusAsync()
    {
        // Arrange
        var apply = NewButton("&Apply");
        var again = NewButton("&Again");
        List<string> activations = [];
        apply.Click += (_, _) => activations.Add("apply");
        again.Click += (_, _) => activations.Add("again");
        var stack = new Stack { Orientation = Orientation.Vertical, Children = { apply, again } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 8),
            TestContext.Current.CancellationToken);

        // Act
        await SendAsync(surface, "\u001b[97;3:1u", "press Alt+A");
        surface.ShouldHaveFocus(apply);
        await SendAsync(surface, "\u001b[97;3:1u", "press Alt+A");
        surface.ShouldHaveFocus(again);
        await SendAsync(surface, "\u001b[97;3:1u", "press Alt+A");

        // Assert
        activations.ShouldBe(["apply", "again", "apply"]);
        surface.ShouldHaveFocus(apply);
    }

    /// <summary>Verifies a caption change while mounted retargets the access key immediately.</summary>
    [Fact]
    public async Task Keyboard_WhenCaptionChangesWhileMounted_RetargetsAccessKeyAsync()
    {
        // Arrange
        var button = NewButton("&Save");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(12, 5),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => button.Text = "&Open", "rename caption");
        await SendAsync(surface, "\u001b[115;3:1u", "press Alt+S");

        // Assert the old key is gone
        clicks.ShouldBe(0);

        // Act
        await SendAsync(surface, "\u001b[111;3:1u", "press Alt+O");

        // Assert the new key works
        clicks.ShouldBe(1);
        surface.Cell(new Point(2, 1)).Text.ShouldBe("O");
        IsUnderlined(surface.Cell(new Point(2, 1))).ShouldBeTrue();
    }

    /// <summary>Verifies a Window's default and cancel Buttons activate from Enter and Escape only when
    /// the focused control leaves the key unhandled, and a disabled default Button never activates.</summary>
    [Fact]
    public async Task Keyboard_WhenWindowHasDefaultAndCancelButtons_ActivatesOnlyForUnhandledKeysAsync()
    {
        // Arrange
        var option = new CheckBox { Text = "Option" };
        var slider = new Slider { Maximum = 10 };
        var ok = new Button("OK") { IsDefault = true };
        var cancel = new Button("Cancel") { IsCancel = true };
        var okClicks = 0;
        var cancelClicks = 0;
        ok.Click += (_, _) => okClicks++;
        cancel.Click += (_, _) => cancelClicks++;
        var window = new Window
        {
            Content = new Stack
            {
                Orientation = Orientation.Vertical,
                Children = { option, slider, ok, cancel }
            }
        };
        var root = new Overlay { Children = { window } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 14),
            TestContext.Current.CancellationToken);

        // Act: Enter on a control that consumes it
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(option).ShouldBeTrue(), "focus CheckBox");
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert the CheckBox toggled and the default Button stayed idle
        option.IsChecked.ShouldBe(true);
        okClicks.ShouldBe(0);

        // Act: Enter and Escape on a control that leaves them unhandled
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(slider).ShouldBeTrue(), "focus Slider");
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        okClicks.ShouldBe(1);
        cancelClicks.ShouldBe(1);
        slider.Value.ShouldBe(0);

        // Act: Escape on the CheckBox, which does not consume Escape
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(option).ShouldBeTrue(), "refocus CheckBox");
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        cancelClicks.ShouldBe(2);

        // Act: a disabled default Button
        await surface.UpdateAsync(() => ok.IsEnabled = false, "disable default Button");
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(slider).ShouldBeTrue(), "refocus Slider");
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        okClicks.ShouldBe(1);
    }

    /// <summary>Verifies a terminal pointer-leave report during a hold clears press and capture, leaves
    /// focus intact, and the next real click activates normally.</summary>
    [Fact]
    public async Task Pointer_WhenTerminalLeaveArrivesDuringHold_CancelsPressAndKeepsFocusAsync()
    {
        // Arrange
        var button = NewButton("Save");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(12, 5),
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel },
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();

        // Act: the terminal-leave report only exists in the pixel-coordinate mouse protocol
        await surface.Pointer.LeaveAsync();

        // Assert
        surface.ShouldHaveState(button, VisualState.Focused);
        surface.ShouldHaveCapture(null);
        clicks.ShouldBe(0);

        // Act
        await surface.Pointer.ClickAsync(button);

        // Assert
        clicks.ShouldBe(1);
    }

    private static Button NewButton(string text) => new(text)
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Width = Length.Cells(8),
        Height = Length.Cells(3)
    };

    private static bool IsUnderlined(SurfaceCell cell) =>
        (cell.Style.Attributes & TerminalAttributes.Underline) != 0 || cell.Style.Underline != Underline.None;

    /// <summary>Sends one complete escape sequence as real terminal input.</summary>
    private static Task SendAsync(ComponentSurface surface, string sequence, string description) =>
        surface.SendAsync(Encoding.ASCII.GetBytes(sequence), description);
}
