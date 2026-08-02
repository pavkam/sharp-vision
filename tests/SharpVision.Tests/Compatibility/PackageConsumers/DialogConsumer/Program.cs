// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.DialogConsumer;

/// <summary>Runs a derived dialog through the protected PresentAsync overload from a public attached
/// application host.</summary>
internal static class Program
{
    private static async Task Main()
    {
        var opener = new Button { Content = new DisplayText("Open") };
        var root = new Overlay { Children = { opener } };
        var terminal = new ConsumerTerminal();
        await using var application = new Application(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);

        await application.StartAsync();

        var dialog = new ConsumerDialog();
        Task<bool> task = null!;

        await application.Dispatcher.InvokeAsync(() =>
        {
            task = dialog.ShowAsync(opener, CancellationToken.None);

            if (dialog.Parent is null)
            {
                throw new InvalidOperationException(
                    "The external dialog did not attach to the owner's presentation host.");
            }

            dialog.Accept();
        });

        var result = await task;

        if (!result)
        {
            throw new InvalidOperationException("The external dialog did not complete with the typed result.");
        }

        if (!dialog.IsDisposed)
        {
            throw new InvalidOperationException("The external dialog was not disposed after completion.");
        }
    }
}
