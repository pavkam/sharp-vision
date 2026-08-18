// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

/// <summary>Runs the built probe with an isolated child environment.</summary>
internal static class ProbeRunner
{
    /// <summary>Runs one probe without mutating the test runner environment.</summary>
    /// <param name="terminalName">The exact terminal name passed to the probe.</param>
    /// <param name="environment">Environment replacements; a null value removes the inherited variable.</param>
    /// <returns>The complete process observation.</returns>
    internal static ProbeResult Run(
        string terminalName,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(terminalName);

        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";
        var probe = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "SharpVision.Terminal.Probe",
            "bin",
            configuration,
            "net10.0",
            "SharpVision.Terminal.Probe.dll"));
        var start = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath is { } processPath && Path.GetFileName(processPath).StartsWith("dotnet", StringComparison.Ordinal)
                ? processPath
                : "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(probe);
        start.ArgumentList.Add(terminalName);

        if (environment is not null)
        {
            foreach (var pair in environment)
            {
                if (pair.Value is null)
                {
                    _ = start.Environment.Remove(pair.Key);
                }
                else
                {
                    start.Environment[pair.Key] = pair.Value;
                }
            }
        }

        using var process = Process.Start(start) ??
            throw new InvalidOperationException("The terminal-description probe could not be started.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProbeResult(process.ExitCode, output, error);
    }
}
