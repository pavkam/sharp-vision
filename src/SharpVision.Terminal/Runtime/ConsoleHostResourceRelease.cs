// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>
/// Best-effort reverse-release helpers shared by the platform console hosts' partially
/// constructed failure paths.
/// </summary>
/// <remarks>
/// A partially constructed console must never let a cleanup failure replace the construction
/// failure the caller needs to see, so every release here swallows <see cref="IOException"/> and
/// <see cref="ObjectDisposedException"/> instead of propagating them.
/// </remarks>
internal static class ConsoleHostResourceRelease
{
    /// <summary>Disposes an already-entered console-mode lease, swallowing a cleanup-only failure.</summary>
    /// <param name="mode">The lease to dispose.</param>
    /// <remarks>
    /// Named apart from <see cref="ReleaseResource"/> rather than overloaded on parameter type:
    /// <see cref="Stream"/> (the type of the owned tty/console streams released through
    /// <see cref="ReleaseResource"/>) implements both <see cref="IDisposable"/> and
    /// <see cref="IAsyncDisposable"/>, so a same-named <c>IDisposable</c> overload would make every
    /// stream release ambiguous.
    /// </remarks>
    public static void ReleaseMode(IDisposable mode)
    {
        try
        {
            mode.Dispose();
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
        }
    }

    /// <summary>
    /// Disposes an owned console resource if it was actually constructed, swallowing a
    /// cleanup-only failure.
    /// </summary>
    /// <param name="resource">The resource to dispose, or null if it was never constructed.</param>
    public static void ReleaseResource(IAsyncDisposable? resource)
    {
        if (resource is null)
        {
            return;
        }

        try
        {
            // Every owned console resource completes disposal synchronously, so this failure path
            // stays synchronous rather than forcing an async signature onto Open.
            var disposal = resource.DisposeAsync();
            Debug.Assert(disposal.IsCompleted, "Console-owned resources dispose synchronously.");
            disposal.AsTask().GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
        }
    }
}
