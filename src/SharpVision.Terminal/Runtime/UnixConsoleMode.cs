// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using System.Diagnostics;

/// <summary>Owns one best-effort Unix terminal raw-input lease for interactive console hosts.</summary>
public sealed class UnixConsoleMode: IDisposable
{
    private readonly string? _restore;
    private int _disposed;

    #region Construction and lifetime

    /// <summary>Initializes one lease with an optional captured terminal restoration value.</summary>
    /// <param name="restore">The non-empty saved terminal state, or null when the host is unsupported.</param>
    /// <exception cref="ArgumentException"><paramref name="restore"/> is whitespace.</exception>
    private UnixConsoleMode(string? restore)
    {
        if (restore is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(restore);
        }

        _restore = restore;
    }

    /// <summary>Enters raw no-echo input mode when the current console is a supported Unix terminal.</summary>
    /// <param name="captureControlKeys">Whether Ctrl-key combinations should be delivered as input bytes instead of raising signals.</param>
    /// <returns>An idempotent lease that restores the captured state when disposed.</returns>
    /// <exception cref="IOException">The current terminal state cannot be captured or raw mode cannot be enabled.</exception>
    /// <remarks>
    /// `isig` is restored after `raw` so Ctrl+C continues to raise the host's
    /// cancellation event while individual key, pointer, paste, and focus bytes
    /// arrive without canonical line buffering. When <paramref name="captureControlKeys"/>
    /// is true, `isig` is left disabled so Ctrl-key combinations arrive as input bytes.
    /// </remarks>
    public static UnixConsoleMode Enter(bool captureControlKeys)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return new UnixConsoleMode(restore: null);
        }

        string restore = Run("-g").Trim();

        try
        {
            _ = captureControlKeys
                ? Run("raw", "-echo")
                : Run("raw", "-echo", "isig");
            return new UnixConsoleMode(restore);
        }
        catch
        {
            TryRestore(restore);
            throw;
        }
    }

    /// <summary>Restores the captured terminal input state once without hiding an earlier application failure.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && _restore is not null)
        {
            TryRestore(_restore);
        }

        GC.SuppressFinalize(this);
    }

    #endregion

    #region Process boundary

    private static void TryRestore(string value)
    {
        try
        {
            _ = Run(value);
        }
        catch (IOException)
        {
            // Application cleanup must restore what it can without obscuring
            // the primary terminal, rendering, or callback failure.
        }
    }

    private static string Run(params string[] arguments)
    {
        ProcessStartInfo start = new("/bin/stty")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)
            ?? throw new IOException("The terminal raw-mode utility could not start.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return process.ExitCode == 0
            ? output
            : throw new IOException($"The terminal raw-mode utility failed: {error.Trim()}");
    }

    #endregion
}
