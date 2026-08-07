// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Compatibility.Tests;

using System.Runtime.CompilerServices;

using DiffEngine;

/// <summary>
/// Configures Verify for interactive and agent-hosted test runs.
/// </summary>
internal static class VerifyDiffConfiguration
{
    private static readonly string[] _agentEnvironmentVariables =
    [
        "CODEX_CI",
        "CODEX_THREAD_ID",
        "CURSOR_AGENT",
        "OPENCODE_CLIENT",
        "OPENCODE_TERMINAL",
    ];

    /// <summary>
    /// Disables interactive diff viewers when the test process is hosted by an agent.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        if (AiCliDetector.Detected || IsAgentEnvironment(Environment.GetEnvironmentVariable))
        {
            DiffRunner.Disabled = true;
        }
    }

    /// <summary>
    /// Determines whether a known agent host marker exists in the current environment.
    /// </summary>
    /// <param name="getEnvironmentVariable">Returns the value for an environment variable name.</param>
    /// <returns><see langword="true"/> when a known agent marker exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="getEnvironmentVariable"/> is <see langword="null"/>.</exception>
    internal static bool IsAgentEnvironment(Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        foreach (var environmentVariable in _agentEnvironmentVariables)
        {
            if (getEnvironmentVariable(environmentVariable) is not null)
            {
                return true;
            }
        }

        return false;
    }
}
