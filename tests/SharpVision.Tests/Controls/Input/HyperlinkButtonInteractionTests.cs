// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies every HyperlinkButton keyboard, pointer, access-key, and lifecycle interaction
/// through a mounted terminal surface, complementing the appearance-oriented HyperlinkButtonSurfaceTests.</summary>
public sealed class HyperlinkButtonInteractionTests
{
    /// <summary>Verifies a Space hold with key-release reporting shows the pressed state, ignores repeat,
    /// and activates once on release; Enter activates once regardless of repeat and release.</summary>
    [Fact]
    public async Task Keyboard_WhenSpaceIsHeldAndEnterRepeats_ActivatesExactlyOncePerGestureAsync()
    {
        // Arrange
        var link = NewLink("Visit site");
        List<ActivationCause> causes = [];
        link.Click += (_, eventArgs) => causes.Add(eventArgs.Cause);
        await using var surface = await ComponentSurface.MountAsync(
            link,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.UpdateAsync(
            () => link.SetCapabilities(TestCapabilities.WithKeyReleases),
            "declare key-release reporting");

        // Act Space hold
        await surface.Keyboard.PressCharacterAsync(new Rune(' '));
        surface.ShouldHaveState(link, VisualState.Focused | VisualState.Pressed);
        await surface.SendAsync("\u001b[32;1:2u"u8.ToArray(), "repeat Space");
        surface.ShouldHaveState(link, VisualState.Focused | VisualState.Pressed);
        causes.ShouldBeEmpty();
        await surface.Keyboard.ReleaseCharacterAsync(new Rune(' '));

        // Assert
        causes.ShouldBe([ActivationCause.Keyboard]);
        surface.ShouldHaveState(link, VisualState.Focused);

        // Act Enter press, repeat, release
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.SendAsync("\u001b[13;1:2u"u8.ToArray(), "repeat Enter");
        await surface.SendAsync("\u001b[13;1:3u"u8.ToArray(), "release Enter");

        // Assert
        causes.Count.ShouldBe(2);
        link.IsPressed.ShouldBeFalse();
    }

    /// <summary>Verifies an activation key carrying an application-command modifier never activates,
    /// while Shift still does.</summary>
    /// <param name="sequence">The Kitty keyboard sequence for the modified key press.</param>
    /// <param name="expectedClicks">The number of activations the press must produce.</param>
    [Theory]
    [InlineData("\u001b[13;5u", 0)] // Ctrl+Enter
    [InlineData("\u001b[32;5u", 0)] // Ctrl+Space
    [InlineData("\u001b[32;3u", 0)] // Alt+Space
    [InlineData("\u001b[13;2u", 1)] // Shift+Enter
    [InlineData("\u001b[32;2u", 1)] // Shift+Space
    public async Task Keyboard_WhenActivationKeyCarriesModifier_ActivatesOnlyForEligibleChordAsync(
        string sequence,
        int expectedClicks)
    {
        // Arrange
        var link = NewLink("Visit site");
        var clicks = 0;
        link.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            link,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.SendAsync(Encoding.ASCII.GetBytes(sequence), "press modified activation key");

        // Assert
        clicks.ShouldBe(expectedClicks);
        link.IsPressed.ShouldBeFalse();
    }

    /// <summary>Verifies a Tab mid Space hold cancels without activating, and disabling mid hold does too.</summary>
    [Fact]
    public async Task Keyboard_WhenFocusLeavesOrLinkIsDisabledDuringSpaceHold_CancelsWithoutActivatingAsync()
    {
        // Arrange
        var link = NewLink("Visit site");
        var button = new Button("Next") { Width = Length.Cells(8), Height = Length.Cells(3) };
        var clicks = 0;
        link.Click += (_, _) => clicks++;
        var stack = new Stack { Orientation = Orientation.Vertical, Children = { link, button } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 6),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.UpdateAsync(
            () => link.SetCapabilities(TestCapabilities.WithKeyReleases),
            "declare key-release reporting");

        // Act hold then Tab
        await surface.Keyboard.PressCharacterAsync(new Rune(' '));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.ReleaseCharacterAsync(new Rune(' '));

        // Assert
        surface.ShouldHaveFocus(button);
        link.IsPressed.ShouldBeFalse();
        clicks.ShouldBe(0);

        // Act hold then disable
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(link);
        await surface.Keyboard.PressCharacterAsync(new Rune(' '));
        await surface.UpdateAsync(() => link.IsEnabled = false, "disable held link");
        await surface.Keyboard.ReleaseCharacterAsync(new Rune(' '));

        // Assert
        surface.ShouldHaveState(link, VisualState.Disabled);
        clicks.ShouldBe(0);
    }

    /// <summary>Verifies a held pointer that leaves the link drops the press but keeps capture, only an
    /// inside release activates, and a Tab during the hold cancels it.</summary>
    [Fact]
    public async Task Pointer_WhenHeldPointerLeavesOrFocusMoves_ActivatesOnlyOnInsideReleaseAsync()
    {
        // Arrange
        var link = NewLink("Visit site");
        var button = new Button("Next") { Width = Length.Cells(8), Height = Length.Cells(3) };
        var clicks = 0;
        link.Click += (_, _) => clicks++;
        var stack = new Stack { Orientation = Orientation.Vertical, Children = { link, button } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(14, 6),
            TestContext.Current.CancellationToken);
        var outside = new Point(13, 5);

        // Act press, out, release out
        await surface.Pointer.MoveToAsync(link);
        await surface.Pointer.PressAsync();
        surface.ShouldHaveState(link, VisualState.IsPointerOver | VisualState.Focused | VisualState.Pressed);
        await surface.Pointer.MovePressedToAsync(outside);
        link.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(link);
        await surface.Pointer.ReleaseAsync();
        clicks.ShouldBe(0);
        surface.ShouldHaveCapture(null);

        // Act press, out, back in, release in
        await surface.Pointer.MoveToAsync(link);
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(outside);
        await surface.Pointer.MovePressedToAsync(await surface.ResolvePointAsync(link));
        link.IsPressed.ShouldBeTrue();
        await surface.Pointer.ReleaseAsync();
        clicks.ShouldBe(1);

        // Act press then Tab
        await surface.Pointer.PressAsync();
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(button);
        link.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        await surface.Pointer.ReleaseAsync();

        // Assert
        clicks.ShouldBe(1);
    }

    /// <summary>Verifies a secondary click neither presses nor activates.</summary>
    [Fact]
    public async Task Pointer_WhenSecondaryButtonClicks_DoesNotPressOrActivateAsync()
    {
        // Arrange
        var link = NewLink("Visit site");
        var clicks = 0;
        link.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            link,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.RightClickAsync(link);

        // Assert
        clicks.ShouldBe(0);
        surface.ShouldHaveState(link, VisualState.IsPointerOver);
        surface.ShouldHaveCapture(null);
        link.IsFocused.ShouldBeFalse();
    }

    /// <summary>Verifies hiding the link mid hold cancels press and capture, the orphaned release is
    /// inert, and showing it restores activation; a terminal leave mid hold behaves the same.</summary>
    [Fact]
    public async Task Pointer_WhenHiddenOrTerminalLeavesDuringHold_CancelsWithoutActivatingAsync()
    {
        // Arrange
        var link = NewLink("Visit site");
        var clicks = 0;
        link.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            link,
            new Size(12, 3),
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel },
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(link);
        await surface.Pointer.PressAsync();

        // Act hide
        await surface.UpdateAsync(() => link.Visibility = Visibility.Hidden, "hide held link");

        // Assert
        link.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldRender("");
        await surface.Pointer.ReleaseAsync();
        clicks.ShouldBe(0);

        // Act show, press, leave
        await surface.UpdateAsync(() => link.Visibility = Visibility.Visible, "show link");
        await surface.Pointer.MoveToAsync(link);
        await surface.Pointer.PressAsync();
        await surface.Pointer.LeaveAsync();

        // Assert
        surface.ShouldHaveState(link, VisualState.Focused);
        surface.ShouldHaveCapture(null);
        clicks.ShouldBe(0);

        // Act a real click
        await surface.Pointer.ClickAsync(link);

        // Assert
        clicks.ShouldBe(1);
    }

    /// <summary>Verifies the pointer-over state and entered/exited events track hover in and out.</summary>
    [Fact]
    public async Task Pointer_WhenPointerEntersAndExits_RaisesEventsAndTogglesStateAsync()
    {
        // Arrange
        var link = NewLink("Visit site");
        var entered = 0;
        var exited = 0;
        link.PointerEntered += (_, _) => entered++;
        link.PointerExited += (_, _) => exited++;
        await using var surface = await ComponentSurface.MountAsync(
            link,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Act and assert
        await surface.Pointer.MoveToAsync(link);
        surface.ShouldHaveState(link, VisualState.IsPointerOver);
        await surface.Pointer.MoveToAsync(new Point(11, 2));
        surface.ShouldHaveState(link, VisualState.Normal);
        entered.ShouldBe(1);
        exited.ShouldBe(1);
    }

    /// <summary>Verifies a two-cell link clips its caption and still activates from a click, and a wide
    /// caption's continuation cell activates too.</summary>
    [Fact]
    public async Task Pointer_WhenLinkIsTinyOrWide_StillActivatesFromClickAsync()
    {
        // Arrange
        var tiny = new HyperlinkButton("Visit site")
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(2),
            Height = Length.Cells(1)
        };
        var wide = NewLink("界面");
        var clicks = new Dictionary<string, int> { ["tiny"] = 0, ["wide"] = 0 };
        tiny.Click += (_, _) => clicks["tiny"]++;
        wide.Click += (_, _) => clicks["wide"]++;
        var stack = new Stack { Orientation = Orientation.Vertical, Children = { tiny, wide } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(2, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(1, 1)).Continuation.ShouldBeTrue();

        // Act
        await surface.Pointer.ClickAsync(tiny, new Point(1, 0));
        await surface.Pointer.ClickAsync(wide, new Point(1, 0));

        // Assert
        clicks["tiny"].ShouldBe(1);
        clicks["wide"].ShouldBe(1);
    }

    /// <summary>Verifies a pointer click raises Click before executing the bound command with its
    /// parameter, and a non-executable command suppresses both.</summary>
    [Fact]
    public async Task Pointer_WhenCommandIsBound_RaisesClickThenExecutesOnlyWhenExecutableAsync()
    {
        // Arrange
        List<string> order = [];
        var command = new ProbeCommand { Executing = _ => order.Add("execute") };
        var link = NewLink("Visit site");
        link.Command = command;
        link.CommandParameter = "target";
        link.Click += (_, eventArgs) => order.Add($"click:{eventArgs.Cause}");
        await using var surface = await ComponentSurface.MountAsync(
            link,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(link);

        // Assert
        order.ShouldBe(["click:Pointer", "execute"]);
        command.Executions.ShouldBe(["target"]);

        // Act
        command.CanExecuteValue = false;
        order.Clear();
        await surface.Pointer.ClickAsync(link);

        // Assert
        order.ShouldBeEmpty();
    }

    /// <summary>Verifies the access key focuses and activates the link from another focus owner, is
    /// skipped while disabled, and follows a live caption change.</summary>
    [Fact]
    public async Task Keyboard_WhenAccessKeyIsPressed_FocusesAndActivatesFollowingCaptionAsync()
    {
        // Arrange
        var button = new Button("Next") { Width = Length.Cells(8), Height = Length.Cells(3) };
        var link = NewLink("&Visit site");
        List<ActivationCause> causes = [];
        link.Click += (_, eventArgs) => causes.Add(eventArgs.Cause);
        var stack = new Stack { Orientation = Orientation.Vertical, Children = { button, link } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 6),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(button);
        surface.Cell(new Point(0, 3)).Text.ShouldBe("V");
        surface.Cell(new Point(1, 3)).Text.ShouldBe("i");

        // Act
        await surface.SendAsync("\u001b[118;3:1u"u8.ToArray(), "press Alt+V");

        // Assert
        causes.ShouldBe([ActivationCause.Keyboard]);
        surface.ShouldHaveFocus(link);

        // Act disabled
        await surface.UpdateAsync(() => link.IsEnabled = false, "disable link");
        await surface.SendAsync("\u001b[118;3:1u"u8.ToArray(), "press Alt+V while disabled");
        causes.Count.ShouldBe(1);

        // Act caption change
        await surface.UpdateAsync(
            () =>
            {
                link.IsEnabled = true;
                link.Text = "&Open";
            },
            "re-enable and rename link");
        await surface.SendAsync("\u001b[118;3:1u"u8.ToArray(), "press stale Alt+V");
        causes.Count.ShouldBe(1);
        await surface.SendAsync("\u001b[111;3:1u"u8.ToArray(), "press Alt+O");

        // Assert
        causes.Count.ShouldBe(2);
        surface.Cell(new Point(0, 3)).Text.ShouldBe("O");
    }

    private static HyperlinkButton NewLink(string text) => new(text)
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Width = Length.Cells(10),
        Height = Length.Cells(1)
    };
}
