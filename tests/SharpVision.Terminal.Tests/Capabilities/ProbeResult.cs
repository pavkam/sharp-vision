// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

/// <summary>Owns one process-isolated terminal-description probe observation.</summary>
internal sealed class ProbeResult
{
    /// <summary>Initializes one immutable probe observation.</summary>
    /// <param name="exitCode">The child-process exit code.</param>
    /// <param name="output">The complete child standard output.</param>
    /// <param name="error">The complete child standard error.</param>
    internal ProbeResult(int exitCode, string output, string error)
    {
        ExitCode = exitCode;
        Output = output;
        Error = error;
        Values = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
    }

    /// <summary>Gets the child exit code.</summary>
    internal int ExitCode { get; }

    /// <summary>Gets the complete child standard output.</summary>
    internal string Output { get; }

    /// <summary>Gets the complete child standard error.</summary>
    internal string Error { get; }

    /// <summary>Gets the parsed semantic fact lines.</summary>
    internal IReadOnlyDictionary<string, string> Values { get; }
}
