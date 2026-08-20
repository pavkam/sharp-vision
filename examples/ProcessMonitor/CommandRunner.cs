// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace ProcessMonitor;

/// <summary>Runs one external command and captures its standard output as text.</summary>
/// <remarks>
/// Every system sample this application takes - the process table, CPU load, and memory pressure -
/// comes from a handful of well-known Unix command-line tools rather than platform interop, so the
/// sampling code stays a few lines of text parsing per fact instead of a native binding per
/// platform. This is the one place that actually launches those tools.
/// </remarks>
internal static class CommandRunner
{
    /// <summary>Runs one command to completion and returns its captured standard output.</summary>
    /// <param name="fileName">The non-empty executable name, resolved through <c>PATH</c>.</param>
    /// <param name="arguments">The non-null literal argument list, one entry per shell-level token.</param>
    /// <param name="cancellationToken">The token observed while the process runs.</param>
    /// <returns>The complete standard output text, or empty when the command exits non-zero.</returns>
    /// <exception cref="ArgumentException"><paramref name="fileName"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> is null.</exception>
    internal static async Task<string> CaptureOutputAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            _ = process.Start();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            // The tool is missing or not executable on this system - callers treat an empty
            // capture as "no data this tick" rather than a fatal error, so a stripped-down
            // container or an unexpected Unix variant degrades instead of crashing the monitor.
            return string.Empty;
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var output = await outputTask.ConfigureAwait(false);
        _ = await errorTask.ConfigureAwait(false);

        return process.ExitCode == 0 ? output : string.Empty;
    }
}
