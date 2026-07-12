using System.Diagnostics;

namespace SharpVision.Showcase;

/// <summary>Owns one best-effort Unix terminal raw-input lease for the interactive showcase host.</summary>
internal sealed class ConsoleRawMode: IDisposable
{
    private readonly string? _restore;
    private int _disposed;

    #region Construction and lifetime

    /// <summary>Initializes one lease with an optional captured terminal restoration value.</summary>
    /// <param name="restore">The non-empty saved terminal state, or null when the host is unsupported.</param>
    /// <exception cref="ArgumentException"><paramref name="restore"/> is whitespace.</exception>
    private ConsoleRawMode(string? restore)
    {
        if (restore is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(restore);
        }

        _restore = restore;
    }

    /// <summary>Enters raw no-echo input mode when the current console is a supported Unix terminal.</summary>
    /// <returns>An idempotent lease that restores the captured state when disposed.</returns>
    /// <exception cref="IOException">The current terminal state cannot be captured or raw mode cannot be enabled.</exception>
    /// <remarks>
    /// `isig` is restored after `raw` so Ctrl+C continues to raise the host's
    /// cancellation event while individual key, pointer, paste, and focus bytes
    /// arrive without canonical line buffering.
    /// </remarks>
    internal static ConsoleRawMode Enter()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return new ConsoleRawMode(restore: null);
        }

        var restore = Run("-g").Trim();

        try
        {
            _ = Run("raw", "-echo", "isig");
            return new ConsoleRawMode(restore);
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
        var start = new ProcessStartInfo("/bin/stty")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
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
