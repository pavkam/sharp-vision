// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.InputBaseConsumer;

/// <summary>Runs a derived InputBase through a public attached application host, exercising every
/// protected capability seam from outside the packed assembly.</summary>
internal static class Program
{
    private static async Task Main()
    {
        var input = new ConsumerInput();
        var root = new Overlay { Children = { input } };
        var terminal = new ConsumerTerminal();
        await using var application = new Application(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);

        await application.StartAsync();

        await application.Dispatcher.InvokeAsync(() =>
        {
            if (!input.IsFocusable || !input.IsTabStop)
            {
                throw new InvalidOperationException(
                    "The external InputBase derivative was not focusable and Tab-stopping by default.");
            }

            if (!ReferenceEquals(input.Popup.Parent, input))
            {
                throw new InvalidOperationException(
                    "The external InputBase derivative's owned popup did not register under it.");
            }

            if (ConsumerInput.IndicatorWidth != 1)
            {
                throw new InvalidOperationException(
                    "DropDownIndicatorWidth did not resolve to one cell from outside the assembly.");
            }

            var up = new KeyEventArgs(new Stroke(Code.Up, character: null, nativeCode: 0, Modifiers.None, KeyAction.Press));

            if (!input.ProbeTryGetStepDelta(up, out var delta) || delta != 1)
            {
                throw new InvalidOperationException(
                    "TryGetStepDelta did not translate Up to +1 from outside the assembly.");
            }

            _ = input.ProbeResolveDropDownGlyph(new Rune('v'));
            input.ProbeVerifyMutable();

            input.Text = "Consumer caption";

            if (!string.Equals(input.Text, "Consumer caption", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The caption capability did not round-trip Text from outside the assembly.");
            }

            var command = new ConsumerCommand();
            var parameter = new object();
            input.Command = command;
            input.CommandParameter = parameter;

            var enter = new KeyEventArgs(new Stroke(Code.Enter, character: null, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(input, Events.Key, enter);

            if (input.Activations != 1 || !input.IsOpen)
            {
                throw new InvalidOperationException(
                    "Press activation did not toggle the owned popup from outside the assembly.");
            }

            if (command.ExecutedParameter is null || !ReferenceEquals(command.ExecutedParameter, parameter))
            {
                throw new InvalidOperationException(
                    "The command capability did not execute the bound command from outside the assembly.");
            }

            // A separate, never-attached instance exercises the disposed guard in isolation, so
            // disposing it cannot disturb the live application tree still under the dispatcher.
            using var detached = new ConsumerInput();
            detached.Dispose();

            var disposedCorrectly = false;

            try
            {
                detached.ProbeVerifyMutable();
            }
            catch (ObjectDisposedException)
            {
                disposedCorrectly = true;
            }

            if (!disposedCorrectly)
            {
                throw new InvalidOperationException(
                    "VerifyMutable did not throw ObjectDisposedException after disposal.");
            }
        });
    }
}
