// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Verifies routed precedence and paired legacy text suppression for hosted access keys.</summary>
public sealed class ApplicationAccessKeyTests
{
    /// <summary>Verifies a handled Alt key consumes its adjacent text record after activating the target.</summary>
    [Fact]
    public async Task Input_WhenAltKeyActivatesButton_SuppressesAdjacentMnemonicTextAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput { Text = "seed" };
        var button = new Button { Content = new ControlText("&Name") };
        var root = new Stack { Children = { input, button } };
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        var clicks = 0;
        button.Click += (_, eventArgs) =>
        {
            eventArgs.Cause.ShouldBe(ActivationCause.Keyboard);
            clicks++;
        };
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(input).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        var stroke = Alt('n');
        var text = new Terminal.Input.Text(new Rune('n'));

        // Act
        application.Input(in stroke);
        application.Input(in text);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        clicks.ShouldBe(1);
        input.Text.ShouldBe("seed");
        application.Focus.Focused.ShouldBeSameAs(button);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an earlier preview handler can reserve Alt input and preserve its text event.</summary>
    [Fact]
    public async Task Input_WhenPreviewHandlesAltKey_DoesNotInvokeOrSuppressTextAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput();
        var button = new Button { Content = new ControlText("&Name") };
        var root = new Stack { Children = { input, button } };
        _ = root.AddHandler(Events.Key, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Preview && eventArgs.Stroke.Modifiers == Modifiers.Alt)
            {
                eventArgs.Handled = true;
            }
        });
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(input).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        var stroke = Alt('n');
        var text = new Terminal.Input.Text(new Rune('n'));

        // Act
        application.Input(in stroke);
        application.Input(in text);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // Assert
        clicks.ShouldBe(0);
        input.Text.ShouldBe("n");
        application.Focus.Focused.ShouldBeSameAs(input);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    private static Stroke Alt(char value) =>
        new(Code.Character, new Rune(value), nativeCode: 0, Modifiers.Alt, KeyAction.Press);
}
