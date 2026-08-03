// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Verifies MenuItem shortcuts dispatch before routed key handling, application-wide.</summary>
public sealed class ApplicationShortcutTests
{
    /// <summary>Verifies a shortcut invokes its item without ever reaching Router.Route, so a
    /// focused TextInput neither consumes the chord nor sees it as typed text.</summary>
    [Fact]
    public async Task Input_WhenShortcutMatchesWhileTextInputIsFocused_InvokesItemBeforeRoutingAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput { Text = "seed" };
        var menu = new Menu();
        var gesture = new KeyGesture(Code.Character, Modifiers.Control, new Rune('s'));
        var save = new MenuItem { Content = new ControlText("Save"), Shortcut = gesture };
        menu.Items.Add(save);
        var root = new Stack { Children = { input, menu } };
        var routedKeyPresses = 0;
        _ = root.AddHandler(Events.Key, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Preview)
            {
                routedKeyPresses++;
            }
        });
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        ActivationCause? cause = null;
        save.Invoked += (_, eventArgs) => cause = eventArgs.Cause;
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(input).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        var stroke = new Stroke(Code.Character, new Rune('s'), nativeCode: 0, Modifiers.Control, KeyAction.Press);

        // Act
        application.Input(in stroke);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        cause.ShouldBe(ActivationCause.Keyboard);
        routedKeyPresses.ShouldBe(0);
        input.Text.ShouldBe("seed");
        application.Focus.Focused.ShouldBeSameAs(input);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an unmatched key still reaches routed handling normally.</summary>
    [Fact]
    public async Task Input_WhenNoShortcutMatches_RoutesTheKeyNormallyAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput();
        var menu = new Menu();
        var save = new MenuItem
        {
            Content = new ControlText("Save"),
            Shortcut = new KeyGesture(Code.Character, Modifiers.Control, new Rune('s'))
        };
        menu.Items.Add(save);
        var root = new Stack { Children = { input, menu } };
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        var invocations = 0;
        save.Invoked += (_, _) => invocations++;
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(input).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        var stroke = new Stroke(Code.Character, new Rune('x'), nativeCode: 0, Modifiers.None, KeyAction.Press);
        var text = new Terminal.Input.Text(new Rune('x'));

        // Act
        application.Input(in stroke);
        application.Input(in text);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        invocations.ShouldBe(0);
        input.Text.ShouldBe("x");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }
}
