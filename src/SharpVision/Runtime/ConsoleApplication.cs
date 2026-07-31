// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

/// <summary>Provides the fluent entry point for interactive console applications.</summary>
[PublicAPI]
public static class ConsoleApplication
{
    /// <summary>Creates a builder for one detached screen.</summary>
    /// <param name="screen">The non-null detached screen.</param>
    /// <returns>A fluent builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="screen"/> is null.</exception>
    public static ConsoleApplicationBuilder CreateBuilder(Screen screen) => new(screen);

    /// <summary>Configures and runs an interactive console application.</summary>
    /// <param name="screen">The non-null detached screen.</param>
    /// <param name="configure">Optional fluent configuration.</param>
    /// <returns>The run status.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="screen"/> is null.</exception>
    public static ValueTask<ConsoleRunStatus> RunAsync(
        Screen screen,
        Action<ConsoleApplicationBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(screen);
        var builder = new ConsoleApplicationBuilder(screen);
        configure?.Invoke(builder);
        return RunCoreAsync(builder, CancellationToken.None);
    }

    /// <summary>Runs an interactive console application with prebuilt options.</summary>
    /// <param name="screen">The non-null detached screen.</param>
    /// <param name="options">The non-null run options.</param>
    /// <returns>The run status.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static ValueTask<ConsoleRunStatus> RunAsync(Screen screen, ConsoleRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(options);
        var builder = new ConsoleApplicationBuilder(screen).ConfigureOptions(_ => options);
        return RunCoreAsync(builder, CancellationToken.None);
    }

    internal static async ValueTask<ConsoleRunStatus> RunCoreAsync(
        ConsoleApplicationBuilder builder,
        CancellationToken cancellationToken)
    {
        if (!builder.IsInteractive)
        {
            if (builder.Options.RedirectedMessage is { Length: > 0 } message)
            {
                builder.WriteLine(message);
            }

            return ConsoleRunStatus.Redirected;
        }

        Application application;

        try
        {
            application = builder.Build();
        }
        catch (UnsupportedTerminalException)
        {
            if (builder.Options.UnsupportedTerminalMessage is { Length: > 0 } message)
            {
                builder.WriteLine(message);
            }

            return ConsoleRunStatus.UnsupportedTerminal;
        }

        await using (application)
        {
            return await RunApplicationAsync(application, builder, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask<ConsoleRunStatus> RunApplicationAsync(
        Application application,
        ConsoleApplicationBuilder builder,
        CancellationToken cancellationToken)
    {
        using var cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        void OnCancel(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        }

        var observeCtrlC = !builder.Options.TreatControlCAsInput;

        if (observeCtrlC)
        {
            Console.CancelKeyPress += OnCancel;
        }

        try
        {
            await application.StartAsync(cancellation.Token).ConfigureAwait(false);
            var completion = application.Completion;
            var winner = await Task.WhenAny(
                completion,
                Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token)).ConfigureAwait(false);

            // Task.WhenAny never adopts the winning task's status; a post-startup application
            // fault on Completion must be awaited here to surface it, or it is lost and this
            // method falls through to the unguarded StopAsync call below (see #132). When the
            // delay wins instead, cancellation is already handled by the checks after the
            // try/catch, so Completion is left unobserved.
            if (ReferenceEquals(winner, completion))
            {
                await completion.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            await application.StopAsync(CancellationToken.None).ConfigureAwait(false);
            return ConsoleRunStatus.Cancelled;
        }
        catch (Exception exception)
        {
            try
            {
                await application.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Suppress cleanup errors so the original exception propagates.
            }

            Console.Error.WriteLine(exception);
            return ConsoleRunStatus.Failed;
        }
        finally
        {
            if (observeCtrlC)
            {
                Console.CancelKeyPress -= OnCancel;
            }
        }

        await application.StopAsync(CancellationToken.None).ConfigureAwait(false);

        return application.Failure is not null
            ? ConsoleRunStatus.Failed
            : cancellation.IsCancellationRequested
                ? ConsoleRunStatus.Cancelled
                : ConsoleRunStatus.Completed;
    }
}
