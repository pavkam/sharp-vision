// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using SharpVision.Terminal.Runtime;
using SharpVision.Terminal.Transport;

public sealed partial class Application
{
    #region Console host

    /// <summary>
    /// Prepares the interactive console, runs the screen to completion, and restores host state.
    /// </summary>
    /// <param name="screen">The non-null detached screen.</param>
    /// <param name="options">Optional console run policy.</param>
    /// <returns>A status that reports redirect, completion, cancellation, or failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="screen"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="screen"/> is already attached.</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="screen"/> is disposed.</exception>
    /// <exception cref="IOException">The console host cannot enter raw input mode.</exception>
    public static async ValueTask<ConsoleRunStatus> RunConsoleAsync(
        Screen screen,
        ConsoleRunOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(screen);
        options ??= new ConsoleRunOptions();

        if (!ConsoleHost.IsInteractive)
        {
            if (options.RedirectedMessage is { Length: > 0 })
            {
                Console.WriteLine(options.RedirectedMessage);
            }

            return ConsoleRunStatus.Redirected;
        }

        using UnixConsoleMode inputMode = UnixConsoleMode.Enter(captureControlKeys: false);
        StreamTransport transport = ConsoleHost.CreateTransport();
        ConsoleResizeSource resize = new(TimeSpan.FromMilliseconds(100));
        await using Application application = new(
            screen,
            transport,
            resize,
            ConsoleRun.CreateTerminalOptions());
        screen.Attach(application);

        using CancellationTokenSource cancellation = new();
        void OnCancel(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        }

        Console.CancelKeyPress += OnCancel;

        try
        {
            await application.StartAsync(cancellation.Token);

            _ = await Task.WhenAny(
                application.Completion,
                Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            await application.StopAsync(CancellationToken.None);
            return ConsoleRunStatus.Cancelled;
        }
        finally
        {
            Console.CancelKeyPress -= OnCancel;
        }

        await application.StopAsync(CancellationToken.None);

        return application.Failure is not null
            ? ConsoleRunStatus.Failed
            : cancellation.IsCancellationRequested
            ? ConsoleRunStatus.Cancelled
            : ConsoleRunStatus.Completed;
    }

    #endregion
}
