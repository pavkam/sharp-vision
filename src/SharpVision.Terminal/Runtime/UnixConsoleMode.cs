// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>Owns one best-effort Unix terminal raw-input lease for interactive console hosts.</summary>
internal sealed class UnixConsoleMode: IDisposable
{
    private readonly string? _restore;
    private readonly Func<string[], string> _run;
    private int _disposed;

    #region Construction and lifetime

    /// <summary>Initializes one lease with an optional captured terminal restoration value.</summary>
    /// <param name="restore">The non-empty saved terminal state, or null when the host is unsupported.</param>
    /// <param name="run">The terminal-utility boundary this lease restores through.</param>
    /// <exception cref="ArgumentException"><paramref name="restore"/> is whitespace.</exception>
    private UnixConsoleMode(string? restore, Func<string[], string> run)
    {
        if (restore is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(restore);
        }

        Debug.Assert(run is not null, "A lease always owns a terminal-utility boundary.");

        _restore = restore;
        _run = run;
    }

    /// <summary>Enters raw no-echo input mode when the current console is a supported Unix terminal.</summary>
    /// <param name="captureControlKeys">Whether Ctrl-key combinations should be delivered as input bytes instead of raising signals.</param>
    /// <param name="run">
    /// The terminal-utility boundary, or null for the real <c>/bin/stty</c> process. Tests supply
    /// this to reach the restoration-failure path, which cannot be provoked through the real
    /// utility without altering the developer's terminal.
    /// </param>
    /// <returns>An idempotent lease that restores the captured state when disposed.</returns>
    /// <exception cref="IOException">The current terminal state cannot be captured or raw mode cannot be enabled.</exception>
    /// <remarks>
    /// `isig` is restored after `raw` so Ctrl+C continues to raise the host's
    /// cancellation event while individual key, pointer, paste, and focus bytes
    /// arrive without canonical line buffering. When <paramref name="captureControlKeys"/>
    /// is true, `isig` is left disabled so Ctrl-key combinations arrive as input bytes.
    /// </remarks>
    public static UnixConsoleMode Enter(bool captureControlKeys, Func<string[], string>? run = null)
    {
        var invoke = run ?? Run;

        if (run is null && !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return new UnixConsoleMode(restore: null, invoke);
        }

        var restore = invoke(["-g"]).Trim();

        try
        {
            _ = captureControlKeys
                ? invoke(["raw", "-echo"])
                : invoke(["raw", "-echo", "isig"]);
            return new UnixConsoleMode(restore, invoke);
        }
        catch
        {
            // Entry already failed. Restoring is best effort here precisely because the caller
            // must see why raw mode could not be established, not why the undo also failed.
            try
            {
                _ = invoke([restore]);
            }
            catch (IOException)
            {
            }

            throw;
        }
    }

    /// <summary>Restores the captured terminal input state exactly once.</summary>
    /// <remarks>
    /// Restoration failure is reported rather than swallowed. Silently discarding it let cleanup
    /// claim success while the user's terminal stayed raw, without echo and without canonical
    /// input, and gave no layer above a chance to react. The owning connection folds this into a
    /// cleanup diagnostic, so a primary application failure is still preserved.
    /// </remarks>
    /// <exception cref="IOException">The captured terminal state could not be restored.</exception>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Interlocked.Exchange(ref _disposed, 1) != 0 || _restore is null)
        {
            return;
        }

        _ = _run([_restore]);
    }

    #endregion

    #region Process boundary

    private static string Run(string[] arguments)
    {
        var start = new ProcessStartInfo("/bin/stty")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
                            ?? throw new IOException("The terminal raw-mode utility could not start.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return process.ExitCode == 0
            ? output
            : throw new IOException($"The terminal raw-mode utility failed: {error.Trim()}");
    }

    #endregion
}
